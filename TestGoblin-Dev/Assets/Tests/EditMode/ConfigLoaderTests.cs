using System;
using System.IO;
using NUnit.Framework;
using Routaz.TestGoblin.Runtime.Config;

namespace Routaz.TestGoblin.Tests
{
    public class ConfigLoaderTests
    {
        private const string DeviceIdEnvVar = "TESTGOBLIN_DEVICE_ID";

        private string _tempJsonPath;

        [SetUp]
        public void SetUp()
        {
            _tempJsonPath = Path.GetTempFileName();
        }

        [TearDown]
        public void TearDown()
        {
            File.Delete(_tempJsonPath);
            Environment.SetEnvironmentVariable(DeviceIdEnvVar, null);
        }

        [Test]
        public void Load_ValidJson_ReturnsPopulatedConfig()
        {
            const string json = @"{
                ""executionTarget"": ""StandalonePlayer"",
                ""platform"": ""Android"",
                ""deviceId"": ""device-123"",
                ""buildPath"": ""/builds/sugar-rush.apk"",
                ""environmentName"": ""staging"",
                ""backendBaseUrl"": ""http://staging.example.com"",
                ""driverHost"":""10.0.0.5"",
                ""driverPort"":13000,
                ""defaultTimeout"": 15.5,
                ""defaultPollInterval"": 250
            }";
            File.WriteAllText(_tempJsonPath, json);

            TestRunConfig config = ConfigLoader.Load(_tempJsonPath);

            Assert.AreEqual(ExecutionTarget.StandalonePlayer, config.ExecutionTarget);
            Assert.AreEqual(Platform.Android, config.Platform);
            Assert.AreEqual("device-123", config.DeviceId);
            Assert.AreEqual("/builds/sugar-rush.apk", config.BuildPath);
            Assert.AreEqual("staging", config.EnvironmentName);
            Assert.AreEqual(new Uri("http://staging.example.com"), config.BackendBaseUrl);
            Assert.AreEqual("10.0.0.5", config.DriverHost);
            Assert.AreEqual(13000, config.DriverPort);
            Assert.AreEqual(15.5f, config.DefaultTimeout);
            Assert.AreEqual(250, config.DefaultPollInterval);
        }

        [Test]
        public void Load_EnvVarOverridesJsonValue()
        {
            const string json = @"{
                ""executionTarget"": ""Editor"",
                ""platform"": ""Windows"",
                ""deviceId"": ""json-device-id"",
                ""backendBaseUrl"": ""http://localhost:5000"",
                ""driverHost"":""127.0.0.1"",
                ""driverPort"":13000
            }";
            File.WriteAllText(_tempJsonPath, json);
            Environment.SetEnvironmentVariable(DeviceIdEnvVar, "override-device-id");

            TestRunConfig config = ConfigLoader.Load(_tempJsonPath);

            Assert.AreEqual("override-device-id", config.DeviceId);
        }

        [Test]
        public void Load_MissingBackendBaseUrl_Throws()
        {
            const string json = @"{
                ""executionTarget"": ""Editor"",
                ""platform"": ""Windows"",
                ""driverHost"":""127.0.0.1"",
                ""driverPort"":13000
            }";
            File.WriteAllText(_tempJsonPath, json);

            Assert.Throws<InvalidOperationException>(() => ConfigLoader.Load(_tempJsonPath));
        }
    }
}
