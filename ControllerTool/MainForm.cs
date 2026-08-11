namespace NMSOpenCompositeConfigurator;

internal sealed class MainForm : Form
{
    private static readonly Color Background = Color.FromArgb(24, 25, 30);
    private static readonly Color Surface = Color.FromArgb(32, 34, 40);
    private static readonly Color SurfaceRaised = Color.FromArgb(42, 45, 53);
    private static readonly Color Accent = Color.FromArgb(75, 170, 255);
    private static readonly Color Success = Color.FromArgb(65, 170, 95);
    private static readonly Color TextPrimary = Color.FromArgb(238, 241, 246);
    private static readonly Color TextMuted = Color.FromArgb(160, 168, 180);

    private readonly TextBox _gamePath = new();
    private readonly Label _status = new();
    private readonly NumericUpDown _leftDeadZone = MakeNumber(0m, 0.75m, 0.01m);
    private readonly NumericUpDown _rightDeadZone = MakeNumber(0m, 0.75m, 0.01m);
    private readonly NumericUpDown _rightSensitivity = MakeNumber(0.10m, 2.00m, 0.05m);
    private readonly ComboBox _context = new();
    private readonly ComboBox _handLayout = new();
    private readonly CheckBox _showAdvancedContexts = new();
    private readonly TextBox _bindingSearch = new();
    private readonly DataGridView _bindingsGrid = new();
    private readonly ComboBox _bindingToReplace = new();
    private readonly ComboBox _newControl = new();
    private readonly CheckBox _applyAllContexts = new();
    private readonly Button _applyBinding = new();
    private readonly Button _saveAll = new();
    private readonly TabControl _tabs = new();
    private readonly CheckBox _enableLeftThumbrestTripleTap = new();
    private readonly CheckBox _enableRightThumbrestTripleTap = new();
    private readonly NumericUpDown _thumbrestDoubleTapWindow = MakeNumber(0.20m, 0.80m, 0.05m);
    private readonly AppSettings _settings = AppSettings.Load();

    private string? _loadedFolder;
    private IniDocument? _ini;
    private NmsBindingDocument? _bindings;
    private List<ContextItem> _allContexts = new();
    private List<ActionBindingGroup> _visibleActionGroups = new();
    private bool _thumbrestWasEnabled;
    private bool _loadingHandLayoutPreference;

    private static readonly string[] BasicContextOrder =
    {
        "/actions/onfootcontrols",
        "/actions/onfootquickmenu",
        "/actions/shipcontrols",
        "/actions/shipquickmenu",
        "/actions/vehiclemode",
        "/actions/vehiclequickmenu",
        "/actions/frontend",
        "/actions/galacticmap",
        "/actions/photomodemvcam",
        "/actions/photomodemenu",
        "/actions/buildmenuselectionmode",
        "/actions/buildmenuplacementmode",
        "/actions/buildmenubiggsselect",
        "/actions/buildmenubiggsplace"
    };

    public MainForm()
    {
        Text = "No Man's Sky VR Controller Tool";
		try
		{
			Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
		}
		catch
		{
			// The embedded executable icon is cosmetic; the configurator can still run without it.
		}
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(980, 700);
        Size = new Size(1180, 860);
        BackColor = Background;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9.5f);

