namespace NMSOpenCompositeConfigurator;

internal static class SelfTest
{
    public static void RunSettingsAndLocator(string gameFolder, string settingsPath, string reportPath)
    {
        try
        {
            var expected = Path.GetFullPath(gameFolder).TrimEnd(Path.DirectorySeparatorChar);
            var settings = new AppSettings
            {
                GameFolder = expected,
                HandLayout = "left"
            };
            settings.Save(settingsPath);

            var reloaded = AppSettings.Load(settingsPath);
            var detected = GameLocator.FindNoMansSky(reloaded.GameFolder);
            var automaticallyDetected = GameLocator.FindNoMansSky();
            var binariesDetected = GameLocator.TryNormalizeGameFolder(
                Path.Combine(expected, "Binaries"), out var normalizedFromBinaries);
            var passed = reloaded.GameFolder == expected
                         && reloaded.HandLayout == "left"
                         && string.Equals(detected, expected, StringComparison.OrdinalIgnoreCase)
                         && GameLocator.IsValidGameFolder(automaticallyDetected ?? string.Empty)
                         && binariesDetected
                         && string.Equals(normalizedFromBinaries, expected, StringComparison.OrdinalIgnoreCase);

            File.WriteAllText(reportPath,
                passed
                    ? $"PASS\nSaved: {reloaded.GameFolder}\nDetected from settings: {detected}\nDetected automatically: {automaticallyDetected}\nSettings: {settingsPath}"
                    : $"FAIL\nSaved: {reloaded.GameFolder}\nDetected from settings: {detected}\nDetected automatically: {automaticallyDetected}\nNormalized Binaries: {normalizedFromBinaries}");
        }
        catch (Exception ex)
        {
            File.WriteAllText(reportPath, "FAIL: " + ex);
        }
    }

    public static void Run(string gameFolder, string reportPath)
    {
        try
        {
            var iniPath = Path.Combine(gameFolder, "Binaries", "opencomposite.ini");
            var ini = IniDocument.Load(iniPath);
            ini.Set("leftDeadZoneSize", 0.12m);
            ini.Set("rightDeadZoneSize", 0.08m);
            ini.Set("rightJoystickScale", 1.05m);
            ini.Set("enableThumbrestDoubleTap", true);
            ini.Set("enableRightThumbrestDoubleTap", true);
            ini.Set("thumbrestDoubleTapWindowMs", 450);
            ini.SaveAtomic(iniPath);

            var bindings = NmsBindingDocument.Load(gameFolder);
            var onFoot = bindings.GetContexts().First(item =>
                item.Name.Equals("/actions/OnFootControls", StringComparison.OrdinalIgnoreCase));
            var entries = bindings.GetBindings(onFoot.Name);
            var groups = bindings.GetActionBindingGroups(onFoot.Name);
            var menuContexts = bindings.GetContexts();
            var mergedMenus = bindings.GetActionBindingGroups(new[]
            {
                menuContexts.First(item => item.Name.Equals("/actions/FRONTEND", StringComparison.OrdinalIgnoreCase)).Name,
                menuContexts.First(item => item.Name.Equals("/actions/FRONTEND_RIGHT", StringComparison.OrdinalIgnoreCase)).Name
            });
            var recentre = groups.First(group =>
                group.DisplayName.Equals("Recentre View", StringComparison.OrdinalIgnoreCase));
            var selected = recentre.Entries.First();
            var replacement = bindings.GetPhysicalControls(selected.RequiredType).First(control =>
                !control.DevicePath.Equals(selected.DevicePath, StringComparison.OrdinalIgnoreCase)
                || !control.Mode.Equals(selected.Mode, StringComparison.OrdinalIgnoreCase)
                || !control.InputComponent.Equals(selected.InputComponent, StringComparison.OrdinalIgnoreCase));
            bindings.Rebind(selected, replacement);
            var thumbrestBindings = bindings.ConfigureThumbrestRecentre(true, true);
            bindings.SaveWithBackup();

            var reloadedIni = IniDocument.Load(iniPath);
            var reloadedBindings = NmsBindingDocument.Load(gameFolder);
            var reloadedRecentre = reloadedBindings.GetActionBindingGroups(onFoot.Name).First(group =>
                group.DisplayName.Equals("Recentre View", StringComparison.OrdinalIgnoreCase));
			var booleanControls = reloadedBindings.GetPhysicalControls("boolean");
            var passed = reloadedIni.GetDecimal("leftDeadZoneSize", -1m) == 0.12m
                && reloadedIni.GetDecimal("rightDeadZoneSize", -1m) == 0.08m
                && reloadedIni.GetDecimal("rightJoystickScale", -1m) == 1.05m
                && reloadedIni.GetBoolean("enableThumbrestDoubleTap", false)
                && reloadedIni.GetBoolean("enableRightThumbrestDoubleTap", false)
                && reloadedIni.GetDecimal("thumbrestDoubleTapWindowMs", -1m) == 450m
                && File.Exists(bindings.BackupPath)
                && recentre.Entries.Count >= 2
                && mergedMenus.Any(group => group.DisplayName.Equals("Quick Transfer", StringComparison.OrdinalIgnoreCase))
                && mergedMenus.Any(group => group.DisplayName.Equals("Confirm (Menus) / Move & Stack Items", StringComparison.OrdinalIgnoreCase))
                && thumbrestBindings > 0
				&& reloadedRecentre.Entries.Any(entry =>
					entry.DevicePath.Equals("/user/hand/left/input/thumbrest", StringComparison.OrdinalIgnoreCase)
					&& entry.InputComponent.Equals("touch", StringComparison.OrdinalIgnoreCase))
				&& reloadedRecentre.Entries.Any(entry =>
					entry.DevicePath.Equals("/user/hand/right/input/thumbrest", StringComparison.OrdinalIgnoreCase)
					&& entry.InputComponent.Equals("touch", StringComparison.OrdinalIgnoreCase))
				&& !booleanControls.Any(control =>
					control.DevicePath.Contains("/input/thumbrest", StringComparison.OrdinalIgnoreCase))
				&& booleanControls.GroupBy(control => control.DisplayName, StringComparer.OrdinalIgnoreCase)
					.All(group => group.Count() == 1);

            File.WriteAllText(reportPath,
                passed
					? $"PASS\nContexts: {bindings.GetContexts().Count}\nOn-foot actions: {groups.Count}\nRecentre controls grouped: {recentre.Entries.Count}\nThumbrest macro bindings: {thumbrestBindings}\nBackup: {bindings.BackupPath}"
					: $"FAIL: verification values did not match\n" +
					  $"Recentre: {string.Join(", ", reloadedRecentre.Entries.Select(entry => entry.DevicePath + ":" + entry.InputComponent))}\n" +
					  $"Thumbrest normal controls: {booleanControls.Count(control => control.DevicePath.Contains("/input/thumbrest", StringComparison.OrdinalIgnoreCase))}\n" +
					  $"Duplicate labels: {string.Join(", ", booleanControls.GroupBy(control => control.DisplayName, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key + " x" + group.Count()))}\n" +
					  $"Configured bindings: {thumbrestBindings}");
        }
        catch (Exception ex)
        {
            File.WriteAllText(reportPath, "FAIL: " + ex);
        }
    }
}
