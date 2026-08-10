using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Routaz.TestGoblin.Runtime.Config
{
    public static class ConfigLoader
    {
       
        private const string EnvVarPrefix = "TESTGOBLIN_";
        public static TestRunConfig Load(string jsonFilePath)
        {
            TestRunConfig config = LoadFromJson(jsonFilePath);
            config = ApplyEnvironmentOverrides(config);
            Validate(config);
            return config;
        }

        private static TestRunConfig LoadFromJson(string jsonFilePath)
        {
            string json = File.ReadAllText(jsonFilePath);
            TestRunConfig config = JsonConvert.DeserializeObject<TestRunConfig>(json,new StringEnumConverter());
            if(config == null)
            {
                throw new InvalidOperationException($"Failed to deserialize TestRunConfig from JSON file: {jsonFilePath}");
            }
            return config;
        }

        private static TestRunConfig ApplyEnvironmentOverrides(TestRunConfig config)
        {
            return new TestRunConfig
            {
                ExecutionTarget = GetEnumOrDefault("EXECUTION_TARGET", config.ExecutionTarget),
                Platform = GetEnumOrDefault("PLATFORM", config.Platform),
                DeviceId = GetStringOrDefault("DEVICE_ID", config.DeviceId),
                BuildPath = GetStringOrDefault("BUILD_PATH", config.BuildPath),
                EnvironmentName = GetStringOrDefault("ENVIRONMENT_NAME", config.EnvironmentName),
                BackendBaseUrl = GetUriOrDefault("BACKEND_BASE_URL", config.BackendBaseUrl),
                InputBackend = GetEnumOrDefault("INPUT_BACKEND", config.InputBackend),
                DriverHost = GetStringOrDefault("DRIVER_HOST", config.DriverHost),
                DriverPort = GetIntOrDefault("DRIVER_PORT", config.DriverPort),
                DefaultTimeout = GetFloatOrDefault("DEFAULT_TIMEOUT", config.DefaultTimeout),
                DefaultPollInterval = GetIntOrDefault("DEFAULT_POLL_INTERVAL", config.DefaultPollInterval)
            };

        }

        private static void Validate(TestRunConfig config)
        {
            if (config.BackendBaseUrl == null)
            {
                throw new InvalidOperationException("BackendBaseUrl is missing in the configuration.");
            }

            // DriverHost is deliberately NOT required: the default in-process driver
            // has nothing to connect to. A driver that needs a host validates its own
            // configuration, because only it knows whether one is meaningful.
        }

        //HELPER METHODS
        private static string GetStringOrDefault(string envVarName, string defaultValue)
        {
            string value = Environment.GetEnvironmentVariable(EnvVarPrefix + envVarName);
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }

        private static TEnum GetEnumOrDefault<TEnum>(string envVarName, TEnum defaultValue) where TEnum : struct
        {
            string value = Environment.GetEnvironmentVariable(EnvVarPrefix + envVarName);
            var parsed = Enum.TryParse<TEnum>(value, ignoreCase: true, out TEnum result);
            return parsed ? result : defaultValue;
        }

        private static int GetIntOrDefault(string envVarName, int defaultValue)
        {
            string value = Environment.GetEnvironmentVariable(EnvVarPrefix + envVarName);
            return int.TryParse(value, out int result) ? result : defaultValue;
        }

        private static float GetFloatOrDefault(string envVarName, float defaultValue)
        {
            string value = Environment.GetEnvironmentVariable(EnvVarPrefix + envVarName);
            return float.TryParse(value, out float result) ? result : defaultValue;
        }

        private static Uri GetUriOrDefault(string envVarName, Uri defaultValue)
        {
            string value = Environment.GetEnvironmentVariable(EnvVarPrefix + envVarName);
            return string.IsNullOrEmpty(value) ? defaultValue : new Uri(value);
        }

    }
}