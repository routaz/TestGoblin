using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Routaz.TestGoblin.Runtime.Driver;

namespace Routaz.TestGoblin.Runtime.Scenarios
{
    /// <summary>
    /// Runs a <see cref="Scenario"/> by resolving each step against the keyword vocabulary.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every step is bound — resolved and its arguments converted — before any step executes.
    /// A typo in the last step of a scenario should not be discovered ninety seconds into a
    /// run that has already changed game state; it should fail immediately, having done nothing.
    /// </para>
    /// <para>
    /// Keywords are instance methods, so the executor constructs the declaring type. The
    /// convention is a public constructor taking a single <see cref="IGameDriver"/> — which is
    /// what <c>GameFlows</c> and any per-title flows class already look like.
    /// </para>
    /// </remarks>
    public sealed class ScenarioExecutor
    {
        private readonly KeywordRegistry _registry;
        private readonly IGameDriver _driver;

        public ScenarioExecutor(KeywordRegistry registry, IGameDriver driver)
        {
            _registry = registry;
            _driver = driver;
        }

        /// <summary>
        /// Checks that every step resolves and every argument converts, without running anything.
        /// </summary>
        /// <exception cref="InvalidOperationException">The scenario is not runnable.</exception>
        public void Validate(Scenario scenario)
        {
            Bind(scenario);
        }

        /// <summary>
        /// Binds and then runs every step in order.
        /// </summary>
        /// <exception cref="InvalidOperationException">The scenario is not runnable.</exception>
        public async Task Execute(Scenario scenario)
        {
            List<BoundStep> steps = Bind(scenario);

            // One instance per run, not per step: a per-title flows class may legitimately
            // carry state across the steps of a single scenario.
            var instances = new Dictionary<Type, object>();

            foreach (BoundStep step in steps)
            {
                object instance = GetInstance(instances, step.Keyword.DeclaringType);

                object result;

                try
                {
                    result = step.Keyword.Method.Invoke(instance, step.Arguments);
                }
                catch (TargetInvocationException exception) when (exception.InnerException != null)
                {
                    // Surface the real failure, and say which step produced it — otherwise a
                    // scenario failure reads as a stack trace with no scenario in it.
                    throw new ScenarioStepException(scenario, step.Number, step.Keyword.Name, exception.InnerException);
                }

                if (result is Task task)
                {
                    try
                    {
                        await task;
                    }
                    catch (Exception exception)
                    {
                        throw new ScenarioStepException(scenario, step.Number, step.Keyword.Name, exception);
                    }
                }
            }
        }

        private List<BoundStep> Bind(Scenario scenario)
        {
            var bound = new List<BoundStep>(scenario.Steps.Count);

            for (int index = 0; index < scenario.Steps.Count; index++)
            {
                ScenarioStep step = scenario.Steps[index];
                int stepNumber = index + 1;

                if (string.IsNullOrWhiteSpace(step.Action))
                {
                    throw Malformed(scenario, stepNumber, "has no \"action\".");
                }

                Keyword keyword;

                try
                {
                    keyword = _registry.Resolve(step.Action);
                }
                catch (InvalidOperationException exception)
                {
                    throw Malformed(scenario, stepNumber, exception.Message);
                }

                bound.Add(new BoundStep
                {
                    Number = stepNumber,
                    Keyword = keyword,
                    Arguments = BindArguments(scenario, stepNumber, keyword, step)
                });
            }

            return bound;
        }

        private static object[] BindArguments(Scenario scenario, int stepNumber, Keyword keyword, ScenarioStep step)
        {
            // An argument the keyword does not declare is nearly always a typo or a stale
            // scenario written against an older signature. Silently ignoring it would run
            // something subtly different from what the author wrote.
            foreach (string supplied in step.Arguments.Keys)
            {
                if (keyword.Parameters.All(parameter => parameter.Name != supplied))
                {
                    throw Malformed(scenario, stepNumber,
                        $"'{keyword.Name}' has no argument '{supplied}'. Expected: " +
                        $"{FormatParameterList(keyword)}.");
                }
            }

