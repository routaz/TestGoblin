using System.Text.Json;

namespace TestGoblin.Server;

/// <summary>Where scenario files and the keyword catalog live. Bound from appsettings.json.</summary>
public sealed class ScenarioOptions
{
    /// <summary>
    /// Folder holding <c>*.scenario.json</c>. Points at the package's <c>Scenarios~</c>
    /// folder, which is the same location the Unity-side loader reads.
    /// </summary>
    public string Directory { get; set; } = "";

    /// <summary>
    /// The <c>keywords.json</c> written by the Unity-side exporter.
    /// </summary>
    public string CatalogPath { get; set; } = "keywords.json";
}

/// <summary>
/// Reads and writes scenario files on behalf of the browser.
/// </summary>
/// <remarks>
/// The browser cannot touch the filesystem, so this is the only thing standing between a
/// scenario the QA engineer composed and a file the Unity test runner will pick up. It is
/// also a trust boundary: file names arrive from a web page, so they are validated rather
/// than concatenated into a path.
/// </remarks>
public sealed class ScenarioStore
{
    /// <summary>Required suffix — matches the pattern the Unity-side loader searches for.</summary>
    public const string RequiredSuffix = ".scenario.json";

    private readonly ScenarioOptions _options;

    public ScenarioStore(ScenarioOptions options)
    {
        _options = options;
    }

    /// <summary>The resolved scenario directory.</summary>
    public string Directory => _options.Directory;

    /// <summary>Lists scenario file names, sorted. Empty when the folder does not exist.</summary>
    public IReadOnlyList<string> List()
    {
        if (!System.IO.Directory.Exists(_options.Directory))
        {
            return Array.Empty<string>();
        }

        return System.IO.Directory
            .GetFiles(_options.Directory, "*" + RequiredSuffix)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Reads one scenario's raw JSON.</summary>
    /// <exception cref="FileNotFoundException">No such scenario.</exception>
    public string Read(string fileName)
    {
        string path = ResolvePath(fileName);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No scenario named '{fileName}'.", path);
        }

        return File.ReadAllText(path);
    }

    /// <summary>
    /// Writes a scenario, creating the folder if needed.
    /// </summary>
    /// <exception cref="ArgumentException">The name or the content is unacceptable.</exception>
    public void Write(string fileName, string json)
    {
        string path = ResolvePath(fileName);

        // Parsed before writing so a malformed body fails here, where the browser can show
        // it, rather than later inside a Unity run that reports it as a test failure.
        try
        {
            using JsonDocument _ = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException($"Scenario content is not valid JSON: {exception.Message}");
        }

        System.IO.Directory.CreateDirectory(_options.Directory);
        File.WriteAllText(path, json);
    }

    /// <summary>Deletes a scenario. Missing files are not an error.</summary>
    public void Delete(string fileName)
    {
        string path = ResolvePath(fileName);

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>Reads the keyword catalog, or null if it has not been exported yet.</summary>
    public string? ReadCatalog()
    {
        return File.Exists(_options.CatalogPath) ? File.ReadAllText(_options.CatalogPath) : null;
    }

    /// <summary>
    /// Turns a browser-supplied name into a path inside the scenario folder, or refuses.
    /// </summary>
    /// <remarks>
    /// The name is whitelisted rather than sanitised. Stripping bad characters invites the
    /// argument about whether the strip was complete; rejecting anything that is not a plain
    /// file name does not. Combined with a final containment check, a request cannot address
    /// a file outside the scenario folder even if the whitelist is wrong.
    /// </remarks>
    private string ResolvePath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("Scenario name is required.");
        }

        if (!fileName.EndsWith(RequiredSuffix, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Scenario file names must end with '{RequiredSuffix}'.");
        }

        foreach (char character in fileName)
        {
            bool allowed = char.IsLetterOrDigit(character)
                || character == '.'
                || character == '-'
                || character == '_';

            if (!allowed)
            {
                throw new ArgumentException(
                    $"Invalid character '{character}' in scenario name. " +
                    "Use letters, digits, dots, dashes and underscores only.");
            }
        }

        string directory = Path.GetFullPath(_options.Directory);
        string path = Path.GetFullPath(Path.Combine(directory, fileName));

        // Belt and braces: whatever the name was, the result must be inside the folder.
        if (!path.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Scenario name resolves outside the scenario folder.");
        }

        return path;
    }
}
