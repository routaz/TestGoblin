using System.Xml.Linq;

namespace TestGoblin.Server;

/// <summary>
/// One executed test, flattened out of the NUnit3 result tree.
/// </summary>
/// <param name="FullName">Namespace-qualified name, unique within a run.</param>
/// <param name="ClassName">Declaring fixture — used to group results in the UI.</param>
/// <param name="Name">Short method name.</param>
/// <param name="Result">NUnit outcome: Passed, Failed, Skipped or Inconclusive.</param>
/// <param name="DurationSeconds">Wall-clock execution time.</param>
/// <param name="FailureMessage">Assertion message; null unless the test failed.</param>
/// <param name="StackTrace">Stack trace; null unless the test failed.</param>
public sealed record TestCaseResult(
    string FullName,
    string ClassName,
    string Name,
    string Result,
    double DurationSeconds,
    string? FailureMessage,
    string? StackTrace);

/// <summary>
/// Aggregate outcome of a single Unity test run.
/// </summary>
public sealed record TestRunResult(
    string Result,
    int Total,
    int Passed,
    int Failed,
    int Skipped,
    int Inconclusive,
    double DurationSeconds,
    IReadOnlyList<TestCaseResult> Cases);

/// <summary>
/// Reads the NUnit3 XML file that Unity writes via <c>-testResults</c> and
/// flattens it into a shape the HTML UI can render directly.
/// </summary>
/// <remarks>
/// We deliberately consume Unity's existing output format rather than inventing
/// one: the same file is what CI systems already understand, so nothing here is
/// a private format that only TestGoblin can read.
/// </remarks>
public static class NUnitResultParser
{
    /// <summary>
    /// Parses an NUnit3 results file.
    /// </summary>
    /// <param name="xmlPath">Path to the results XML written by Unity.</param>
    /// <returns>The flattened run result.</returns>
    /// <exception cref="FileNotFoundException">
    /// The file does not exist — usually meaning Unity died before writing results.
    /// </exception>
    /// <exception cref="InvalidDataException">The file is not an NUnit3 result document.</exception>
    public static TestRunResult Parse(string xmlPath)
    {
        if (!File.Exists(xmlPath))
        {
            throw new FileNotFoundException(
                $"No test results at '{xmlPath}'. Unity usually failed to start or crashed " +
                "before the run finished — check the Unity log.",
                xmlPath);
        }

        XDocument document = XDocument.Load(xmlPath);
        XElement run = document.Root
            ?? throw new InvalidDataException($"'{xmlPath}' is empty.");

        if (run.Name != "test-run")
        {
            throw new InvalidDataException(
                $"Expected a <test-run> root in '{xmlPath}' but found <{run.Name}>.");
        }

        // Descendants() walks the whole nested suite tree, so we get every leaf
        // regardless of how deeply Unity nested assemblies/namespaces/fixtures.
        List<TestCaseResult> cases = run
            .Descendants("test-case")
            .Select(ParseCase)
            .OrderBy(testCase => testCase.ClassName, StringComparer.Ordinal)
            .ThenBy(testCase => testCase.Name, StringComparer.Ordinal)
            .ToList();

        return new TestRunResult(
            Result: Attribute(run, "result", "Unknown"),
            Total: IntAttribute(run, "total"),
            Passed: IntAttribute(run, "passed"),
            Failed: IntAttribute(run, "failed"),
            Skipped: IntAttribute(run, "skipped"),
            Inconclusive: IntAttribute(run, "inconclusive"),
            DurationSeconds: DoubleAttribute(run, "duration"),
            Cases: cases);
    }

    private static TestCaseResult ParseCase(XElement element)
    {
        // Present only on failures. NUnit nests the detail one level down.
        XElement? failure = element.Element("failure");

        return new TestCaseResult(
            FullName: Attribute(element, "fullname", string.Empty),
            ClassName: Attribute(element, "classname", string.Empty),
            Name: Attribute(element, "name", string.Empty),
            Result: Attribute(element, "result", "Unknown"),
            DurationSeconds: DoubleAttribute(element, "duration"),
            FailureMessage: failure?.Element("message")?.Value.Trim(),
            StackTrace: failure?.Element("stack-trace")?.Value.Trim());
    }

    private static string Attribute(XElement element, string name, string fallback)
    {
        return element.Attribute(name)?.Value ?? fallback;
    }

    private static int IntAttribute(XElement element, string name)
    {
        return int.TryParse(Attribute(element, name, "0"), out int value) ? value : 0;
    }

    private static double DoubleAttribute(XElement element, string name)
    {
        // NUnit always writes invariant-culture decimals; parsing with the current
        // culture would silently mangle them on a comma-decimal machine (fi-FI).
        return double.TryParse(
            Attribute(element, name, "0"),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out double value)
            ? value
            : 0d;
    }
}
