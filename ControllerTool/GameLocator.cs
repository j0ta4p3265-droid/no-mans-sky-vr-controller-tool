using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace NMSOpenCompositeConfigurator;

internal static partial class GameLocator
{
    private const string SteamAppId = "275850";

    public static string? FindNoMansSky(string? savedFolder = null)
    {
        if (TryNormalizeGameFolder(savedFolder, out var saved))
            return saved;

        foreach (var library in FindSteamLibraries())
        {
            var steamApps = Path.Combine(library, "steamapps");
            var manifest = Path.Combine(steamApps, $"appmanifest_{SteamAppId}.acf");
            if (File.Exists(manifest))
            {
                try
                {
                    var match = InstallDirectoryRegex().Match(File.ReadAllText(manifest));
                    if (match.Success && TryNormalizeGameFolder(
                            Path.Combine(steamApps, "common", match.Groups[1].Value), out var fromManifest))
                        return fromManifest;
                }
                catch
                {
                    // Fall back to Steam's standard installation folder below.
                }
            }

            if (TryNormalizeGameFolder(Path.Combine(steamApps, "common", "No Man's Sky"), out var standard))
                return standard;
        }

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.IsReady && TryNormalizeGameFolder(
                        Path.Combine(drive.RootDirectory.FullName, "XboxGames", "No Man's Sky", "Content"),
                        out var xboxGame))
                    return xboxGame;
            }
            catch
            {
                // An unavailable removable or network drive must not stop discovery.
            }
        }

        return null;
    }

    public static bool IsValidGameFolder(string path)
    {
        return File.Exists(Path.Combine(path, "Binaries", "NMS.exe"))
            && File.Exists(Path.Combine(path, "GAMEDATA", "INPUT", "ACTIONS.JSON"))
            && File.Exists(Path.Combine(path, "GAMEDATA", "INPUT", "TOUCH.JSON"));
    }

    public static bool TryNormalizeGameFolder(string? path, out string folder)
    {
        folder = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            var candidate = Path.GetFullPath(path.Trim().Trim('"'))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (Path.GetFileName(candidate).Equals("Binaries", StringComparison.OrdinalIgnoreCase))
                candidate = Directory.GetParent(candidate)?.FullName ?? candidate;

            if (!IsValidGameFolder(candidate))
                return false;

            folder = candidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> FindSteamLibraries()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var defaultSteam = Path.Combine(programFilesX86, "Steam");
        roots.Add(defaultSteam);

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        roots.Add(Path.Combine(programFiles, "Steam"));

        AddRegistrySteamPath(roots, Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath");
        AddRegistrySteamPath(roots, Registry.LocalMachine, @"Software\Valve\Steam", "InstallPath");
        AddRegistrySteamPath(roots, Registry.LocalMachine, @"Software\WOW6432Node\Valve\Steam", "InstallPath");

        foreach (var steamRoot in roots.ToArray())
        {
            yield return steamRoot;
            var vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf))
                continue;
            string contents;
            try
            {
                contents = File.ReadAllText(vdf);
            }
            catch
            {
                continue;
            }

            foreach (Match match in SteamPathRegex().Matches(contents))
            {
                var path = match.Groups[1].Value.Replace("\\\\", "\\");
                if (Directory.Exists(path) && roots.Add(path))
                    yield return path;
            }
        }
    }

    private static void AddRegistrySteamPath(HashSet<string> roots, RegistryKey root, string keyPath, string valueName)
    {
        try
        {
            using var key = root.OpenSubKey(keyPath);
            if (key?.GetValue(valueName) is string path && Directory.Exists(path))
                roots.Add(path.Replace('/', Path.DirectorySeparatorChar));
        }
        catch
        {
            // Registry discovery is optional; other locations can still be checked.
        }
    }

    [GeneratedRegex("\\\"path\\\"\\s+\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase)]
    private static partial Regex SteamPathRegex();

    [GeneratedRegex("\\\"installdir\\\"\\s+\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase)]
    private static partial Regex InstallDirectoryRegex();
}
