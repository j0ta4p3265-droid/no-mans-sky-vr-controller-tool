using System.Text.RegularExpressions;

namespace NMSOpenCompositeConfigurator;

internal static partial class GameLocator
{
    public static string? FindNoMansSky()
    {
        foreach (var library in FindSteamLibraries())
        {
            var candidate = Path.Combine(library, "steamapps", "common", "No Man's Sky");
            if (IsValidGameFolder(candidate))
                return candidate;
        }
        return null;
    }

    public static bool IsValidGameFolder(string path)
    {
        return File.Exists(Path.Combine(path, "Binaries", "NMS.exe"))
            && File.Exists(Path.Combine(path, "GAMEDATA", "INPUT", "ACTIONS.JSON"))
            && File.Exists(Path.Combine(path, "GAMEDATA", "INPUT", "TOUCH.JSON"));
    }

    private static IEnumerable<string> FindSteamLibraries()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var defaultSteam = Path.Combine(programFilesX86, "Steam");
        roots.Add(defaultSteam);

        foreach (var steamRoot in roots.ToArray())
        {
            yield return steamRoot;
            var vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf))
                continue;
            foreach (Match match in SteamPathRegex().Matches(File.ReadAllText(vdf)))
            {
                var path = match.Groups[1].Value.Replace("\\\\", "\\");
                if (Directory.Exists(path) && roots.Add(path))
                    yield return path;
            }
        }
    }

    [GeneratedRegex("\\\"path\\\"\\s+\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase)]
    private static partial Regex SteamPathRegex();
}
