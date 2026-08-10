using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace DemoGame.Vault
{
    /// <summary>
    /// Builds the vault: a lever, a wall, a key behind that wall, and a gate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The wall is the point of this sample. The key sits behind it, and the game itself does
    /// <em>not</em> forbid taking it — nothing in <see cref="VaultState"/> mentions the wall.
    /// What stops you is geometry, and only a driver that raycasts can tell. Pull the lever,
    /// the wall retracts, and the same click starts working.
    /// </para>
    /// <para>
    /// Built in code rather than authored, so the object names — which are the locators tests
    /// address it by — all live in one readable place.
    /// </para>
    /// </remarks>
    public sealed class VaultView : MonoBehaviour
    {
        public const string LeverHotspotName = "Hotspot_Lever";
        public const string KeyHotspotName = "Hotspot_Key";
        public const string GateHotspotName = "Hotspot_Gate";
        public const string WallName = "Wall";
        public const string GateStateLabelName = "GateStateLabel";
        public const string InventoryButtonName = "InventoryButton";
        public const string InventoryPanelName = "InventoryPanel";
        public const string KeySlotName = "KeySlot";

        private VaultState _state;
        private GameObject _wall;
        private GameObject _key;
        private TMP_Text _gateStateLabel;
        private Camera _camera;
        private Vector3 _wallClosedPosition;
        private Vector3 _wallOpenPosition;
        private VisualElement _inventoryPanel;
        private VisualElement _keySlot;

        /// <summary>The state being displayed. Tests read it through the world, not this.</summary>
        public VaultState State => _state;

        private void Awake()
        {
            _state = new VaultState();

            EnsureEventSystem();
            EnsureCamera();
            BuildRoom();
            Refresh();
        }

        /// <summary>
        /// Builds the UI Toolkit HUD once the document has produced its root.
        /// </summary>
        /// <remarks>
        /// A <c>UIDocument</c> creates its root on enable, not on AddComponent, so this waits
        /// until Start rather than racing it in Awake.
        /// </remarks>
        private void Start()
        {
            BuildHud();
            Refresh();
        }

        private void OnLeverClicked()
        {
            _state.PullLever();
            Refresh();
        }

        private void OnKeyClicked()
        {
            if (!_state.CanTakeKey)
            {
                return;
            }

            _state.TakeKey();
            Refresh();
        }

        private void OnGateClicked()
        {
            // Clicking the gate empty-handed is an ordinary thing to try, so it is ignored
            // rather than thrown. A test asserts on the resulting state.
            if (!_state.CanUnlockGate)
            {
                return;
            }

            _state.UnlockGate();
            Refresh();
        }

        /// <summary>
        /// Pushes state into the world.
        /// </summary>
        /// <remarks>
        /// The wall is <em>moved</em>, not deactivated. Deactivating it would make the key
        /// reachable for the trivial reason that nothing is there any more; moving it keeps a
        /// real collider in the scene and proves the raycast is what changed its mind.
        /// </remarks>
        private void Refresh()
        {
            _wall.transform.position = _state.IsWallOpen ? _wallOpenPosition : _wallClosedPosition;

            _key.SetActive(!_state.HasKey);
            _gateStateLabel.text = _state.IsGateOpen ? "Open" : "Locked";

            // Null until Start builds the HUD, since a UIDocument has no root before then.
            if (_keySlot != null)
            {
                _keySlot.style.display = _state.HasKey ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>
        /// Builds a UI Toolkit HUD over the 3D world: a button, and the inventory it opens.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is what makes the vault a mixed-technology game — the shape most real 3D games
        /// have. The world is meshes, the interface is UI Toolkit, and a scenario needs a
        /// composite driver to touch both.
        /// </para>
        /// <para>
        /// Sizes are in pixels, not percentages. A panel built in code has no theme style
        /// sheet, so the root never resolves a size and a "100%" child would silently collapse
        /// to nothing — an overlay that covers no pixels while looking correct in the code.
        /// </para>
        /// </remarks>
        private void BuildHud()
        {
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();

            var hudObject = new GameObject("Hud");
            hudObject.transform.SetParent(transform, worldPositionStays: false);

            var document = hudObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;

            VisualElement root = document.rootVisualElement;

            if (root == null)
            {
                return;
            }

            // Critical: without this the root is picked at every screen position, so the
            // composite driver would treat the entire world as permanently covered by the HUD.
            root.pickingMode = PickingMode.Ignore;

            _inventoryPanel = new VisualElement { name = InventoryPanelName };
            _inventoryPanel.style.position = Position.Absolute;
            _inventoryPanel.style.left = 0;
            _inventoryPanel.style.top = 0;
            _inventoryPanel.style.width = 4000;
            _inventoryPanel.style.height = 4000;
            _inventoryPanel.style.backgroundColor = new Color(0.1f, 0.1f, 0.15f, 0.85f);
            _inventoryPanel.style.display = DisplayStyle.None;
            root.Add(_inventoryPanel);

            var title = new Label("Inventory") { name = "InventoryTitle" };
            title.style.position = Position.Absolute;
            title.style.left = 40;
            title.style.top = 100;
            title.style.fontSize = 28;
            title.style.color = Color.white;
            _inventoryPanel.Add(title);

            _keySlot = new VisualElement { name = KeySlotName };
            _keySlot.style.position = Position.Absolute;
            _keySlot.style.left = 40;
            _keySlot.style.top = 150;
            _keySlot.style.width = 80;
            _keySlot.style.height = 80;
            _keySlot.style.backgroundColor = new Color(0.9f, 0.8f, 0.3f);
            _keySlot.style.display = DisplayStyle.None;
            _inventoryPanel.Add(_keySlot);

            // Added AFTER the panel so it draws — and picks — on top of it. Otherwise opening
            // the inventory would cover the only control that closes it again.
            var toggle = new Button(ToggleInventory) { name = InventoryButtonName, text = "Inventory" };
            toggle.style.position = Position.Absolute;
            toggle.style.left = 20;
            toggle.style.top = 20;
            toggle.style.width = 160;
            toggle.style.height = 44;
            root.Add(toggle);
        }

        private void ToggleInventory()
        {
            if (_inventoryPanel == null)
            {
                return;
            }

            _inventoryPanel.style.display = _inventoryPanel.style.display == DisplayStyle.None
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystem = new GameObject(
                "EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            eventSystem.transform.SetParent(null);
        }

        /// <summary>
        /// Ensures a perspective camera that can deliver 3D pointer events.
        /// </summary>
        /// <remarks>
        /// The <c>PhysicsRaycaster</c> is what lets a human click meshes. The driver does not
        /// need it — it raycasts itself — but a sample nobody can play by hand is a poor
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

            // Raised and pitched down, with no roll — a plain over-the-shoulder adventure
            // view. Roll is what skews a scene: every prop stands upright in the world, so
            // any tilt in the camera reads as the whole room leaning over.
            _camera.orthographic = false;
            _camera.transform.position = new Vector3(0f, 8f, -12f);
            _camera.transform.rotation = Quaternion.Euler(30f, 0f, 0f);

            if (_camera.GetComponent<PhysicsRaycaster>() == null)
            {
                _camera.gameObject.AddComponent<PhysicsRaycaster>();
            }
        }

        /// <summary>
        /// Lays the room out in world space: props upright on the ground, the wall standing
        /// between the camera and the key.
        /// </summary>
        /// <remarks>
        /// Plain world coordinates rather than camera-relative ones, so everything stands
        /// square to the ground and reads as a room. That does mean the occlusion depends on
        /// where the camera is — but it is not left to chance: <c>VaultOcclusionTests</c>
        /// fails the moment the wall stops covering the key, so reframing the shot without
        /// re-checking the geometry cannot pass unnoticed.
        /// </remarks>
        private void BuildRoom()
        {
            BuildHotspot(LeverHotspotName, "Lever", new Vector3(-5f, 1f, 2f),
                new Vector3(1f, 2f, 1f), new Color(0.7f, 0.7f, 0.75f), OnLeverClicked);

            // Further from the camera than the wall, and directly behind it.
            _key = BuildHotspot(KeyHotspotName, "Key", new Vector3(0f, 0.5f, 6f),
                new Vector3(1f, 1f, 1f), new Color(0.9f, 0.8f, 0.3f), OnKeyClicked);

            BuildHotspot(GateHotspotName, "Gate", new Vector3(5f, 1.5f, 2f),
                new Vector3(2f, 3f, 0.5f), new Color(0.6f, 0.4f, 0.2f), OnGateClicked);

            // Standing on the ground at z = 1. The camera's line of sight to the key passes
            // through roughly (0, 2.6, 1), which this covers; the lever and gate sit wide
            // enough to either side to stay clear of it.
            _wallClosedPosition = new Vector3(0f, 2f, 1f);

            // Retracts straight up and out of frame, rather than being switched off — a real
            // collider has to leave the line of sight for the raycast to change its mind.
            _wallOpenPosition = new Vector3(0f, 10f, 1f);

            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.name = WallName;
            _wall.transform.SetParent(transform, worldPositionStays: true);
            _wall.transform.localScale = new Vector3(4f, 4f, 0.5f);
            _wall.GetComponent<Renderer>().material.color = new Color(0.45f, 0.45f, 0.5f);

            _gateStateLabel = BuildLabel(GateStateLabelName, "Locked", new Vector3(5f, 3.6f, 2f));
        }

        private GameObject BuildHotspot(
            string name, string caption, Vector3 position, Vector3 scale, Color colour, Action onClick)
        {
            GameObject hotspot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hotspot.name = name;
            hotspot.transform.SetParent(transform, worldPositionStays: true);
            hotspot.transform.position = position;
            hotspot.transform.localScale = scale;
            hotspot.GetComponent<Renderer>().material.color = colour;

            hotspot.AddComponent<VaultHotspot>().Initialise(onClick);

            BuildLabel($"{name}_Label", caption, position + new Vector3(0f, scale.y * 0.5f + 0.6f, 0f));

            return hotspot;
        }

        private TMP_Text BuildLabel(string name, string text, Vector3 position)
        {
            var labelObject = new GameObject(name);
            labelObject.transform.SetParent(transform, worldPositionStays: true);
            labelObject.transform.position = position;

            // Text reads from the side its forward points AWAY from, so aligning forward with
            // the camera's forward is what faces it. Aiming forward at the camera mirrors it.
            labelObject.transform.rotation = Quaternion.LookRotation(_camera.transform.forward);

            var label = labelObject.AddComponent<TextMeshPro>();
            label.text = text;
            label.fontSize = 6f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;

            return label;
        }
    }
}
