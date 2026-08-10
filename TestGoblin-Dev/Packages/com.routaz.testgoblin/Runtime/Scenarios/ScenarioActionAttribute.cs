using System;

namespace Routaz.TestGoblin.Runtime.Scenarios
{
    /// <summary>
    /// Marks a method as a keyword that scenario authors can use by name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An explicit attribute rather than reflection over method names, for two reasons.
    /// The name here is a <em>contract with saved scenario files</em>, so renaming the C#
    /// method cannot silently break every scenario that used it. And exposure is opt-in,
    /// so a private helper never leaks into the vocabulary just by existing.
    /// </para>
    /// <para>
    /// Keywords belong at intent level — <c>CompleteTutorial</c>, <c>ClaimDailyReward</c> —
    /// not at input level. A vocabulary of <c>Tap</c> and <c>Drag</c> turns scenario JSON
    /// into a worse programming language than the one it replaced.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class ScenarioActionAttribute : Attribute
    {
        /// <summary>
        /// The keyword as scenario files spell it. Must be unique across the vocabulary.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Optional one-line help, surfaced in the scenario builder.
        /// </summary>
        /// <remarks>
        /// Carried on the attribute rather than read from XML doc comments, because those
        /// are not available through reflection without shipping the generated XML file.
        /// </remarks>
        public string Description { get; set; } = "";

        /// <param name="name">The keyword name. Changing it breaks existing scenarios.</param>
        public ScenarioActionAttribute(string name)
        {
            Name = name;
        }
    }
}
