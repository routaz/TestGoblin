using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Routaz.TestGoblin.Runtime.Scenarios
{
    /// <summary>
    /// One argument of a keyword, described well enough for a UI to render an input for it.
    /// </summary>
    public sealed class KeywordParameter
    {
        /// <summary>Argument name, as scenario JSON spells it.</summary>
        public string Name { get; init; }

        /// <summary>The CLR type the executor must convert an argument to.</summary>
        public Type Type { get; init; }

        /// <summary>Friendly type name for the catalog: int, float, bool, string or enum.</summary>
        public string TypeName { get; init; }

        /// <summary>Legal values when <see cref="Type"/> is an enum; otherwise null.</summary>
        public IReadOnlyList<string> AllowedValues { get; init; }

        /// <summary>Whether the scenario may omit this argument.</summary>
        public bool IsOptional { get; init; }

        /// <summary>The value used when omitted; null when required.</summary>
        public object DefaultValue { get; init; }
    }

    /// <summary>
    /// One callable keyword: its name, its arguments, and the method behind it.
    /// </summary>
    public sealed class Keyword
    {
        /// <summary>The name scenario files use.</summary>
        public string Name { get; init; }

        /// <summary>Optional help text for the scenario builder.</summary>
        public string Description { get; init; }

        /// <summary>The method to invoke.</summary>
        public MethodInfo Method { get; init; }

        /// <summary>
        /// The type declaring the method. The executor constructs one of these,
        /// passing the driver, before invoking.
        /// </summary>
        public Type DeclaringType { get; init; }

        /// <summary>Arguments, in declaration order.</summary>
        public IReadOnlyList<KeywordParameter> Parameters { get; init; }
    }

    /// <summary>
    /// The scenario vocabulary: every <see cref="ScenarioActionAttribute"/> method found,
    /// validated and indexed by keyword name.
    /// </summary>
    /// <remarks>
    /// Validation happens here, at construction, rather than when a scenario runs. A
    /// malformed vocabulary is a programming error, and the useful moment to report it is
    /// while someone is editing C# — not when a QA engineer presses Run and gets a stack
    /// trace about a method they have never heard of.
    /// </remarks>
    public sealed class KeywordRegistry
    {
        private readonly Dictionary<string, Keyword> _keywords;

        /// <summary>Every valid keyword, ordered by name.</summary>
        public IReadOnlyList<Keyword> Keywords { get; }

        /// <summary>
        /// Malformed <see cref="ScenarioActionAttribute"/> methods that were skipped.
        /// Always empty when the throwing constructor was used.
        /// </summary>
        public IReadOnlyList<string> Problems { get; }

        private KeywordRegistry(List<Keyword> keywords, List<string> problems)
        {
            _keywords = BuildIndex(keywords, problems);

            // Derived from the index, not the raw list, so a name that lost a duplicate
            // clash cannot appear twice in the published vocabulary.
            Keywords = _keywords.Values.OrderBy(keyword => keyword.Name, StringComparer.Ordinal).ToList();
            Problems = problems;
        }

        /// <summary>
        /// Builds a registry from an explicit set of types, rejecting any defect.
        /// </summary>
        /// <param name="types">Types to scan for <see cref="ScenarioActionAttribute"/> methods.</param>
        /// <exception cref="InvalidOperationException">The vocabulary is malformed.</exception>
        /// <remarks>
        /// Strict because the caller chose these types, so a malformed keyword is their bug
        /// and the useful moment to say so is immediately.
        /// </remarks>
        public KeywordRegistry(IEnumerable<Type> types)
        {
            List<Keyword> found = Scan(types, out List<string> problems);
            _keywords = BuildIndex(found, problems);

            if (problems.Count > 0)
            {
                throw new InvalidOperationException(string.Join(" ", problems));
            }

            Keywords = _keywords.Values.OrderBy(keyword => keyword.Name, StringComparer.Ordinal).ToList();
            Problems = Array.Empty<string>();
        }

        /// <summary>
        /// Builds the production vocabulary by scanning loaded assemblies, excluding test
        /// assemblies and skipping anything malformed.
        /// </summary>
        /// <remarks>
        /// Broad by design: keywords live in per-title Flows assemblies whose names the
        /// framework cannot know. Deliberately tolerant, because a domain-wide sweep reaches
        /// code this registry does not own. One bad method elsewhere must not deprive QA of
        /// the whole vocabulary, so problems are reported through <see cref="Problems"/>
        /// rather than thrown.
        /// </remarks>
        public static KeywordRegistry FromLoadedAssemblies()
        {
            return FromLoadedAssemblies(IsNotTestAssembly);
        }

        /// <summary>
        /// Builds a registry from loaded assemblies matching <paramref name="assemblyFilter"/>.
        /// </summary>
        /// <param name="assemblyFilter">Which assemblies to scan.</param>
        public static KeywordRegistry FromLoadedAssemblies(Func<Assembly, bool> assemblyFilter)
        {
            List<Keyword> keywords = Scan(
                AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assemblyFilter)
                    .SelectMany(SafeGetTypes),
                out List<string> problems);

            return new KeywordRegistry(keywords, problems);
        }

        /// <summary>
        /// Whether an assembly looks like production code rather than tests.
        /// </summary>
        /// <remarks>
        /// Test assemblies are excluded from the default vocabulary because their fixtures
        /// are not keywords a QA engineer should ever be offered — and fixtures written to
        /// exercise the registry's own validation would otherwise appear in the catalog as
        /// both fake keywords and fake problems.
        /// <para>
        /// Detected by an NUnit reference rather than a name convention, since assembly
        /// naming is a project's own choice but depending on a test framework is not.
        /// </para>
        /// </remarks>
        private static bool IsNotTestAssembly(Assembly assembly)
        {
            try
            {
                return !assembly.GetReferencedAssemblies()
                    .Any(reference => reference.Name.StartsWith("nunit", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>
        /// Collects every well-formed keyword, describing the rest in <paramref name="problems"/>.
        /// </summary>
        private static List<Keyword> Scan(IEnumerable<Type> types, out List<string> problems)
        {
            var found = new List<Keyword>();
            var collected = new List<string>();

            foreach (Type type in types)
            {
                foreach (MethodInfo method in SafeGetMethods(type))
                {
                    ScenarioActionAttribute attribute = SafeGetScenarioAction(method);

                    if (attribute == null)
                    {
                        continue;
                    }

                    if (TryDescribe(method, type, attribute, out Keyword keyword, out string problem))
                    {
                        found.Add(keyword);
                    }
                    else
                    {
                        collected.Add(problem);
                    }
                }
            }

            problems = collected;
            return found;
        }

        /// <summary>
        /// Looks up a keyword by the name a scenario used.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// No such keyword — with the available names listed, since the usual cause is a typo.
        /// </exception>
        public Keyword Resolve(string name)
        {
            if (_keywords.TryGetValue(name, out Keyword keyword))
            {
                return keyword;
            }

            throw new InvalidOperationException(
                $"Unknown scenario action '{name}'. Available actions: {string.Join(", ", _keywords.Keys.OrderBy(key => key, StringComparer.Ordinal))}.");
        }

        /// <summary>Whether a keyword with this name exists.</summary>
        public bool Contains(string name)
        {
            return _keywords.ContainsKey(name);
        }

        private static bool TryDescribe(
            MethodInfo method,
            Type declaringType,
            ScenarioActionAttribute attribute,
            out Keyword keyword,
            out string problem)
        {
            keyword = null;
            problem = null;

            string origin = $"{declaringType.FullName}.{method.Name}";

            if (string.IsNullOrWhiteSpace(attribute.Name))
            {
                problem = $"{origin} has a [ScenarioAction] with an empty name.";
                return false;
            }

            // Steps are actions to await, not queries. Allowing Task<T> would raise the
            // question of what a scenario does with the result, which is a bigger design
            // decision than this layer should make silently.
            if (method.ReturnType != typeof(Task))
            {
                problem = $"{origin} is marked [ScenarioAction] but returns {method.ReturnType.Name}. " +
                          "Scenario actions must return Task.";
                return false;
            }

            if (method.IsGenericMethodDefinition)
            {
                problem = $"{origin} is generic; scenario actions cannot be.";
                return false;
            }

            var parameters = new List<KeywordParameter>();

            foreach (ParameterInfo parameter in method.GetParameters())
            {
                string typeName = FriendlyTypeName(parameter.ParameterType);

                if (typeName == null)
                {
                    problem = $"{origin} parameter '{parameter.Name}' is of unsupported type " +
                              $"{parameter.ParameterType.Name}. Scenario actions may only take " +
                              "int, float, bool, string or enum parameters.";
                    return false;
                }

                parameters.Add(new KeywordParameter
                {
                    Name = parameter.Name,
                    Type = parameter.ParameterType,
                    TypeName = typeName,
                    AllowedValues = parameter.ParameterType.IsEnum
                        ? Enum.GetNames(parameter.ParameterType)
                        : null,
                    IsOptional = parameter.HasDefaultValue,
                    DefaultValue = parameter.HasDefaultValue ? parameter.DefaultValue : null
                });
            }

            keyword = new Keyword
            {
                Name = attribute.Name,
                Description = attribute.Description ?? "",
                Method = method,
                DeclaringType = declaringType,
                Parameters = parameters
            };

            return true;
        }

        /// <summary>
        /// Maps a CLR type to the small set the catalog and scenario JSON can represent,
        /// or null if there is no such mapping.
        /// </summary>
        /// <remarks>
        /// The restriction is deliberate. Every supported type has an obvious JSON form and
        /// an obvious UI control; anything else would have neither, so it is rejected at the
        /// boundary instead of failing later in a way that looks like a scenario bug.
        /// </remarks>
        private static string FriendlyTypeName(Type type)
        {
            if (type == typeof(int))
            {
                return "int";
            }

            if (type == typeof(float))
            {
                return "float";
            }

            if (type == typeof(bool))
            {
                return "bool";
            }

            if (type == typeof(string))
            {
                return "string";
            }

            if (type.IsEnum)
            {
                return "enum";
            }

            return null;
        }

        /// <summary>
        /// Indexes keywords by name, recording rather than resolving any clash.
        /// </summary>
        /// <remarks>
        /// The first declaration wins. Dropping both would turn a name clash into
        /// "unknown action" for a scenario that did nothing wrong, which is a worse
        /// message than the clash itself — and the clash is reported either way.
        /// </remarks>
        private static Dictionary<string, Keyword> BuildIndex(List<Keyword> keywords, List<string> problems)
        {
            var index = new Dictionary<string, Keyword>(StringComparer.Ordinal);

            foreach (Keyword keyword in keywords)
            {
                if (index.TryGetValue(keyword.Name, out Keyword existing))
                {
                    problems.Add(
                        $"Duplicate scenario action '{keyword.Name}', declared on both " +
                        $"{existing.DeclaringType.FullName}.{existing.Method.Name} and " +
                        $"{keyword.DeclaringType.FullName}.{keyword.Method.Name}.");

                    continue;
                }

                index.Add(keyword.Name, keyword);
            }

            return index;
        }

        /// <summary>
        /// Gets a type's public instance methods, tolerating types that fail to load.
        /// </summary>
        private static MethodInfo[] SafeGetMethods(Type type)
        {
            try
            {
                return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            }
            catch (Exception)
            {
                return Array.Empty<MethodInfo>();
            }
        }

        /// <summary>
        /// Reads the <see cref="ScenarioActionAttribute"/> off a method, or null if the
        /// method's attributes cannot be inspected at all.
        /// </summary>
        /// <remarks>
        /// Reading custom attributes forces the runtime to load every attribute type on the
        /// member, so one unresolvable attribute from unrelated code — JetBrains.Annotations
        /// is the usual culprit in a Rider project — throws while reading ours.
        /// <para>
        /// Skipped silently, deliberately, and NOT recorded as a problem: we cannot tell
        /// whether such a method was ever a keyword, and third-party assemblies produce these
        /// by the hundred. Reporting them would bury the handful of problems that are
        /// genuinely about someone's own vocabulary.
        /// </para>
        /// </remarks>
        private static ScenarioActionAttribute SafeGetScenarioAction(MethodInfo method)
        {
            try
            {
                return method.GetCustomAttribute<ScenarioActionAttribute>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Gets an assembly's types, tolerating ones that fail to load.
        /// </summary>
        /// <remarks>
        /// A single unloadable assembly anywhere in the domain would otherwise take the
        /// whole vocabulary down, and it is rarely one of ours.
        /// </remarks>
        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null);
            }
            catch (Exception)
            {
                return Array.Empty<Type>();
            }
        }
    }
}
