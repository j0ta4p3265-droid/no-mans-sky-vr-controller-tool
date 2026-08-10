using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace NMSOpenCompositeConfigurator;

internal sealed record ActionDefinition(string FullName, string SetName, string Type, string DisplayName)
{
    public override string ToString() => DisplayName;
}

internal sealed record BindingEntry(
    string DevicePath,
    string Mode,
    string InputComponent,
    string OutputAction,
    string RequiredType,
    JsonObject SourceNode,
    JsonObject InputNode)
{
    public string PhysicalDisplay => FormatControl(DevicePath, InputComponent);
    public string ModeDisplay => Mode.Replace('_', ' ');
    public override string ToString() => PhysicalDisplay;

    private static string FormatControl(string path, string component)
    {
        var hand = path.Contains("/hand/left/", StringComparison.OrdinalIgnoreCase) ? "Left" : "Right";
        var control = path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.ToLowerInvariant() ?? "control";
        var part = component.ToLowerInvariant();

        if (control is "a" or "b" or "x" or "y")
            return part == "touch"
                ? $"{control.ToUpperInvariant()} button touch"
                : $"{control.ToUpperInvariant()} button";

        if (control == "joystick")
        {
            return part switch
            {
                "position" => $"{hand} stick",
                "click" => $"{hand} stick click",
                "touch" => $"{hand} stick touch",
                "north" => $"{hand} stick up",
                "south" => $"{hand} stick down",
                "east" => $"{hand} stick right",
                "west" => $"{hand} stick left",
                _ => $"{hand} stick {part.Replace('_', ' ')}"
            };
        }

        if (control == "grip")
            return part is "value" or "pull" ? $"{hand} grip pressure" : $"{hand} grip";
        if (control == "trigger")
            return part switch
            {
                "value" or "pull" => $"{hand} trigger pressure",
                "touch" => $"{hand} trigger touch",
                _ => $"{hand} trigger"
            };

        return $"{hand} {control.Replace('_', ' ')} {part.Replace('_', ' ')}";
    }
}

internal sealed record ActionBindingGroup(string DisplayName, IReadOnlyList<BindingEntry> Entries)
{
    public string CurrentControlsDisplay => string.Join(" + ", Entries
        .Select(entry => entry.PhysicalDisplay)
        .Distinct(StringComparer.OrdinalIgnoreCase));
}

internal sealed record PhysicalControlOption(
    string DevicePath,
    string Mode,
    string InputComponent,
    string RequiredType,
    string DisplayName,
    JsonObject Parameters)
{
    public override string ToString() => DisplayName;
}

