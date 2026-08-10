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
    /// <summary>
    /// <see cref="Physics3DGameDriver"/> driving meshes with colliders.
    /// </summary>
    /// <remarks>
    /// The occlusion case is the one that matters. In 2D, "in front of" is a sorting-order
    /// argument; in 3D it is literal geometry, and a wall between the camera and a door is a
    /// bug a player hits immediately and a naive driver never notices.
    /// </remarks>
    [Category("LiveScene")]
    public class Physics3DGameDriverTests
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
        public IEnumerator Tap_ReachesAnObjectInTheOpen()
        {
            BuildCamera();
            ClickRecorder recorder = BuildCube("LonelyCube", new Vector3(0f, 0f, 0f));

            yield return null;
            yield return RunAsync(TapAsync("LonelyCube"));

            Assert.AreEqual(1, recorder.Clicks);
        }

        [UnityTest]
        public IEnumerator Tap_RefusesAnObjectBehindAWall()
        {
            BuildCamera();

            // The wall is nearer the camera, directly on the line of sight.
            ClickRecorder hidden = BuildCube("HiddenCube", new Vector3(0f, 0f, 5f));
            BuildCube("Wall", new Vector3(0f, 0f, 2f));

            yield return null;

            Task tap = TapAsync("HiddenCube");

            while (!tap.IsCompleted)
            {
                yield return null;
            }

            Assert.IsTrue(tap.IsFaulted, "An object behind geometry must not be clickable.");
            StringAssert.Contains("Wall", tap.Exception?.InnerException?.Message ?? string.Empty,
                "The message must name what is in the way.");

            Assert.AreEqual(0, hidden.Clicks);
        }

        [UnityTest]
        public IEnumerator Tap_RefusesAnObjectBehindTheCamera()
        {
            BuildCamera();

            // Negative Z is behind a camera looking down +Z. WorldToScreenPoint would return
            // plausible-looking but meaningless coordinates, so this must be caught explicitly.
            BuildCube("BehindCube", new Vector3(0f, 0f, -20f));

            yield return null;

            Task tap = TapAsync("BehindCube");

            while (!tap.IsCompleted)
            {
                yield return null;
            }

            Assert.IsTrue(tap.IsFaulted);
            StringAssert.Contains("behind the camera", tap.Exception?.InnerException?.Message ?? string.Empty);
        }

        [UnityTest]
        public IEnumerator Tap_AimsAtTheRenderersCentre_NotThePivot()
        {
            BuildCamera();

            // Pivot at the floor, mesh raised well above it — a common shape for character
            // and prop models. Aiming at the pivot would miss the visible object entirely.
            var root = new GameObject("OffsetPivot");
            root.transform.position = new Vector3(0f, -6f, 6f);
            _created.Add(root);

            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(root.transform, worldPositionStays: false);
            cube.transform.localPosition = new Vector3(0f, 6f, 0f);
            cube.transform.localScale = Vector3.one * 2f;

            ClickRecorder recorder = root.AddComponent<ClickRecorder>();

            yield return null;
            yield return RunAsync(TapAsync("OffsetPivot"));

            Assert.AreEqual(1, recorder.Clicks,
                "Aiming should use the mesh the player can see, not the transform origin.");
        }

        private static async Task TapAsync(string name)
        {
            var driver = new Physics3DGameDriver(Timeout, PollIntervalMs);
            GoblinObject target = await driver.WaitForObject(name);
            await driver.Tap(target);
        }

        private void BuildCamera()
        {
            if (EventSystem.current == null)
            {
                _created.Add(new GameObject("EventSystem", typeof(EventSystem)));
            }

            var cameraObject = new GameObject("TestCamera", typeof(Camera));
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.transform.rotation = Quaternion.identity;
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
