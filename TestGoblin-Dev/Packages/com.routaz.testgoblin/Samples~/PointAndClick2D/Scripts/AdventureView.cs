using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace DemoGame.Adventure
{
    /// <summary>
    /// Builds the point-and-click demo — login, a fake load, and a room with two hotspots.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything clickable is a sprite with a <c>Collider2D</c>, including the login button,
    /// because a game driven by <c>PhysicsGameDriver</c> must be reachable by a physics
    /// raycast. Mixing uGUI widgets in would need a second driver, which a single scenario
    /// cannot use.
    /// </para>
    /// <para>
    /// Built in code rather than authored, so the sample is reviewable as C# and the object
    /// names — which are the locators tests address it by — are all in one place.
    /// </para>
    /// </remarks>
    public sealed class AdventureView : MonoBehaviour
    {
        public const string LoginButtonName = "LoginButton";
        public const string LoadingScreenName = "LoadingScreen";
        public const string LoadingLabelName = "LoadingLabel";
        public const string RoomScreenName = "RoomScreen";
        public const string KeyHotspotName = "Hotspot_Key";
        public const string DoorHotspotName = "Hotspot_Door";
        public const string KeyInventorySlotName = "InventorySlot_Key";
        public const string DoorStateLabelName = "DoorStateLabel";

        /// <summary>Long enough that reaching the room requires a genuine wait.</summary>
        public const float LoadingSeconds = 1.5f;

        private AdventureState _state;
        private GameObject _loginScreen;
        private GameObject _loadingScreen;
        private GameObject _roomScreen;
        private GameObject _keyHotspot;
        private GameObject _keySlot;
        private TMP_Text _loadingLabel;
        private TMP_Text _doorStateLabel;
        private TMP_Text _cursorLabel;
        private Camera _camera;

        /// <summary>The state being displayed. Tests read it through the world, not this.</summary>
        public AdventureState State => _state;

        private void Awake()
        {
            _state = new AdventureState();

            EnsureEventSystem();
            EnsureCamera();

            _loginScreen = BuildLoginScreen();
            _loadingScreen = BuildLoadingScreen();
            _roomScreen = BuildRoomScreen();

            _loginScreen.SetActive(true);
            _loadingScreen.SetActive(false);
            _roomScreen.SetActive(false);

            Refresh();
        }

        private void OnLogin()
        {
            _loginScreen.SetActive(false);
            _loadingScreen.SetActive(true);
            StartCoroutine(LoadThenEnterRoom());
        }

        /// <summary>Fills the loading label over time, then reveals the room.</summary>
        /// <remarks>
        /// Spread across frames on purpose: an instant transition would let a test "wait"
        /// for a screen that had already arrived, proving nothing about waiting.
        /// </remarks>
        private IEnumerator LoadThenEnterRoom()
        {
            float elapsed = 0f;

            while (elapsed < LoadingSeconds)
            {
                elapsed += Time.deltaTime;
                _loadingLabel.text = $"{Mathf.RoundToInt(Mathf.Clamp01(elapsed / LoadingSeconds) * 100f)}%";
                yield return null;
            }

            _loadingScreen.SetActive(false);
            _roomScreen.SetActive(true);
            Refresh();
        }

        private void OnKeyClicked()
        {
            if (!_state.CanPickUp(AdventureState.Key))
            {
                return;
            }

            _state.PickUp(AdventureState.Key);
            Refresh();
        }

        private void OnKeySlotClicked()
        {
            _state.Select(AdventureState.Key);
            Refresh();
        }

        private void OnDoorClicked()
        {
            // A click with nothing useful selected is an ordinary thing for a player to do,
            // so it is ignored rather than thrown. A test asserts on the resulting state.
            if (!_state.CanUseSelectedOn(AdventureState.Door))
            {
                return;
            }

            _state.UseSelectedOn(AdventureState.Door);
            Refresh();
        }

        /// <summary>
        /// Pushes state into the world.
        /// </summary>
        /// <remarks>
        /// The key hotspot and the inventory slot are <em>deactivated</em>, not merely
        /// recoloured, so "the key is no longer in the room" and "the player is carrying it"
        /// are both things a test can assert by absence and presence.
        /// </remarks>
        private void Refresh()
        {
            bool carryingKey = _state.IsCarrying(AdventureState.Key);

            _keyHotspot.SetActive(!carryingKey);
            _keySlot.SetActive(carryingKey);
            _doorStateLabel.text = _state.IsDoorOpen ? "Open" : "Closed";
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            // InputSystemUIInputModule, not the legacy one: this project uses the new Input
            // System, and StandaloneInputModule throws when it reads UnityEngine.Input.
            var eventSystem = new GameObject(
                "EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            eventSystem.transform.SetParent(null);
        }

        /// <summary>
        /// Makes sure there is an orthographic camera that can deliver 2D pointer events.
        /// </summary>
        /// <remarks>
        /// The <c>Physics2DRaycaster</c> is what lets a human click sprites. The driver does
        /// not need it — it raycasts itself — but a sample nobody can play by hand is a poor
        /// demonstration, and its absence would be invisible to every test.
        /// </remarks>
        private void EnsureCamera()
        {
            _camera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();

            if (_camera == null)
            {
                var cameraObject = new GameObject("Main Camera", typeof(Camera));
                cameraObject.tag = "MainCamera";
                _camera = cameraObject.GetComponent<Camera>();
            }

            _camera.orthographic = true;
            _camera.orthographicSize = 5f;
            _camera.transform.position = new Vector3(0f, 0f, -10f);

            if (_camera.GetComponent<Physics2DRaycaster>() == null)
            {
                _camera.gameObject.AddComponent<Physics2DRaycaster>();
            }
        }

        /// <summary>
        /// Keeps the verb line under the pointer while an item is held.
        /// </summary>
        /// <remarks>
        /// Purely presentational — it carries no collider, so it can never become what a
        /// click lands on. A label that followed the cursor and also blocked it would make
        /// the game unplayable the moment you picked anything up.
        /// </remarks>
        private void Update()
        {
            if (_cursorLabel == null)
            {
                return;
            }

            bool holding = !string.IsNullOrEmpty(_state.SelectedItem);
            _cursorLabel.gameObject.SetActive(holding);

            if (!holding)
            {
                return;
            }

            _cursorLabel.text = $"Using {_state.SelectedItem} with…";

            // Mouse.current is null when nothing is driving input — a headless test run, for
            // instance — so the label simply stays put rather than throwing.
            if (Mouse.current == null)
            {
                return;
            }

            Vector2 pointer = Mouse.current.position.ReadValue();
            Vector3 world = _camera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y, 10f));

            // Offset up and to the right so the text does not sit under the cursor itself.
            _cursorLabel.transform.position = world + new Vector3(1.6f, 0.4f, -2f);
        }

        private GameObject BuildLoginScreen()
        {
            var screen = new GameObject("LoginScreen");

            BuildLabel(screen.transform, "LoginTitle", "Point & Click Demo", new Vector3(0f, 2f, 0f));
            BuildHotspot(screen.transform, LoginButtonName, "Log in",
                new Vector3(0f, 0f, 0f), new Vector2(4f, 1.2f), new Color(0.85f, 0.85f, 0.85f), OnLogin);

            return screen;
        }

        private GameObject BuildLoadingScreen()
        {
            var screen = new GameObject(LoadingScreenName);

            // A collider so the driver can find and hit-test the screen itself, which is what
            // makes "the loading screen is gone" an assertable claim.
            var collider = screen.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(6f, 2f);

            BuildLabel(screen.transform, "LoadingTitle", "Loading…", new Vector3(0f, 1f, 0f));
            _loadingLabel = BuildLabel(screen.transform, LoadingLabelName, "0%", new Vector3(0f, 0f, 0f));

            return screen;
        }

        private GameObject BuildRoomScreen()
        {
            var screen = new GameObject(RoomScreenName);

            var collider = screen.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.5f, 0.5f);
            screen.transform.position = new Vector3(0f, 4f, 0f);

            _keyHotspot = BuildHotspot(screen.transform, KeyHotspotName, "Key",
                new Vector3(-3f, -1f, 0f), new Vector2(1.5f, 1.5f), new Color(0.9f, 0.8f, 0.3f), OnKeyClicked);

            BuildHotspot(screen.transform, DoorHotspotName, "Door",
                new Vector3(3f, -1f, 0f), new Vector2(2f, 3f), new Color(0.6f, 0.4f, 0.2f), OnDoorClicked);

            _keySlot = BuildHotspot(screen.transform, KeyInventorySlotName, "Key",
                new Vector3(-5f, -4f, 0f), new Vector2(1f, 1f), new Color(0.9f, 0.8f, 0.3f), OnKeySlotClicked);

            _doorStateLabel = BuildLabel(screen.transform, DoorStateLabelName, "Closed", new Vector3(3f, -3.5f, 0f));

            BuildLabel(screen.transform, "InventoryTitle", "Inventory", new Vector3(-5f, -2.9f, 0f));

            _cursorLabel = BuildLabel(screen.transform, "CursorLabel", "", new Vector3(0f, 0f, -2f));
            _cursorLabel.fontSize = 3f;
            _cursorLabel.gameObject.SetActive(false);

            return screen;
        }

        private GameObject BuildHotspot(
            Transform parent, string name, string caption, Vector3 position,
            Vector2 size, Color colour, System.Action onClick)
        {
            var hotspot = new GameObject(name);
            hotspot.transform.SetParent(parent, worldPositionStays: false);
            hotspot.transform.position = position;

            var renderer = hotspot.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateSprite();
            renderer.color = colour;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;

            var collider = hotspot.AddComponent<BoxCollider2D>();
            collider.size = size;

            hotspot.AddComponent<Hotspot>().Initialise(onClick);

            BuildLabel(hotspot.transform, "Label", caption, position + new Vector3(0f, 0f, -1f));

            return hotspot;
        }

        private static TMP_Text BuildLabel(Transform parent, string name, string text, Vector3 position)
        {
            var labelObject = new GameObject(name);
            labelObject.transform.SetParent(parent, worldPositionStays: false);
            labelObject.transform.position = position;

            var label = labelObject.AddComponent<TextMeshPro>();
            label.text = text;
            label.fontSize = 4f;
            label.color = Color.black;
            label.alignment = TextAlignmentOptions.Center;

            // No collider: a label must never be what a click lands on.
            return label;
        }

        private static Sprite CreateSprite()
        {
            var texture = new Texture2D(8, 8);
            var pixels = new Color[64];

            for (int index = 0; index < pixels.Length; index++)
            {
                pixels[index] = Color.white;
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), pixelsPerUnit: 8f);
        }
    }
}
