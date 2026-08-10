using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Routaz.TestGoblin.Runtime.Driver;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Routaz.TestGoblin.PlayModeDemo
{
    /// <summary>Records pointer clicks so a test can assert one actually arrived.</summary>
    public sealed class ClickRecorder : MonoBehaviour, IPointerClickHandler
    {
        public int Clicks { get; private set; }

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicks++;
        }
    }

    /// <summary>
    /// <see cref="PhysicsGameDriver"/> driving sprites with colliders — the shape a
    /// point-and-click game takes, where uGUI raycasting does not apply.
    /// </summary>
    /// <remarks>
    /// The scene is built in code rather than authored, so the test states exactly what it
    /// depends on: two sprites, one camera, and a sorting order that decides which is on top.
    /// </remarks>
    [Category("LiveScene")]
    public class PhysicsGameDriverTests
    {
        private const float Timeout = 3f;
        private const int PollIntervalMs = 50;

        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject created in _created)
            {
                if (created != null)
                {
                    UnityEngine.Object.Destroy(created);
                }
            }

            _created.Clear();
        }

        [UnityTest]
        public IEnumerator Tap_ReachesASpriteThatIsVisible()
        {
            BuildCamera();
            ClickRecorder recorder = BuildClickable("LonelySprite", new Vector3(0f, 0f, 0f), sortingOrder: 0);

            yield return null;   // let the objects register with physics

            yield return RunAsync(TapAsync("LonelySprite"));

            Assert.AreEqual(1, recorder.Clicks, "The sprite should have received exactly one click.");
        }

        [UnityTest]
        public IEnumerator Tap_RefusesASpriteCoveredByAnother()
        {
            BuildCamera();

            // Same position, and the cover sorts above — exactly the case distance-based
            // ordering would get wrong, since both colliders sit at identical depth.
            ClickRecorder hidden = BuildClickable("HiddenSprite", Vector3.zero, sortingOrder: 0);
            BuildClickable("CoveringSprite", Vector3.zero, sortingOrder: 5);

            yield return null;

            Task tap = TapAsync("HiddenSprite");

            while (!tap.IsCompleted)
            {
                yield return null;
            }

            Assert.IsTrue(tap.IsFaulted, "Tapping a covered sprite must fail, not silently succeed.");

            Exception failure = tap.Exception?.InnerException;
            Assert.IsInstanceOf<InvalidOperationException>(failure);
            StringAssert.Contains("CoveringSprite", failure.Message,
                "The message must name what is in the way.");

            Assert.AreEqual(0, hidden.Clicks, "A covered sprite must not receive the click.");
        }

        [UnityTest]
        public IEnumerator Tap_RefusesAnObjectWithNoCollider()
        {
            BuildCamera();

            var bare = new GameObject("NoCollider");
            _created.Add(bare);

            yield return null;

            Task tap = TapAsync("NoCollider");

            while (!tap.IsCompleted)
            {
                yield return null;
            }

            Assert.IsTrue(tap.IsFaulted, "An object physics cannot hit is not tappable.");
            StringAssert.Contains("Collider2D", tap.Exception?.InnerException?.Message ?? string.Empty);
        }

        [UnityTest]
        public IEnumerator FindObject_ReportsColliderStateAsEnabled()
        {
            BuildCamera();
            ClickRecorder recorder = BuildClickable("Toggleable", Vector3.zero, sortingOrder: 0);
            recorder.GetComponent<Collider2D>().enabled = false;

            yield return null;

            var driver = new PhysicsGameDriver(Timeout, PollIntervalMs);
            Task<GoblinObject> find = driver.FindObject("Toggleable");

            while (!find.IsCompleted)
            {
                yield return null;
            }

            Assert.IsNotNull(find.Result, "The object is still in the scene.");
            Assert.IsFalse(find.Result.Enabled, "A disabled collider means the player cannot act on it.");
        }

        private static async Task TapAsync(string name)
        {
            var driver = new PhysicsGameDriver(Timeout, PollIntervalMs);
            GoblinObject target = await driver.WaitForObject(name);
            await driver.Tap(target);
        }

        private void BuildCamera()
        {
            if (EventSystem.current == null)
            {
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
                _created.Add(eventSystem);
            }

            var cameraObject = new GameObject("TestCamera", typeof(Camera));
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.tag = "MainCamera";

            _created.Add(cameraObject);
        }

        private ClickRecorder BuildClickable(string name, Vector3 position, int sortingOrder)
        {
            var spriteObject = new GameObject(name);
            spriteObject.transform.position = position;

            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateSprite();
            renderer.sortingOrder = sortingOrder;

            var collider = spriteObject.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;

            _created.Add(spriteObject);

            return spriteObject.AddComponent<ClickRecorder>();
        }

        private static Sprite CreateSprite()
        {
            var texture = new Texture2D(4, 4);
            return Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), pixelsPerUnit: 4f);
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
