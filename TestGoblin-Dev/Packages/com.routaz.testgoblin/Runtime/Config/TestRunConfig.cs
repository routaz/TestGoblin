using System;

namespace Routaz.TestGoblin.Runtime.Config
{
    public sealed class TestRunConfig
    {
        public ExecutionTarget ExecutionTarget { get; init; }
        public Platform Platform { get; init; }
        public string DeviceId { get; init; } = "";
        public string BuildPath { get; init; } = "";
        public string EnvironmentName { get; init; } = "";
        public Uri BackendBaseUrl { get; init; }
        public string AltTesterHost { get; init; } = "";
        public int AltTesterPort { get; init; }
        public float DefaultTimeout { get; init; }
        public int DefaultPollInterval { get; init; }
    }

    public enum ExecutionTarget
    {
        Editor,
        StandalonePlayer,
        Device
    }

    public enum Platform
    {
        Windows,
        MacOS,
        Linux,
        Android,
        iOS
    }
}




