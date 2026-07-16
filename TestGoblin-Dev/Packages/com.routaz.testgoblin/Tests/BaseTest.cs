using NUnit.Framework;
using NUnit.Framework.Interfaces;
using Routaz.TestGoblin.Runtime.Backend;
using Routaz.TestGoblin.Runtime.Config;
using Routaz.TestGoblin.Runtime.Driver;
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Routaz.TestGoblin.Tests
{
    /// <summary>
    /// Base class for all TestGoblin test fixtures. Owns the run lifecycle:
    /// config loading, driver creation/connection, per-test state reset,
    /// artifact capture on failure, and disconnection.
    /// </summary>
    public abstract class BaseTest
    {
        protected TestRunConfig Config { get; private set; }
        protected IGameDriver Driver { get; private set; }
        protected IBackendTestApi Backend { get; private set; }

        [OneTimeSetUp]
        public async Task FixtureSetUp()
        {
            var configPath = Environment.GetEnvironmentVariable("TESTGOBLIN_CONFIG_PATH") ?? "Packages/com.routaz.testgoblin/config.local.json";
            Config = ConfigLoader.Load(configPath);
            Driver = CreateDriver(Config);
            await Driver.Connect(Config.DeviceId, Config.AltTesterHost, Config.AltTesterPort).ConfigureAwait(false);
            //TODO: Guard against connection failure and throw a more informative exception if it fails.
            Backend = CreateBackend(Config);

        }

        [SetUp]
        public async Task TestSetUp()
        {
            await Backend.Reset();
        }

        [TearDown]
        public void TestTearDown()
        {
            var result = TestContext.CurrentContext.Result;
            if (result.Outcome.Status != TestStatus.Failed)
            {
                return;
            }
            
            var testName = TestContext.CurrentContext.Test.Name;
            var safeTestName = MakeFileNameSafe(testName);

            Debug.LogError(
                $"Test failed: {testName}\n" +
                $"Message: {result.Message}\n" +
                $"Stack trace:\n{result.StackTrace}");

            CaptureScreenshot(safeTestName);
            CaptureLogs(safeTestName);

        }

        [OneTimeTearDown]
        public async Task FixtureTearDown()
        {
            await Driver.Disconnect().ConfigureAwait(false);
        }

        private static IGameDriver CreateDriver(TestRunConfig config)
        {
            switch (config.ExecutionTarget)
            {
                case ExecutionTarget.Editor:
                    // In-memory fake: proof level 1, no game or SDK required.
                    // TODO: once the AltTester SDK is installed, Editor should also
                    // connect AltTesterGameDriver to the editor-hosted player.
                    return new FakeGameDriver(config.DefaultTimeout, config.DefaultPollInterval);

                case ExecutionTarget.StandalonePlayer:
                case ExecutionTarget.Device:
                    return new AltTesterGameDriver(config);

                default:
                    throw new ArgumentOutOfRangeException(nameof(config.ExecutionTarget),
                        $"No driver mapping for execution target '{config.ExecutionTarget}'.");
            }
        }

        private static IBackendTestApi CreateBackend(TestRunConfig config)
        {
            switch(config.ExecutionTarget)
            {
                case ExecutionTarget.Editor:
                    return new FakeBackendTestApi();

                case ExecutionTarget.StandalonePlayer:
                case ExecutionTarget.Device:
                    // TODO: real HTTP-backed impl using config.BackendBaseUrl.
                    throw new NotImplementedException("Backend test API not implemented for AltTester driver yet.");

                default:
                    throw new ArgumentOutOfRangeException(nameof(config.ExecutionTarget),
                        $"No backend mapping for execution target '{config.ExecutionTarget}'.");
            }
        }

        private static void CaptureScreenshot(string testName)
        {
            // Stub: replace with the real screenshot implementation.
            Debug.Log($"[Artifact] Capture screenshot for {testName}");

            /*
            var directory = Path.Combine(
                Application.persistentDataPath,
                "TestArtifacts");

            Directory.CreateDirectory(directory);

            var path = Path.Combine(
                directory,
                $"{testName}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.png");

            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"Screenshot requested: {path}");
            */
        }

        private static void CaptureLogs(string testName)
        {
            // Stub: replace with your buffered-log implementation.
            Debug.Log($"[Artifact] Capture logs for {testName}");
        }

        private static string MakeFileNameSafe(string name)
        {
            var safe = name;
            
            foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
            {
                safe = safe.Replace(invalidCharacter, '_');
            }

            return safe;
        }




    }
}
