using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Routaz.TestGoblin.Runtime.Driver;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Routaz.TestGoblin.PlayModeDemo
{
    /// <summary>
    /// <see cref="UiToolkitGameDriver"/> driving VisualElements inside a UIDocument.
    /// </summary>
    /// <remarks>
    /// UI Toolkit shares nothing with the other drivers — no GameObjects, no EventSystem, no
    /// colliders — so this suite covers the same guarantees from scratch: a click reaches a
    /// visible control, a hidden one is absent, a covered one is refused, and text is readable.
    /// </remarks>
    [Category("LiveScene")]
    public class UiToolkitGameDriverTests
    {
        private const float Timeout = 3f;
        private const int PollIntervalMs = 50;

        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
        private VisualElement _root;

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
            _root = null;
        }

        [UnityTest]
        public IEnumerator Tap_ActivatesAVisibleButton()
        {
            yield return BuildDocument();

            int clicks = 0;
            AddButton("Confirm", () => clicks++);

            yield return LayoutFrames();
            yield return RunAsync(TapAsync("Confirm"));

            Assert.AreEqual(1, clicks, "The button should have been activated exactly once.");
        }

        [UnityTest]
        public IEnumerator FindObject_TreatsAHiddenElementAsAbsent()
        {
            yield return BuildDocument();

            Button button = AddButton("Hidden", () => { });
            button.style.display = DisplayStyle.None;

            yield return LayoutFrames();

            var driver = new UiToolkitGameDriver(Timeout, PollIntervalMs);
            Task<GoblinObject> find = driver.FindObject("Hidden");

            while (!find.IsCompleted)
            {
                yield return null;
            }

            // display:none is how a UI Toolkit screen is dismissed, so it has to read as
            // gone — otherwise waiting for a panel to disappear could never succeed.
            Assert.IsNull(find.Result, "A display:none element must read as absent.");
        }

        [UnityTest]
        public IEnumerator Tap_RefusesADisabledElement()
        {
            yield return BuildDocument();

            int clicks = 0;
            Button button = AddButton("Locked", () => clicks++);
            button.SetEnabled(false);

            yield return LayoutFrames();

            Task tap = TapAsync("Locked");

            while (!tap.IsCompleted)
            {
                yield return null;
            }

            Assert.IsTrue(tap.IsFaulted, "A disabled control must not be tappable.");
            StringAssert.Contains("disabled", tap.Exception?.InnerException?.Message ?? string.Empty);
            Assert.AreEqual(0, clicks);
        }

        [UnityTest]
        public IEnumerator Tap_RefusesAnElementCoveredByAnother()
        {
            yield return BuildDocument();

            int clicks = 0;
            Button button = AddButton("Underneath", () => clicks++);

            // A full-panel overlay on top, which is how a modal dialog is normally built.
            var overlay = new VisualElement { name = "Overlay" };
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.top = 0;
            overlay.style.width = Length.Percent(100);
            overlay.style.height = Length.Percent(100);
            _root.Add(overlay);

            yield return LayoutFrames();

            Task tap = TapAsync("Underneath");

            while (!tap.IsCompleted)
            {
                yield return null;
            }

            Assert.IsTrue(tap.IsFaulted, "A control behind an overlay must not be tappable.");
            StringAssert.Contains("Overlay", tap.Exception?.InnerException?.Message ?? string.Empty,
                "The message must name what is in the way.");
            Assert.AreEqual(0, clicks);
        }

        [UnityTest]
        public IEnumerator GetText_ReadsAnElementsLabel()
        {
            yield return BuildDocument();

            var label = new Label("Door is open") { name = "StatusLabel" };
            _root.Add(label);

            yield return LayoutFrames();

            var driver = new UiToolkitGameDriver(Timeout, PollIntervalMs);
            Task<string> read = ReadAsync(driver, "StatusLabel");

            while (!read.IsCompleted)
            {
                yield return null;
            }

            Assert.AreEqual("Door is open", read.Result);
        }

        private static async Task<string> ReadAsync(UiToolkitGameDriver driver, string name)
        {
            GoblinObject element = await driver.WaitForObject(name);
            return await driver.GetText(element);
        }

        private static async Task TapAsync(string name)
        {
            var driver = new UiToolkitGameDriver(Timeout, PollIntervalMs);
            GoblinObject element = await driver.WaitForObject(name);
            await driver.Tap(element);
        }

        private IEnumerator BuildDocument()
        {
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _created.Add(panelSettings);

            var host = new GameObject("UiDocumentHost");
            _created.Add(host);

            UIDocument document = host.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;

            // The document builds its root on enable, not on AddComponent.
            yield return null;

            _root = document.rootVisualElement;
            Assert.IsNotNull(_root, "UIDocument produced no rootVisualElement.");
        }

        private Button AddButton(string name, Action onClick)
        {
            var button = new Button(onClick) { name = name, text = name };
            button.style.width = 200;
            button.style.height = 60;

            _root.Add(button);

            return button;
        }

        /// <summary>Layout and picking are meaningless until the panel has resolved styles.</summary>
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