internal sealed class NmsBindingDocument
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    private readonly JsonObject _touchRoot;
    private readonly Dictionary<string, ActionDefinition> _actions;
    private readonly Dictionary<string, string> _setDisplayNames;

    public string TouchPath { get; }
    public string BackupPath => TouchPath + ".opencomposite-nms-original.bak";

    private NmsBindingDocument(
        string touchPath,
        JsonObject touchRoot,
        Dictionary<string, ActionDefinition> actions,
        Dictionary<string, string> setDisplayNames)
    {
        TouchPath = touchPath;
        _touchRoot = touchRoot;
        _actions = actions;
        _setDisplayNames = setDisplayNames;
    }

    public static NmsBindingDocument Load(string gameFolder)
    {
        var inputFolder = Path.Combine(gameFolder, "GAMEDATA", "INPUT");
        var actionsPath = Path.Combine(inputFolder, "ACTIONS.JSON");
        var touchPath = Path.Combine(inputFolder, "TOUCH.JSON");

        var actionsRoot = JsonNode.Parse(File.ReadAllText(actionsPath))?.AsObject()
            ?? throw new InvalidDataException("ACTIONS.JSON is invalid.");
        var touchRoot = JsonNode.Parse(File.ReadAllText(touchPath))?.AsObject()
            ?? throw new InvalidDataException("TOUCH.JSON is invalid.");

        var localization = actionsRoot["localization"]?.AsArray()
            .Select(node => node?.AsObject())
            .FirstOrDefault(node => node?["language_tag"]?.GetValue<string>() == "en_US");

        var setDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var setNode in actionsRoot["action_sets"]?.AsArray() ?? new JsonArray())
        {
            var name = setNode?["name"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(name))
                continue;
            setDisplayNames[name] = localization?[name]?.GetValue<string>() ?? Humanize(name.Split('/').Last());
        }

        var actions = new Dictionary<string, ActionDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var actionNode in actionsRoot["actions"]?.AsArray() ?? new JsonArray())
        {
            var fullName = actionNode?["name"]?.GetValue<string>();
            var type = actionNode?["type"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(type))
                continue;
            var marker = fullName.IndexOf("/in/", StringComparison.OrdinalIgnoreCase);
            if (marker < 0)
                continue;
            var setName = fullName[..marker];
            var display = localization?[fullName]?.GetValue<string>() ?? Humanize(fullName[(marker + 4)..]);
            display = AddContextualActionName(fullName, display);
            actions[fullName] = new ActionDefinition(fullName, setName, type, display);
        }

        return new NmsBindingDocument(touchPath, touchRoot, actions, setDisplayNames);
    }

    public IReadOnlyList<(string Name, string Display)> GetContexts()
    {
        var result = new List<(string Name, string Display)>();
        var bindings = _touchRoot["bindings"]?.AsObject();
        if (bindings is null)
            return result;
        foreach (var pair in bindings)
        {
            var display = _setDisplayNames.TryGetValue(pair.Key, out var localized)
                ? localized
                : Humanize(pair.Key.Split('/').Last());
            result.Add((pair.Key, display));
        }
        return result.OrderBy(item => item.Display).ToList();
    }

    public IReadOnlyList<BindingEntry> GetBindings(string context)
    {
        var result = new List<BindingEntry>();
        var sources = _touchRoot["bindings"]?[context]?["sources"]?.AsArray();
        if (sources is null)
            return result;

        foreach (var sourceNode in sources)
        {
            var source = sourceNode?.AsObject();
            var path = source?["path"]?.GetValue<string>() ?? "Unknown input";
            var mode = source?["mode"]?.GetValue<string>() ?? "unknown";
            var inputs = source?["inputs"]?.AsObject();
            if (inputs is null)
                continue;
            foreach (var pair in inputs)
            {
                if (pair.Value is not JsonObject inputNode)
                    continue;
                var output = inputNode["output"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(output))
                    continue;
                var requiredType = _actions.TryGetValue(output, out var action)
                    ? action.Type
                    : TypeForInput(pair.Key);
                result.Add(new BindingEntry(path, mode, pair.Key, output, requiredType, source!, inputNode));
            }
        }
        return result;
    }

    public IReadOnlyList<ActionBindingGroup> GetActionBindingGroups(string context)
        => GetActionBindingGroups(new[] { context });

    public IReadOnlyList<ActionBindingGroup> GetActionBindingGroups(IEnumerable<string> contexts)
    {
        return contexts.SelectMany(GetBindings)
            .GroupBy(entry => NormalizeActionDisplay(GetActionDisplay(entry.OutputAction)), StringComparer.OrdinalIgnoreCase)
            .Select(group => new ActionBindingGroup(
                group.Key,
                group.OrderBy(entry => entry.PhysicalDisplay).ToList()))
            .OrderBy(group => group.DisplayName)
            .ToList();
    }

    public IReadOnlyList<PhysicalControlOption> GetPhysicalControls(string requiredType)
    {
        var bindings = _touchRoot["bindings"]?.AsObject();
        if (bindings is null)
            return Array.Empty<PhysicalControlOption>();

        var result = new Dictionary<string, PhysicalControlOption>(StringComparer.OrdinalIgnoreCase);
        foreach (var context in bindings)
        {
            foreach (var entry in GetBindings(context.Key).Where(entry =>
                         entry.RequiredType.Equals(requiredType, StringComparison.OrdinalIgnoreCase)
						 && !entry.DevicePath.Contains("/input/thumbrest", StringComparison.OrdinalIgnoreCase)))
            {
                var key = $"{entry.DevicePath}|{entry.InputComponent}|{entry.RequiredType}";
                if (result.ContainsKey(key))
                    continue;
                var parameters = entry.SourceNode["parameters"]?.DeepClone() as JsonObject ?? new JsonObject();
                result[key] = new PhysicalControlOption(
                    entry.DevicePath,
                    entry.Mode,
                    entry.InputComponent,
                    entry.RequiredType,
                    entry.PhysicalDisplay,
                    parameters);
            }
        }
        return result.Values.OrderBy(option => option.DisplayName).ToList();
    }

    public IReadOnlyList<ActionDefinition> GetCompatibleActions(string context, string requiredType)
    {
        return _actions.Values
            .Where(action => action.SetName.Equals(context, StringComparison.OrdinalIgnoreCase)
                && action.Type.Equals(requiredType, StringComparison.OrdinalIgnoreCase))
            .OrderBy(action => action.DisplayName)
            .ToList();
    }

    public string GetActionDisplay(string fullName)
    {
        return _actions.TryGetValue(fullName, out var action)
            ? action.DisplayName
            : Humanize(fullName.Split('/').Last());
    }

    public void Remap(BindingEntry entry, ActionDefinition action)
    {
        entry.InputNode["output"] = action.FullName;
    }

    public void Rebind(BindingEntry entry, PhysicalControlOption control)
    {
        if (!entry.RequiredType.Equals(control.RequiredType, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The selected control is not compatible with this game action.");

        if (entry.DevicePath.Equals(control.DevicePath, StringComparison.OrdinalIgnoreCase)
            && entry.Mode.Equals(control.Mode, StringComparison.OrdinalIgnoreCase)
            && entry.InputComponent.Equals(control.InputComponent, StringComparison.OrdinalIgnoreCase))
            return;

        if (entry.SourceNode.Parent is not JsonArray sources
            || entry.SourceNode["inputs"] is not JsonObject oldInputs)
            throw new InvalidDataException("The selected binding no longer exists in TOUCH.JSON.");

        var movedInput = entry.InputNode.DeepClone();
        oldInputs.Remove(entry.InputComponent);
        if (oldInputs.Count == 0)
            sources.Remove(entry.SourceNode);

        var newSource = new JsonObject
        {
            ["inputs"] = new JsonObject { [control.InputComponent] = movedInput },
            ["mode"] = control.Mode,
            ["parameters"] = control.Parameters.DeepClone(),
            ["path"] = control.DevicePath
        };
        sources.Add(newSource);
    }

    public int RebindMatchingActions(BindingEntry selectedEntry, PhysicalControlOption control)
    {
        var actionSuffix = ActionSuffix(selectedEntry.OutputAction);
        var matches = GetAllBindings()
            .Where(entry => ActionSuffix(entry.OutputAction).Equals(actionSuffix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var match in matches)
            Rebind(match, control);
        return matches.Count;
    }

    public int ConfigureThumbrestRecentre(bool enableLeft, bool enableRight)
    {
        var configuredBindings = 0;
        foreach (var context in GetContexts())
        {
            var sources = _touchRoot["bindings"]?[context.Name]?["sources"]?.AsArray();
            if (sources is null)
                continue;

            var actionGroups = GetBindings(context.Name)
                .Where(entry => ActionSuffix(entry.OutputAction) is "vr_recentre1" or "vr_recentre2")
                .GroupBy(entry => entry.OutputAction, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var group in actionGroups)
            {
                var template = group.First().InputNode.DeepClone();
                foreach (var entry in group.ToList())
                {
                    if (entry.SourceNode["inputs"] is not JsonObject inputs)
                        continue;
                    inputs.Remove(entry.InputComponent);
                    if (inputs.Count == 0)
                        sources.Remove(entry.SourceNode);
                }

                var controls = new List<(string Path, string Mode, string Input)>();
                if (enableLeft)
                    controls.Add(("/user/hand/left/input/thumbrest", "button", "touch"));
                if (enableRight)
                    controls.Add(("/user/hand/right/input/thumbrest", "button", "touch"));
                if (controls.Count == 0)
                {
                    var primary = ActionSuffix(group.Key) == "vr_recentre1";
                    controls.Add((
                        primary ? "/user/hand/left/input/joystick" : "/user/hand/right/input/joystick",
                        "joystick",
                        "click"));
                }

                foreach (var control in controls)
                {
                    sources.Add(new JsonObject
                    {
                        ["inputs"] = new JsonObject { [control.Input] = template.DeepClone() },
                        ["mode"] = control.Mode,
                        ["parameters"] = new JsonObject(),
                        ["path"] = control.Path
                    });
                    configuredBindings++;
                }
            }
        }
        return configuredBindings;
    }

    public void SaveWithBackup()
    {
        if (!File.Exists(BackupPath))
            File.Copy(TouchPath, BackupPath, false);

        var temporary = TouchPath + ".tmp";
        File.WriteAllText(temporary, _touchRoot.ToJsonString(WriteOptions));
        File.Move(temporary, TouchPath, true);
    }

    public bool RestoreBackup()
    {
        if (!File.Exists(BackupPath))
            return false;
        File.Copy(BackupPath, TouchPath, true);
        return true;
    }

    private static string TypeForInput(string input)
    {
        return input.ToLowerInvariant() switch
        {
            "position" => "vector2",
            "value" or "pull" => "vector1",
            _ => "boolean"
        };
    }

    private static string Humanize(string value)
    {
        value = value.Replace('_', ' ').Trim();
        return string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    }

    private static string AddContextualActionName(string fullName, string officialDisplay)
    {
        var normalized = fullName.ToLowerInvariant();
        if ((normalized.StartsWith("/actions/frontend_right/in/", StringComparison.Ordinal)
                || normalized.StartsWith("/actions/frontend_left/in/", StringComparison.Ordinal))
            && normalized.EndsWith("/select", StringComparison.Ordinal))
            return "Confirm (Menus) / Move & Stack Items";

        if (normalized == "/actions/frontend/in/confirmdelete")
            return "Confirm Delete";

        if ((normalized.StartsWith("/actions/frontend_right/in/", StringComparison.Ordinal)
                || normalized.StartsWith("/actions/frontend_left/in/", StringComparison.Ordinal))
            && normalized.EndsWith("/menu_canceldelete", StringComparison.Ordinal))
            return "Cancel Delete";

        if (normalized == "/actions/onfootcontrols/in/vr_snaparound")
            return "Turn 180°";

        return officialDisplay;
    }

    private static string NormalizeActionDisplay(string display)
        => Regex.Replace(display, @"\s*\(Button\s+\d+\)\s*$", string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private IEnumerable<BindingEntry> GetAllBindings()
    {
        foreach (var context in GetContexts())
        {
            foreach (var entry in GetBindings(context.Name))
                yield return entry;
        }
    }

    private static string ActionSuffix(string action)
    {
        var marker = action.LastIndexOf("/in/", StringComparison.OrdinalIgnoreCase);
        return marker >= 0 ? action[(marker + 4)..].ToLowerInvariant() : action.ToLowerInvariant();
    }

    private static PhysicalControlOption MakeControl(
        string path, string mode, string component, string type, string display)
        => new(path, mode, component, type, display, new JsonObject());

    private static void AddControl(
        Dictionary<string, PhysicalControlOption> controls,
        PhysicalControlOption control)
    {
        var key = $"{control.DevicePath}|{control.InputComponent}|{control.RequiredType}";
        controls.TryAdd(key, control);
    }
}
