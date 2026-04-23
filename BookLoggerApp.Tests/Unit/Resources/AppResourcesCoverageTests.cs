using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Resources;

/// <summary>
/// Guards the EN + DE <c>.resx</c> pair against drift: every key defined in the
/// neutral resource must have a matching entry in the German resource and vice
/// versa. Runs against the actual resx files in <c>BookLoggerApp.Core/Resources/</c>.
/// </summary>
public class AppResourcesCoverageTests
{
    [Fact]
    public void GermanResx_ContainsEveryKeyFromNeutralResx_AndNoExtras()
    {
        (string resxRoot, string neutralPath, string germanPath) = LocateResxFiles();

        HashSet<string> neutralKeys = LoadKeys(neutralPath);
        HashSet<string> germanKeys = LoadKeys(germanPath);

        neutralKeys.Should().NotBeEmpty($"expected keys in {neutralPath}");

        IEnumerable<string> missingInGerman = neutralKeys.Except(germanKeys).OrderBy(x => x);
        IEnumerable<string> extrasInGerman = germanKeys.Except(neutralKeys).OrderBy(x => x);

        missingInGerman.Should().BeEmpty(
            "every neutral (EN) key must have a German translation. Resx root: {0}", resxRoot);
        extrasInGerman.Should().BeEmpty(
            "German resx must not define keys missing from the neutral resx. Resx root: {0}", resxRoot);
    }

    [Fact]
    public void GermanResx_HasNonEmptyValueForEveryKey()
    {
        (_, _, string germanPath) = LocateResxFiles();

        List<string> emptyKeys = new();
        foreach (XElement data in XDocument.Load(germanPath).Root!.Elements("data"))
        {
            string? key = data.Attribute("name")?.Value;
            string? value = data.Element("value")?.Value;
            if (!string.IsNullOrEmpty(key) && string.IsNullOrWhiteSpace(value))
            {
                emptyKeys.Add(key);
            }
        }

        emptyKeys.Should().BeEmpty("German resx must not contain empty translations");
    }

    private static HashSet<string> LoadKeys(string path)
    {
        XDocument doc = XDocument.Load(path);
        return doc.Root!
            .Elements("data")
            .Select(e => e.Attribute("name")?.Value)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static (string resxRoot, string neutralPath, string germanPath) LocateResxFiles()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "BookLoggerApp.Core", "Resources");
            if (Directory.Exists(candidate))
            {
                string neutral = Path.Combine(candidate, "AppResources.resx");
                string german = Path.Combine(candidate, "AppResources.de.resx");
                if (File.Exists(neutral) && File.Exists(german))
                {
                    return (candidate, neutral, german);
                }
            }
            dir = Path.GetDirectoryName(dir);
        }

        throw new FileNotFoundException("Could not locate AppResources.resx pair. Run tests from solution root.");
    }
}