        BuildUi();
        LoadHandLayoutPreference();
        Shown += (_, _) => TryAutoDetect();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(18, 18, 18, 10),
            BackColor = Background
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Background };
        header.Controls.Add(new Label
        {
            Text = "Controller Tool",
            Font = new Font("Segoe UI Semibold", 20f),
            ForeColor = TextPrimary,
            AutoSize = true,
            Location = new Point(2, 3)
        });
        header.Controls.Add(new Label
        {
            Text = "Controller bindings and OpenComposite input settings for No Man's Sky VR",
            ForeColor = TextMuted,
            AutoSize = true,
            Location = new Point(5, 43)
        });
        root.Controls.Add(header, 0, 0);

        var pathBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2,
            Padding = new Padding(12, 8, 12, 4),
            BackColor = Surface
        };
        pathBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        pathBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        pathBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165));
        pathBar.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        pathBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        pathBar.Controls.Add(MakeLabel("NMS game folder", false), 0, 0);
        StyleTextBox(_gamePath);
        _gamePath.Dock = DockStyle.Fill;
        pathBar.Controls.Add(_gamePath, 1, 0);
        var browse = MakeButton("Browse…", Accent);
        browse.Click += (_, _) => BrowseForGame();
        pathBar.Controls.Add(browse, 2, 0);
        _saveAll.Text = "Save";
        StyleButton(_saveAll, Success);
        _saveAll.Enabled = false;
        _saveAll.Click += (_, _) => SaveAll();
        pathBar.Controls.Add(_saveAll, 3, 0);
        _status.Text = "Select the No Man's Sky folder to begin.";
        _status.ForeColor = TextMuted;
        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        pathBar.SetColumnSpan(_status, 4);
        pathBar.Controls.Add(_status, 0, 1);
        root.Controls.Add(pathBar, 0, 1);

        _tabs.Dock = DockStyle.Fill;
        _tabs.Appearance = TabAppearance.Normal;
        _tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        _tabs.SizeMode = TabSizeMode.Fixed;
        _tabs.ItemSize = new Size(125, 34);
        _tabs.BackColor = Surface;
        _tabs.ForeColor = TextPrimary;
        _tabs.DrawItem += (_, e) =>
        {
            var selected = e.Index == _tabs.SelectedIndex;
            using var background = new SolidBrush(selected ? SurfaceRaised : Background);
            using var foreground = new SolidBrush(selected ? Color.White : TextMuted);
            e.Graphics.FillRectangle(background, e.Bounds);
            var text = _tabs.TabPages[e.Index].Text;
            TextRenderer.DrawText(e.Graphics, text, Font, e.Bounds, foreground.Color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
        _tabs.TabPages.Add(BuildSettingsTab());
        _tabs.TabPages.Add(BuildBindingsTab());
        _tabs.TabPages.Add(BuildExperimentalTab());
        root.Controls.Add(_tabs, 0, 2);
    }

    private TabPage BuildSettingsTab()
    {
        var page = MakePage("Settings");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            Padding = new Padding(26),
            BackColor = Surface
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        page.Controls.Add(panel);

        AddSectionHeader(panel, "Thumbstick tuning", "These values are read by OpenComposite when the game starts.", 0);
        AddSetting(panel, "Left stick dead zone", "Ignore small movement near the centre of the movement stick.", _leftDeadZone, 2);
        AddSetting(panel, "Right stick dead zone", "Ignore small movement near the centre of the camera stick.", _rightDeadZone, 3);
        AddSetting(panel, "Right stick sensitivity", "Multiplier used for smooth camera turning.", _rightSensitivity, 4);

        _handLayout.Items.AddRange(new object[] { "Right-handed", "Left-handed" });
        StyleCombo(_handLayout);
        _handLayout.SelectedIndexChanged += (_, _) =>
        {
            SaveHandLayoutPreference();
            PopulateContexts();
        };
        AddSetting(panel, "Controller hand layout",
            "Choose the same dominant-hand layout used in No Man's Sky. The configurator merges the matching hidden action sets into each binding list.",
            _handLayout, 5);

        var preset = MakeButton("Use SteamVR / Touch defaults", Accent);
        preset.AutoSize = true;
        preset.Click += (_, _) =>
        {
            _leftDeadZone.Value = 0m;
            _rightDeadZone.Value = 0m;
            _rightSensitivity.Value = 1m;
            SetStatus("SteamVR / Oculus Touch defaults selected. Click Save changes.");
        };
        panel.Controls.Add(preset, 1, 6);

        var note = MakeLabel("Game restart required after changing these values.", true);
        note.ForeColor = Color.FromArgb(245, 190, 75);
        note.Margin = new Padding(0, 18, 0, 0);
        panel.Controls.Add(note, 2, 6);
        return page;
    }

    private TabPage BuildBindingsTab()
    {
        var page = MakePage("Bindings");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(18),
            BackColor = Surface
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        page.Controls.Add(layout);

        var filters = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, BackColor = Surface };
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 225));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        filters.Controls.Add(MakeLabel("Context", false), 0, 0);
        StyleCombo(_context);
        _context.SelectedIndexChanged += (_, _) => RefreshBindings();
        filters.Controls.Add(_context, 1, 0);
        _showAdvancedContexts.Text = "Show advanced contexts";
        _showAdvancedContexts.ForeColor = TextMuted;
        _showAdvancedContexts.AutoSize = true;
        _showAdvancedContexts.Anchor = AnchorStyles.Left;
        _showAdvancedContexts.Margin = new Padding(12, 5, 4, 5);
        _showAdvancedContexts.CheckedChanged += (_, _) => PopulateContexts();
        filters.Controls.Add(_showAdvancedContexts, 2, 0);
        filters.Controls.Add(MakeLabel("Search", false), 3, 0);
        StyleTextBox(_bindingSearch);
        _bindingSearch.TextChanged += (_, _) => RefreshBindings();
        _bindingSearch.Dock = DockStyle.Fill;
        filters.Controls.Add(_bindingSearch, 4, 0);
        layout.Controls.Add(filters, 0, 0);

        _bindingsGrid.Dock = DockStyle.Fill;
        _bindingsGrid.ReadOnly = true;
        _bindingsGrid.AllowUserToAddRows = false;
        _bindingsGrid.AllowUserToDeleteRows = false;
        _bindingsGrid.AllowUserToResizeRows = false;
        _bindingsGrid.MultiSelect = false;
        _bindingsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _bindingsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _bindingsGrid.BackgroundColor = Background;
        _bindingsGrid.BorderStyle = BorderStyle.None;
        _bindingsGrid.GridColor = Color.FromArgb(54, 57, 66);
        _bindingsGrid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = SurfaceRaised,
            ForeColor = TextPrimary,
            SelectionBackColor = SurfaceRaised,
            Font = new Font("Segoe UI Semibold", 9f)
        };
        _bindingsGrid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Surface,
            ForeColor = TextPrimary,
            SelectionBackColor = Color.FromArgb(45, 92, 130),
            SelectionForeColor = Color.White
        };
        _bindingsGrid.EnableHeadersVisualStyles = false;
        _bindingsGrid.Columns.Add("action", "Game action");
        _bindingsGrid.Columns.Add("controls", "Current control(s)");
        _bindingsGrid.Columns[0].FillWeight = 46;
        _bindingsGrid.Columns[1].FillWeight = 54;
        _bindingsGrid.SelectionChanged += (_, _) => LoadSelectedBinding();
        layout.Controls.Add(_bindingsGrid, 0, 1);

        var editor = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 3,
            Padding = new Padding(0, 8, 0, 0),
            BackColor = Surface
        };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        editor.Controls.Add(MakeLabel("Current control to replace", false), 0, 0);
        StyleCombo(_bindingToReplace);
        _bindingToReplace.DisplayMember = nameof(BindingEntry.PhysicalDisplay);
        _bindingToReplace.SelectedIndexChanged += (_, _) => PopulatePhysicalControls();
        editor.Controls.Add(_bindingToReplace, 0, 1);
        editor.Controls.Add(MakeLabel("New control", false), 1, 0);
        StyleCombo(_newControl);
        editor.Controls.Add(_newControl, 1, 1);
        _applyBinding.Text = "Apply & save";
        StyleButton(_applyBinding, Accent);
        _applyBinding.Enabled = false;
        _applyBinding.Click += (_, _) => ApplySelectedBinding();
        editor.Controls.Add(_applyBinding, 2, 1);
        var restore = MakeButton("Restore original", Color.FromArgb(160, 75, 70));
        restore.Click += (_, _) => RestoreOriginalBindings();
        editor.Controls.Add(restore, 3, 1);
        _applyAllContexts.Text = "Apply this action to every context where it exists";
        _applyAllContexts.ForeColor = TextMuted;
        _applyAllContexts.AutoSize = true;
        _applyAllContexts.Anchor = AnchorStyles.Left;
        _applyAllContexts.Margin = new Padding(5, 2, 5, 2);
        editor.SetColumnSpan(_applyAllContexts, 4);
        editor.Controls.Add(_applyAllContexts, 0, 2);
        layout.Controls.Add(editor, 0, 2);
        return page;
    }

    private TabPage BuildExperimentalTab()
    {
        var page = MakePage("Experimental");
        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 360,
            Padding = new Padding(26),
            BackColor = SurfaceRaised,
            ColumnCount = 3,
            RowCount = 6
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var title = MakeLabel("Capacitive thumbrest gestures", false);
        title.Font = new Font("Segoe UI Semibold", 13f);
        title.Dock = DockStyle.Fill;
        card.SetColumnSpan(title, 3);
        card.Controls.Add(title, 0, 0);
        var description = MakeLabel(
            "Tap either enabled capacitive thumbrest three times to recenter the VR view. " +
            "This experimental macro replaces LS + RS, but NMS keeps showing its original LS + RS glyph.", true);
        description.ForeColor = TextMuted;
        card.SetColumnSpan(description, 3);
        card.Controls.Add(description, 0, 1);
        _enableLeftThumbrestTripleTap.Text = "Left thumbrest (beside X/Y)";
        _enableLeftThumbrestTripleTap.ForeColor = TextPrimary;
        _enableLeftThumbrestTripleTap.AutoSize = true;
        _enableLeftThumbrestTripleTap.Anchor = AnchorStyles.Left;
        card.SetColumnSpan(_enableLeftThumbrestTripleTap, 3);
        card.Controls.Add(_enableLeftThumbrestTripleTap, 0, 2);

        _enableRightThumbrestTripleTap.Text = "Right thumbrest (beside A/B)";
        _enableRightThumbrestTripleTap.ForeColor = TextPrimary;
        _enableRightThumbrestTripleTap.AutoSize = true;
        _enableRightThumbrestTripleTap.Anchor = AnchorStyles.Left;
        card.SetColumnSpan(_enableRightThumbrestTripleTap, 3);
        card.Controls.Add(_enableRightThumbrestTripleTap, 0, 3);

        card.Controls.Add(MakeLabel("Maximum time between taps", false), 0, 4);
        _thumbrestDoubleTapWindow.DecimalPlaces = 2;
        _thumbrestDoubleTapWindow.Anchor = AnchorStyles.Left;
        card.Controls.Add(_thumbrestDoubleTapWindow, 1, 4);
        card.Controls.Add(MakeLabel("seconds", false), 2, 4);

        var note = MakeLabel(
            "Requires the current No Man's Sky OpenComposite runtime from the main download. " +
            "This companion tool never installs or replaces game DLLs. Close the game before saving changes.", true);
        note.ForeColor = Color.FromArgb(245, 190, 75);
        card.SetColumnSpan(note, 3);
        card.Controls.Add(note, 0, 5);
        page.Padding = new Padding(24);
        page.Controls.Add(card);
        return page;
    }

    private void TryAutoDetect()
    {
        var detected = GameLocator.FindNoMansSky(_settings.GameFolder);
        if (detected is null)
        {
            SetStatus("Automatic detection failed. Click Browse and select the No Man's Sky folder.", true);
            return;
        }
        _gamePath.Text = detected;
        LoadGameFolder(detected);
    }

    private void LoadHandLayoutPreference()
    {
        _loadingHandLayoutPreference = true;
        try
        {
            var saved = _settings.HandLayout;
            _handLayout.SelectedIndex = saved.Equals("left", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        }
        catch
        {
            _handLayout.SelectedIndex = 0;
        }
        finally
        {
            _loadingHandLayoutPreference = false;
        }
    }

    private void SaveHandLayoutPreference()
    {
        if (_loadingHandLayoutPreference || _handLayout.SelectedIndex < 0)
            return;
        try
        {
            _settings.HandLayout = _handLayout.SelectedIndex == 1 ? "left" : "right";
            _settings.Save();
        }
        catch
        {
            // The preference is optional. A settings write failure falls back to right-handed.
        }
    }

    internal void SelectPreviewTab(string tabName)
    {
        for (var index = 0; index < _tabs.TabPages.Count; index++)
        {
            if (_tabs.TabPages[index].Text.Equals(tabName, StringComparison.OrdinalIgnoreCase))
            {
                _tabs.SelectedIndex = index;
                break;
            }
        }
    }

    private void BrowseForGame()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the No Man's Sky folder (the folder containing Binaries and GAMEDATA)",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
            InitialDirectory = Directory.Exists(_gamePath.Text) ? _gamePath.Text : string.Empty
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        if (GameLocator.TryNormalizeGameFolder(dialog.SelectedPath, out var folder))
        {
            _gamePath.Text = folder;
            LoadGameFolder(folder);
        }
        else
        {
            _gamePath.Text = dialog.SelectedPath;
            LoadGameFolder(dialog.SelectedPath);
        }
    }

    private void LoadGameFolder(string folder)
    {
        if (!GameLocator.IsValidGameFolder(folder))
        {
            SetStatus("That folder does not contain a complete No Man's Sky installation.", true);
            _saveAll.Enabled = false;
            return;
        }

        try
        {
            _loadedFolder = folder;
            _gamePath.Text = folder;
            _ini = IniDocument.Load(Path.Combine(folder, "Binaries", "opencomposite.ini"));
            _bindings = NmsBindingDocument.Load(folder);
            _leftDeadZone.Value = Clamp(_ini.GetDecimal("leftDeadZoneSize", 0m), _leftDeadZone);
            _rightDeadZone.Value = Clamp(_ini.GetDecimal("rightDeadZoneSize", 0m), _rightDeadZone);
            _rightSensitivity.Value = Clamp(_ini.GetDecimal("rightJoystickScale", 1m), _rightSensitivity);
            _enableLeftThumbrestTripleTap.Checked = _ini.GetBoolean("enableThumbrestDoubleTap", false);
            _enableRightThumbrestTripleTap.Checked = _ini.GetBoolean("enableRightThumbrestDoubleTap", false);
            _thumbrestWasEnabled = _enableLeftThumbrestTripleTap.Checked || _enableRightThumbrestTripleTap.Checked;
            var thumbrestSeconds = _ini.GetDecimal("thumbrestDoubleTapWindowMs", 450m) / 1000m;
            _thumbrestDoubleTapWindow.Value = Clamp(thumbrestSeconds, _thumbrestDoubleTapWindow);

            _allContexts = _bindings.GetContexts()
                .Select(item => new ContextItem(new[] { item.Name }, item.Display))
                .ToList();
            _showAdvancedContexts.Checked = false;
            PopulateContexts();
            _saveAll.Enabled = true;
            RememberGameFolder(folder);
            SetStatus("No Man's Sky loaded. Changes are not written until you click Save changes.");
        }
        catch (Exception ex)
        {
            SetStatus("Could not load the game configuration: " + ex.Message, true);
            _saveAll.Enabled = false;
        }
    }

    private void RememberGameFolder(string folder)
    {
        try
        {
            _settings.GameFolder = folder;
            _settings.Save();
        }
        catch
        {
            // The game can still be configured when the optional preference cannot be saved.
        }
    }

    private int FindPreferredContext()
    {
        for (var i = 0; i < _context.Items.Count; i++)
        {
            if (_context.Items[i] is ContextItem item
                && item.PrimaryName.Equals("/actions/OnFootControls", StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return 0;
    }

    private void PopulateContexts()
    {
        var previousName = (_context.SelectedItem as ContextItem)?.PrimaryName;
        _context.BeginUpdate();
        _context.Items.Clear();

        IEnumerable<ContextItem> contexts;
        if (_showAdvancedContexts.Checked)
        {
            var oppositeHandSuffix = _handLayout.SelectedIndex == 1 ? "_right" : "_left";
            contexts = _allContexts
                .Where(item => !item.PrimaryName.EndsWith(oppositeHandSuffix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.Display);
        }
        else
        {
            var byName = _allContexts.ToDictionary(item => item.PrimaryName, StringComparer.OrdinalIgnoreCase);
            var handSuffix = _handLayout.SelectedIndex == 1 ? "_left" : "_right";
            contexts = BasicContextOrder
                .Where(byName.ContainsKey)
                .Select(name =>
                {
                    var names = new List<string> { byName[name].PrimaryName };
                    if (byName.TryGetValue(name + handSuffix, out var handedContext))
                        names.Add(handedContext.PrimaryName);
                    return new ContextItem(names, byName[name].Display);
                });
        }

        foreach (var context in contexts)
            _context.Items.Add(context);
        _context.EndUpdate();

        if (_context.Items.Count == 0)
        {
            RefreshBindings();
            return;
        }

        var restoredIndex = -1;
        if (previousName is not null)
        {
            for (var index = 0; index < _context.Items.Count; index++)
            {
                if (_context.Items[index] is ContextItem item
                    && item.PrimaryName.Equals(previousName, StringComparison.OrdinalIgnoreCase))
                {
                    restoredIndex = index;
                    break;
                }
            }
        }
        _context.SelectedIndex = restoredIndex >= 0 ? restoredIndex : FindPreferredContext();
    }

    private void RefreshBindings()
    {
        var previouslySelectedAction = _bindingsGrid.SelectedRows.Count > 0
            && _bindingsGrid.SelectedRows[0].Index >= 0
            && _bindingsGrid.SelectedRows[0].Index < _visibleActionGroups.Count
                ? _visibleActionGroups[_bindingsGrid.SelectedRows[0].Index].DisplayName
                : null;
        _bindingsGrid.Rows.Clear();
        _bindingToReplace.DataSource = null;
        _newControl.DataSource = null;
        _applyBinding.Enabled = false;
        if (_bindings is null || _context.SelectedItem is not ContextItem context)
            return;

        var search = _bindingSearch.Text.Trim();
        _visibleActionGroups = _bindings.GetActionBindingGroups(context.Names)
            .Where(group => search.Length == 0
                || group.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || group.CurrentControlsDisplay.Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var group in _visibleActionGroups)
            _bindingsGrid.Rows.Add(group.DisplayName, group.CurrentControlsDisplay);
        if (_bindingsGrid.Rows.Count > 0)
        {
            var selectedIndex = previouslySelectedAction is null
                ? 0
                : _visibleActionGroups.FindIndex(group =>
                    group.DisplayName.Equals(previouslySelectedAction, StringComparison.OrdinalIgnoreCase));
            _bindingsGrid.Rows[Math.Max(0, selectedIndex)].Selected = true;
        }
    }

    private void LoadSelectedBinding()
    {
        _bindingToReplace.DataSource = null;
        _newControl.DataSource = null;
        _applyBinding.Enabled = false;
        if (_bindings is null || _bindingsGrid.SelectedRows.Count == 0)
            return;
        var index = _bindingsGrid.SelectedRows[0].Index;
        if (index < 0 || index >= _visibleActionGroups.Count)
            return;

        _bindingToReplace.DataSource = _visibleActionGroups[index].Entries.ToList();
        if (_bindingToReplace.Items.Count > 0)
            _bindingToReplace.SelectedIndex = 0;
    }

    private void PopulatePhysicalControls()
    {
        _newControl.DataSource = null;
        _applyBinding.Enabled = false;
        if (_bindings is null || _bindingToReplace.SelectedItem is not BindingEntry entry)
            return;

        var controls = _bindings.GetPhysicalControls(entry.RequiredType).ToList();
        _newControl.DataSource = controls;
        var currentIndex = controls.FindIndex(control =>
            control.DevicePath.Equals(entry.DevicePath, StringComparison.OrdinalIgnoreCase)
            && control.Mode.Equals(entry.Mode, StringComparison.OrdinalIgnoreCase)
            && control.InputComponent.Equals(entry.InputComponent, StringComparison.OrdinalIgnoreCase));
        if (currentIndex >= 0)
            _newControl.SelectedIndex = currentIndex;
        _applyBinding.Enabled = controls.Count > 0;
    }

    private void ApplySelectedBinding()
    {
        if (_bindings is null
            || _bindingToReplace.SelectedItem is not BindingEntry entry
            || _newControl.SelectedItem is not PhysicalControlOption control)
            return;
        var actionName = _bindings.GetActionDisplay(entry.OutputAction);
        var oldControl = entry.PhysicalDisplay;
        var changedBindings = _applyAllContexts.Checked
            ? _bindings.RebindMatchingActions(entry, control)
            : 1;
        if (!_applyAllContexts.Checked)
            _bindings.Rebind(entry, control);
        RefreshBindings();
        SaveAll($"Saved {actionName}: {oldControl} -> {control.DisplayName} in {changedBindings} context(s). Restart the game.");
    }

    private void SaveAll(string? successMessage = null)
    {
        if (_loadedFolder is null || _ini is null || _bindings is null)
            return;
        try
        {
            var enableLeftThumbrest = _enableLeftThumbrestTripleTap.Checked;
            var enableRightThumbrest = _enableRightThumbrestTripleTap.Checked;
            var enableAnyThumbrest = enableLeftThumbrest || enableRightThumbrest;
            if (enableAnyThumbrest || _thumbrestWasEnabled)
                _bindings.ConfigureThumbrestRecentre(enableLeftThumbrest, enableRightThumbrest);

            _ini.Set("leftDeadZoneSize", _leftDeadZone.Value);
            _ini.Set("rightDeadZoneSize", _rightDeadZone.Value);
            _ini.Set("rightJoystickScale", _rightSensitivity.Value);
            _ini.Set("enableThumbrestDoubleTap", enableLeftThumbrest);
            _ini.Set("enableRightThumbrestDoubleTap", enableRightThumbrest);
            _ini.Remove("thumbrestDoubleTapUseRight");
            var thumbrestWindowMs = decimal.ToInt32(decimal.Round(_thumbrestDoubleTapWindow.Value * 1000m));
            _ini.Set("thumbrestDoubleTapWindowMs", thumbrestWindowMs);
            _ini.SaveAtomic(Path.Combine(_loadedFolder, "Binaries", "opencomposite.ini"));
            _bindings.SaveWithBackup();
            _thumbrestWasEnabled = enableAnyThumbrest;
            RefreshBindings();
            SetStatus(successMessage ?? "Saved successfully. Restart No Man's Sky for the changes to take effect.");
        }
        catch (UnauthorizedAccessException)
        {
            SetStatus("Windows blocked the save. Close the app and run it as Administrator, then try again.", true);
        }
        catch (Exception ex)
        {
            SetStatus("Save failed: " + ex.Message, true);
        }
    }

    private void RestoreOriginalBindings()
    {
        if (_bindings is null || _loadedFolder is null)
            return;
        var answer = MessageBox.Show(this,
            "Restore the original TOUCH.JSON backup? This removes every binding change made by this configurator.",
            "Restore original bindings", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes)
            return;
        try
        {
            if (!_bindings.RestoreBackup())
            {
                SetStatus("No backup exists yet. The original file is already being used.");
                return;
            }
            _bindings = NmsBindingDocument.Load(_loadedFolder);
            _enableLeftThumbrestTripleTap.Checked = false;
            _enableRightThumbrestTripleTap.Checked = false;
            _thumbrestWasEnabled = false;
            _ini?.Set("enableThumbrestDoubleTap", false);
            _ini?.Set("enableRightThumbrestDoubleTap", false);
            _ini?.Remove("thumbrestDoubleTapUseRight");
            _ini?.SaveAtomic(Path.Combine(_loadedFolder, "Binaries", "opencomposite.ini"));
            RefreshBindings();
            SetStatus("Original Touch bindings restored. Restart the game.");
        }
        catch (UnauthorizedAccessException)
        {
            SetStatus("Windows blocked the restore. Run the app as Administrator.", true);
        }
        catch (Exception ex)
        {
            SetStatus("Restore failed: " + ex.Message, true);
        }
    }

    private void SetStatus(string text, bool error = false)
    {
        _status.Text = text;
        _status.ForeColor = error ? Color.FromArgb(255, 125, 120) : TextMuted;
    }

    private static decimal Clamp(decimal value, NumericUpDown control)
        => Math.Min(control.Maximum, Math.Max(control.Minimum, value));

    private static TabPage MakePage(string text) => new(text)
    {
        BackColor = Surface,
        ForeColor = TextPrimary
    };

    private static NumericUpDown MakeNumber(decimal min, decimal max, decimal increment) => new()
    {
        Minimum = min,
        Maximum = max,
        Increment = increment,
        DecimalPlaces = 2,
        Width = 140,
        BackColor = SurfaceRaised,
        ForeColor = TextPrimary,
        BorderStyle = BorderStyle.FixedSingle,
        TextAlign = HorizontalAlignment.Center
    };

    private static void AddSectionHeader(TableLayoutPanel panel, string title, string description, int row)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        var label = MakeLabel(title, false);
        label.Font = new Font("Segoe UI Semibold", 14f);
        panel.SetColumnSpan(label, 2);
        panel.Controls.Add(label, 0, row);
        var desc = MakeLabel(description, true);
        desc.ForeColor = TextMuted;
        panel.Controls.Add(desc, 2, row);
    }

    private static void AddSetting(TableLayoutPanel panel, string title, string description, Control control, int row)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        panel.Controls.Add(MakeLabel(title, false), 0, row);
        control.Anchor = AnchorStyles.Left;
        panel.Controls.Add(control, 1, row);
        var desc = MakeLabel(description, true);
        desc.ForeColor = TextMuted;
        panel.Controls.Add(desc, 2, row);
    }

    private static Label MakeLabel(string text, bool wrap) => new()
    {
        Text = text,
        AutoSize = !wrap,
        Dock = wrap ? DockStyle.Fill : DockStyle.None,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = TextPrimary,
        Margin = new Padding(4, 7, 8, 4)
    };

    private static Button MakeButton(string text, Color color)
    {
        var button = new Button { Text = text, Dock = DockStyle.Fill };
        StyleButton(button, color);
        return button;
    }

    private static void StyleButton(Button button, Color color)
    {
        button.BackColor = color;
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.Cursor = Cursors.Hand;
        button.Margin = new Padding(5);
    }

    private static void StyleTextBox(TextBox textBox)
    {
        textBox.BackColor = SurfaceRaised;
        textBox.ForeColor = TextPrimary;
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.Margin = new Padding(5);
    }

    private static void StyleCombo(ComboBox combo)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.BackColor = SurfaceRaised;
        combo.ForeColor = TextPrimary;
        combo.FlatStyle = FlatStyle.Flat;
        combo.Dock = DockStyle.Fill;
        combo.Margin = new Padding(5);
    }

    private sealed record ContextItem(IReadOnlyList<string> Names, string Display)
    {
        public string PrimaryName => Names[0];
        public override string ToString() => Display;
    }
}
