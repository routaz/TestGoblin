using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DemoGame.Merge
{
    /// <summary>
    /// Builds the demo app — login, loading, and the merge game — and keeps the game screen
    /// in step with a <see cref="MergeBoard"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The whole interface is constructed at runtime rather than authored in a scene. A
    /// <c>.unity</c> file is YAML that nobody can review and that merges badly; a screen
    /// built in <c>Awake</c> is code you can read, diff and reason about.
    /// </para>
    /// <para>
    /// The loading step is deliberately not instant. A transition that takes real time is
    /// the only way to exercise polling waits against a live game: every other state change
    /// here happens within a single frame, so a driver could "pass" without ever waiting
    /// for anything.
    /// </para>
    /// <para>
    /// Object names here are the locators tests address the app by, so renaming one is a
    /// breaking change for every Screen and scenario that uses it.
    /// </para>
    /// </remarks>
    public sealed class MergeGameView : MonoBehaviour
    {
        public const string LoginScreenName = "LoginScreen";
        public const string LoginButtonName = "LoginButton";
        public const string LoadingScreenName = "LoadingScreen";
        public const string LoadingBarName = "LoadingBar";
        public const string LoadingLabelName = "LoadingLabel";
        public const string GameScreenName = "GameScreen";
        public const string SpawnButtonName = "SpawnButton";
        public const string FillEnergyButtonName = "FillEnergyButton";
        public const string LevelUpButtonName = "LevelUpButton";
        public const string EnergyLabelName = "EnergyLabel";
        public const string ExperienceLabelName = "ExperienceLabel";
        public const string LevelLabelName = "LevelLabel";
        public const string CellNamePrefix = "Cell_";

        /// <summary>How long the fake load takes. Long enough to need a real wait.</summary>
        public const float LoadingSeconds = 1.5f;

        private readonly MergeCell[] _cells = new MergeCell[MergeBoard.CellCount];

        private MergeBoard _board;
        private GameObject _loginScreen;
        private GameObject _loadingScreen;
        private GameObject _gameScreen;
        private GameObject _fillEnergyButton;
        private GameObject _levelUpButton;
        private RectTransform _loadingBar;
        private TMP_Text _loadingLabel;
        private Button _spawnButton;
        private TMP_Text _energyLabel;
        private TMP_Text _experienceLabel;
        private TMP_Text _levelLabel;

        /// <summary>The board being displayed. Tests read state through the UI, not this.</summary>
        public MergeBoard Board => _board;

        private void Awake()
        {
            _board = new MergeBoard();

            EnsureEventSystem();
            Transform canvas = CreateCanvas();

            _loginScreen = CreateLoginScreen(canvas);
            _loadingScreen = CreateLoadingScreen(canvas);
            _gameScreen = CreateGameScreen(canvas);

            _loginScreen.SetActive(true);
            _loadingScreen.SetActive(false);
            _gameScreen.SetActive(false);

            Refresh();
        }

        /// <summary>Spawns one item, if the board allows it.</summary>
        public void Spawn()
        {
            if (!_board.CanSpawn)
            {
                return;
            }

            _board.Spawn();
            Refresh();
        }

        /// <summary>Merges two cells if the pair is valid; ignores the drop otherwise.</summary>
        /// <remarks>
        /// An invalid drop is a normal thing for a player to do, so it is silently ignored
        /// rather than thrown. A test that expects a merge asserts on the resulting state.
        /// </remarks>
        public void TryMerge(int from, int to)
        {
            if (!_board.CanMerge(from, to))
            {
                return;
            }

            _board.Merge(from, to);
            Refresh();
        }

        /// <summary>Refills energy when the board allows it.</summary>
        public void FillEnergy()
        {
            if (!_board.CanFillEnergy)
            {
                return;
            }

            _board.FillEnergy();
            Refresh();
        }

        /// <summary>Levels up when enough experience has been earned.</summary>
        public void LevelUp()
        {
            if (!_board.CanLevelUp)
            {
                return;
            }

            _board.LevelUp();
            Refresh();
        }

        /// <summary>Begins the fake load that leads to the game screen.</summary>
        private void OnLogin()
        {
            _loginScreen.SetActive(false);
            _loadingScreen.SetActive(true);
            StartCoroutine(LoadThenShowGame());
        }

        /// <summary>
        /// Fills the progress bar over <see cref="LoadingSeconds"/>, then reveals the game.
        /// </summary>
        /// <remarks>
        /// Driven by <c>Time.deltaTime</c> across frames rather than completing immediately,
        /// so a test genuinely has to wait for the screen to change instead of finding it
        /// already changed.
        /// </remarks>
        private IEnumerator LoadThenShowGame()
        {
            float elapsed = 0f;

            while (elapsed < LoadingSeconds)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / LoadingSeconds);

                _loadingBar.anchorMax = new Vector2(progress, 1f);
                _loadingLabel.text = $"{Mathf.RoundToInt(progress * 100f)}%";

                yield return null;
            }

            _loadingScreen.SetActive(false);
            _gameScreen.SetActive(true);
            Refresh();
        }

        /// <summary>
        /// Pushes board state into the UI.
        /// </summary>
        /// <remarks>
        /// The conditional buttons are <em>deactivated</em>, not merely disabled, so a test
        /// asking for them finds nothing at all — which is what a player sees, and what
        /// makes "the level-up button should not be there yet" an assertable claim.
        /// </remarks>
        private void Refresh()
        {
            _levelLabel.text = _board.Level.ToString();
            _experienceLabel.text = _board.Experience.ToString();
            _energyLabel.text = _board.Energy.ToString();

            _spawnButton.interactable = _board.CanSpawn;
            _fillEnergyButton.SetActive(_board.CanFillEnergy);
            _levelUpButton.SetActive(_board.CanLevelUp);

            for (int index = 0; index < _cells.Length; index++)
            {
                _cells[index].Show(_board.TierAt(index));
            }
        }

        /// <summary>
        /// Creates an EventSystem if the scene has none.
        /// </summary>
        /// <remarks>
        /// Uses <c>InputSystemUIInputModule</c>, not <c>StandaloneInputModule</c>: this
        /// project runs the new Input System, and the legacy module throws the moment it
        /// reads <c>UnityEngine.Input.mousePosition</c>.
        /// <para>
        /// The module exists so a human can play the game. The driver needs none of it —
        /// it dispatches through ExecuteEvents and raycasts through EventSystem directly —
        /// but a game only drivable by a test is not much of a demonstration.
        /// </para>
        /// </remarks>
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystem = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            eventSystem.transform.SetParent(null);
        }

        private static Transform CreateCanvas()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            return canvasObject.transform;
        }

        private GameObject CreateLoginScreen(Transform canvas)
        {
            GameObject screen = CreatePanel(canvas, LoginScreenName);

            CreateLabel(screen.transform, "LoginTitle", new Vector2(0f, -60f)).text = "Merge Demo";
            CreateButton(screen.transform, LoginButtonName, "Log in", new Vector2(0f, -120f), OnLogin);

            return screen;
        }

        private GameObject CreateLoadingScreen(Transform canvas)
        {
            GameObject screen = CreatePanel(canvas, LoadingScreenName);

            _loadingLabel = CreateLabel(screen.transform, LoadingLabelName, new Vector2(0f, -60f));
            _loadingLabel.text = "0%";

            var trackObject = new GameObject("LoadingTrack", typeof(RectTransform), typeof(Image));
            trackObject.transform.SetParent(screen.transform, worldPositionStays: false);

            var trackRect = (RectTransform)trackObject.transform;
            trackRect.anchorMin = new Vector2(0.5f, 1f);
            trackRect.anchorMax = new Vector2(0.5f, 1f);
            trackRect.pivot = new Vector2(0.5f, 1f);
            trackRect.anchoredPosition = new Vector2(0f, -110f);
            trackRect.sizeDelta = new Vector2(260f, 20f);
            trackObject.GetComponent<Image>().color = new Color(0.8f, 0.8f, 0.8f);

            var barObject = new GameObject(LoadingBarName, typeof(RectTransform), typeof(Image));
            barObject.transform.SetParent(trackObject.transform, worldPositionStays: false);

            _loadingBar = (RectTransform)barObject.transform;
            _loadingBar.anchorMin = Vector2.zero;
            _loadingBar.anchorMax = new Vector2(0f, 1f);
            _loadingBar.pivot = new Vector2(0f, 0.5f);
            _loadingBar.offsetMin = Vector2.zero;
            _loadingBar.offsetMax = Vector2.zero;
            barObject.GetComponent<Image>().color = new Color(0.2f, 0.6f, 0.4f);

            return screen;
        }

        private GameObject CreateGameScreen(Transform canvas)
        {
            GameObject screen = CreatePanel(canvas, GameScreenName);
            Transform parent = screen.transform;

            _levelLabel = CreateLabel(parent, LevelLabelName, new Vector2(0f, -20f));
            _experienceLabel = CreateLabel(parent, ExperienceLabelName, new Vector2(0f, -50f));
            _energyLabel = CreateLabel(parent, EnergyLabelName, new Vector2(0f, -80f));

            _spawnButton = CreateButton(parent, SpawnButtonName, "Spawn", new Vector2(-120f, -120f), Spawn);
            _fillEnergyButton = CreateButton(parent, FillEnergyButtonName, "Fill energy", new Vector2(0f, -120f), FillEnergy).gameObject;
            _levelUpButton = CreateButton(parent, LevelUpButtonName, "Level up", new Vector2(120f, -120f), LevelUp).gameObject;

            CreateGrid(parent);

            return screen;
        }

        /// <summary>A full-screen, non-blocking container so a whole screen can be toggled by name.</summary>
        private static GameObject CreatePanel(Transform parent, string name)
        {
            var panel = new GameObject(name, typeof(RectTransform));
            panel.transform.SetParent(parent, worldPositionStays: false);

            var rect = (RectTransform)panel.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return panel;
        }

        private static TMP_Text CreateLabel(Transform parent, string name, Vector2 position)
        {
            var labelObject = new GameObject(name, typeof(RectTransform));
            labelObject.transform.SetParent(parent, worldPositionStays: false);

            var rect = (RectTransform)labelObject.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(300f, 32f);

            TMP_Text text = labelObject.AddComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 18f;

            return text;
        }

        private static Button CreateButton(
            Transform parent, string name, string caption, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, worldPositionStays: false);

            var rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(112f, 34f);

            buttonObject.GetComponent<Image>().color = new Color(0.85f, 0.85f, 0.85f);

            var button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(onClick);

            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, worldPositionStays: false);

            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TMP_Text text = labelObject.AddComponent<TextMeshProUGUI>();
            text.text = caption;
            text.color = Color.black;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 14f;
            text.raycastTarget = false;   // the button itself must be the hit, not its label

            return button;
        }

        private void CreateGrid(Transform parent)
        {
            var gridObject = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridObject.transform.SetParent(parent, worldPositionStays: false);

            var rect = (RectTransform)gridObject.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -165f);
            rect.sizeDelta = new Vector2(MergeBoard.Size * 52f, MergeBoard.Size * 52f);

            var layout = gridObject.GetComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(48f, 48f);
            layout.spacing = new Vector2(4f, 4f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = MergeBoard.Size;

            for (int index = 0; index < MergeBoard.CellCount; index++)
            {
                _cells[index] = CreateCell(gridObject.transform, index);
            }
        }

        private MergeCell CreateCell(Transform parent, int index)
        {
            var cellObject = new GameObject(CellNamePrefix + index, typeof(RectTransform), typeof(Image), typeof(MergeCell));
            cellObject.transform.SetParent(parent, worldPositionStays: false);
            cellObject.GetComponent<Image>().color = new Color(0.92f, 0.92f, 0.92f);

            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(cellObject.transform, worldPositionStays: false);

            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TMP_Text text = labelObject.AddComponent<TextMeshProUGUI>();
            text.color = Color.black;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 20f;
            text.raycastTarget = false;   // drops must land on the cell, not its number

            var cell = cellObject.GetComponent<MergeCell>();
            cell.Initialise(this, index, text);

            return cell;
        }
    }
}
