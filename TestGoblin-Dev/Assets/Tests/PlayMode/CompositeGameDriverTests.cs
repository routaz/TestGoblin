using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Routaz.TestGoblin.Runtime.Driver;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Routaz.TestGoblin.PlayModeDemo
{
    /// <summary>
    /// <see cref="CompositeGameDriver"/> driving a UI Toolkit HUD over a 3D world — the shape
    /// a game with an inventory actually has.
    /// </summary>
    /// <remarks>
    /// The covered-object case is the reason this class exists. Neither driver alone can
    /// detect it: the 3D raycast cannot see a UI panel, and the UI driver knows nothing about
    /// the world. Only something that owns the layering can refuse the click.
    /// </remarks>
    [Category("LiveScene")]
    public class CompositeGameDriverTests
    {
        private const float Timeout = 3f;
        private const int PollIntervalMs = 50;

        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
        private VisualElement _uiRoot;

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object created in _created)
            {
                if (created != null)
                {
                    UnityEngine.Object.Destroy(created);
                }
            }

            _created.Clear();
            _uiRoot = null;
        }

        [UnityTest]
        public IEnumerator Tap_ReachesAWorldObjectWhenNoInterfaceIsOverIt()
        {
            BuildCamera();
            yield return BuildUi();

            ClickRecorder door = BuildCube("Door", new Vector3(0f, 0f, 0f));

            yield return LayoutFrames();
            yield return RunAsync(TapAsync("Door"));

            Assert.AreEqual(1, door.Clicks);
        }

        [UnityTest]
        public IEnumerator Tap_RefusesAWorldObjectCoveredByTheInterface()
        {
            BuildCamera();
            yield return BuildUi();

            ClickRecorder door = BuildCube("Door", new Vector3(0f, 0f, 0f));

            // An inventory panel covering the screen. Sized in pixels rather than percent:
            // percentages resolve against the parent, and a headless panel with no theme
            // style sheet does not give the root a size — so a "100%" overlay would cover
            // nothing and the test would silently prove the opposite of what it claims.
            var inventory = new VisualElement { name = "InventoryPanel" };
            inventory.style.position = Position.Absolute;
            inventory.style.left = 0;
            inventory.style.top = 0;
            inventory.style.width = 4000;
            inventory.style.height = 4000;
            _uiRoot.Add(inventory);

            yield return LayoutFrames();

            Task tap = TapAsync("Door");

            while (!tap.IsCompleted)
            {
                yield return null;
            }

            Assert.IsTrue(tap.IsFaulted,
                "A world object behind an open UI panel must not be clickable — neither driver " +
                "alone can tell, which is the point of the composite.");

            StringAssert.Contains("InventoryPanel", tap.Exception?.InnerException?.Message ?? string.Empty,
                "The message must name the interface element that is in the way.");

            Assert.AreEqual(0, door.Clicks);
        }

        [UnityTest]
        public IEnumerator Tap_ReachesAnInterfaceElement()
        {
            BuildCamera();
            yield return BuildUi();

            int clicks = 0;
            var button = new Button(() => clicks++) { name = "CloseButton", text = "Close" };
            button.style.width = 120;
            button.style.height = 40;
            _uiRoot.Add(button);

            yield return LayoutFrames();
            yield return RunAsync(TapAsync("CloseButton"));

            Assert.AreEqual(1, clicks, "The topmost layer needs no occlusion check.");
        }

        [UnityTest]
        public IEnumerator Drag_AcrossLayers_IsRefusedRatherThanFaked()
        {
            BuildCamera();
            yield return BuildUi();

            BuildCube("Door", new Vector3(0f, 0f, 0f));

            var item = new VisualElement { name = "KeyItem" };
            item.style.width = 40;
            item.style.height = 40;
            _uiRoot.Add(item);

            yield return LayoutFrames();

            Task drag = DragAsync("KeyItem", "Door");

            while (!drag.IsCompleted)
            {
                yield return null;
            }

            Assert.IsTrue(drag.IsFaulted, "A drag cannot begin in one input system and end in another.");
            StringAssert.Contains("different layers", drag.Exception?.InnerException?.Message ?? string.Empty);
        }

        private static CompositeGameDriver BuildDriver()
        {
            // Topmost first: interface over world, which is draw order.
            return new CompositeGameDriver(
                Timeout, PollIntervalMs,
                new UiToolkitGameDriver(Timeout, PollIntervalMs),
                new Physics3DGameDriver(Timeout, PollIntervalMs));
        }

        private static async Task TapAsync(string name)
        {
            CompositeGameDriver driver = BuildDriver();
            GoblinObject target = await driver.WaitForObject(name);
            await driver.Tap(target);
        }

        private static async Task DragAsync(string from, string to)
        {
            CompositeGameDriver driver = BuildDriver();
            GoblinObject source = await driver.WaitForObject(from);
            GoblinObject destination = await driver.WaitForObject(to);
            await driver.Drag(source, destination);
        }

        private IEnumerator BuildUi()
        {
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _created.Add(panelSettings);

            var host = new GameObject("UiDocumentHost");
            _created.Add(host);

            UIDocument document = host.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;

            yield return null;

            _uiRoot = document.rootVisualElement;
            Assert.IsNotNull(_uiRoot);
        }

        private void BuildCamera()
        {
            if (EventSystem.current == null)
            {
                _created.Add(new GameObject("EventSystem", typeof(EventSystem)));
            }

            var cameraObject = new GameObject("TestCamera", typeof(Camera));
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.tag = "MainCamera";

            _created.Add(cameraObject);
        }

        private ClickRecorder BuildCube(string name, Vector3 position)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.position = position;
            cube.transform.localScale = Vector3.one * 2f;

            _created.Add(cube);

            return cube.AddComponent<ClickRecorder>();
        }

        private static IEnumerator LayoutFrames()
        {
            yield return null;
            yield return null;
        }

        private static IEnumerator RunAsync(Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                throw task.Exception?.InnerException ?? task.Exception;
            }
        }
    }
}