            var values = new object[keyword.Parameters.Count];

            for (int index = 0; index < keyword.Parameters.Count; index++)
            {
                KeywordParameter parameter = keyword.Parameters[index];

                if (!step.Arguments.TryGetValue(parameter.Name, out object raw))
                {
                    if (!parameter.IsOptional)
                    {
                        throw Malformed(scenario, stepNumber,
                            $"'{keyword.Name}' requires argument '{parameter.Name}' ({parameter.TypeName}).");
                    }

                    values[index] = parameter.DefaultValue;
                    continue;
                }

                try
                {
                    values[index] = Convert(raw, parameter);
                }
                catch (Exception exception)
                {
                    throw Malformed(scenario, stepNumber,
                        $"argument '{parameter.Name}' of '{keyword.Name}': {exception.Message}");
                }
            }

            return values;
        }

        /// <summary>
        /// Converts a JSON scalar to the parameter's CLR type.
        /// </summary>
        /// <remarks>
        /// JSON has no int/float distinction and Newtonsoft yields long and double, so the
        /// numeric cases are conversions rather than casts. Enums are matched by name because
        /// that is what the catalog offers the UI and what reads sensibly in a file.
        /// </remarks>
        private static object Convert(object raw, KeywordParameter parameter)
        {
            if (raw == null)
            {
                if (parameter.Type == typeof(string))
                {
                    return null;
                }

                throw new InvalidOperationException($"expected {parameter.TypeName} but got null.");
            }

            if (parameter.Type.IsEnum)
            {
                string name = raw.ToString();

                foreach (string allowed in parameter.AllowedValues)
                {
                    if (string.Equals(allowed, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return Enum.Parse(parameter.Type, allowed);
                    }
                }

                throw new InvalidOperationException(
                    $"'{name}' is not one of: {string.Join(", ", parameter.AllowedValues)}.");
            }

            try
            {
                return System.Convert.ChangeType(raw, parameter.Type, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                throw new InvalidOperationException($"expected {parameter.TypeName} but got '{raw}'.");
            }
        }

        private object GetInstance(Dictionary<Type, object> instances, Type type)
        {
            if (instances.TryGetValue(type, out object existing))
            {
                return existing;
            }

            ConstructorInfo constructor = type.GetConstructor(new[] { typeof(IGameDriver) });

            if (constructor == null)
            {
                throw new InvalidOperationException(
                    $"{type.FullName} declares scenario actions but has no public constructor " +
                    "taking an IGameDriver, which is how the executor builds it.");
            }

            object instance = constructor.Invoke(new object[] { _driver });
            instances.Add(type, instance);

            return instance;
        }

        private static string FormatParameterList(Keyword keyword)
        {
            if (keyword.Parameters.Count == 0)
            {
                return "(none)";
            }

            return string.Join(", ", keyword.Parameters.Select(parameter => $"{parameter.Name} ({parameter.TypeName})"));
        }

        private static InvalidOperationException Malformed(Scenario scenario, int stepNumber, string detail)
        {
            return new InvalidOperationException(
                $"Scenario '{scenario.Name}' step {stepNumber} {detail} [{scenario.SourcePath}]");
        }

        private sealed class BoundStep
        {
            public int Number { get; init; }
            public Keyword Keyword { get; init; }
            public object[] Arguments { get; init; }
        }
    }

    /// <summary>
    /// A step failed while running. Names the scenario and step so a failure in CI is
    /// traceable to the file a QA engineer authored.
    /// </summary>
    public sealed class ScenarioStepException : Exception
    {
        public ScenarioStepException(Scenario scenario, int stepNumber, string action, Exception inner)
            : base($"Scenario '{scenario.Name}' failed at step {stepNumber} ('{action}'): {inner.Message} " +
                   $"[{scenario.SourcePath}]", inner)
        {
        }
    }
}
