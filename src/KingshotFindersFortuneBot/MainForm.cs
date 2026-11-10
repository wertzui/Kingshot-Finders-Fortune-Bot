using System.Drawing;

namespace KingshotFindersFortuneBot;

/// <summary>Main window: setup buttons, the grid of detected items, and the log. Owns the global Ctrl+Alt+K hotkey.</summary>
internal sealed class MainForm : Form
{
    private const int HotkeyId = 1;
    private static readonly Color UnknownColor = Color.MistyRose;
    private static readonly Color KnownColor = Color.Honeydew;
    private static readonly Color EmptyColor = Color.WhiteSmoke;
    private static readonly Color FlashColor = Color.Gold;

    private readonly Button _mapButton = CreateButton("1. Select map file");
    private readonly Button _areaButton = CreateButton("2. Select game area");
    private readonly Button _createMapButton = CreateButton("Create Map");
    private readonly Label _mapLabel = CreateDescription("No map file selected. Choose the text file with the item positions.");
    private readonly PictureBox _mapPicture = new() { SizeMode = PictureBoxSizeMode.Zoom, Visible = false, Margin = new Padding(6, 0, 0, 0) };
    private readonly Label _areaLabel = CreateDescription("No game area selected. Drag a rectangle over the whole game. Esc or a click without dragging cancels.");
    private readonly Label _hotkeyLabel = CreateDescription("");
    private readonly ComboBox _modeBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Margin = new Padding(3, 8, 3, 8) };
    private readonly NumericUpDown _delayBox = new() { Minimum = 0, Maximum = 10000, Increment = 50, Value = 100, Width = 70, Anchor = AnchorStyles.Left };
    private readonly NumericUpDown _cooldownBox = new() { Minimum = 0, Maximum = 10000, Increment = 50, Value = 1000, Width = 70, Anchor = AnchorStyles.Left };
    private readonly NumericUpDown _maxFailuresBox = new() { Minimum = 1, Maximum = 1000, Value = 10, Width = 70, Anchor = AnchorStyles.Left };
    private readonly CheckBox _neverStopBox = new() { Text = "Never stop", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly GroupBox _itemsBox = new() { Text = "Detected items", Dock = DockStyle.Fill };
    private Label[] _cells = [];
    private Color[] _cellColors = [];
    private readonly TextBox _log = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };

    private readonly ItemRecognizer? _recognizer;
    private GameMode _mode = GameMode.Fusion;
    private GameMap? _map;
    private Rectangle? _gameArea;
    private CancellationTokenSource? _cts;

    public MainForm()
    {
        Text = $"Kingshot Finders Fortune Bot {AppIcon.Version}";
        Icon = AppIcon.Instance;
        ClientSize = new Size(1300, 1000);
        MinimumSize = new Size(480, 500);
        StartPosition = FormStartPosition.CenterScreen;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(CreateSetupPanel(), 0, 0);
        root.Controls.Add(_itemsBox, 0, 1);
        root.Controls.Add(CreateLogBox(), 0, 2);
        Controls.Add(root);

        foreach (var mode in GameMode.All)
            _modeBox.Items.Add(mode);
        _modeBox.SelectedItem = _mode;
        _modeBox.SelectedIndexChanged += (_, _) => SetMode((GameMode)_modeBox.SelectedItem!);
        BuildItemGrid();

        _mapButton.Click += (_, _) => SelectMap();
        _areaButton.Click += (_, _) => SelectArea();
        _createMapButton.Click += (_, _) => CreateMap();
        UpdateHotkeyLabel(false);

        try
        {
            _recognizer = new ItemRecognizer();
        }
        catch (InvalidOperationException ex)
        {
            Log($"OCR not available: {ex.Message}");
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!NativeMethods.RegisterHotKey(Handle, HotkeyId,
                NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT, (uint)Keys.K))
            Log("Could not register the hotkey Ctrl+Alt+K (already in use?).");
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        NativeMethods.UnregisterHotKey(Handle, HotkeyId);
        base.OnHandleDestroyed(e);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _cts?.Cancel();
        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam == HotkeyId)
            Toggle();
        else
            base.WndProc(ref m);
    }

    private Control CreateSetupPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 6 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var hotkey = new Label
        {
            Text = "3. Ctrl+Alt+K",
            Dock = DockStyle.Fill,
            Height = 36,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(Font, FontStyle.Bold),
        };

        panel.Controls.Add(new Label { Text = "Game mode", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 0, 0);
        panel.Controls.Add(_modeBox, 1, 0);
        panel.Controls.Add(_mapButton, 0, 1);
        panel.Controls.Add(CreateMapInfo(), 1, 1);
        panel.Controls.Add(_areaButton, 0, 2);
        panel.Controls.Add(_areaLabel, 1, 2);
        panel.Controls.Add(hotkey, 0, 3);
        panel.Controls.Add(_hotkeyLabel, 1, 3);
        panel.Controls.Add(_createMapButton, 0, 4);
        panel.Controls.Add(CreateDescription("No map file yet? Create one from the website kingshotmastery.com."), 1, 4);
        panel.Controls.Add(new Label { Text = "Settings", Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopCenter, Padding = new Padding(0, 6, 0, 0) }, 0, 5);
        panel.Controls.Add(CreateSettingsPanel(), 1, 5);
        return panel;
    }

    private void SetMode(GameMode mode)
    {
        _mode = mode;
        BuildItemGrid();
        Log($"Game mode: {mode.Name}.");
    }

    // The grid has one cell per item button of the selected game mode.
    private void BuildItemGrid()
    {
        _itemsBox.Controls.Clear();
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = _mode.Columns,
            RowCount = _mode.Rows,
        };
        for (var c = 0; c < _mode.Columns; c++)
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / _mode.Columns));
        for (var r = 0; r < _mode.Rows; r++)
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / _mode.Rows));

        _cells = new Label[_mode.CellCount];
        _cellColors = new Color[_mode.CellCount];
        for (var i = 0; i < _cells.Length; i++)
        {
            _cellColors[i] = EmptyColor;
            _cells[i] = new Label
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(3),
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = EmptyColor,
                Font = new Font(Font.FontFamily, 11, FontStyle.Bold),
            };
            grid.Controls.Add(_cells[i], i % _mode.Columns, i / _mode.Columns);
        }

        _itemsBox.Controls.Add(grid);
    }
    private Control CreateSettingsPanel()
    {
        var tip = new ToolTip();
        tip.SetToolTip(_delayBox, "The wait after each click, in milliseconds.");
        tip.SetToolTip(_cooldownBox, "How long an item is ignored after it was clicked, in milliseconds. It should be as long as the animation of the game, in which the item name is still visible.");
        tip.SetToolTip(_maxFailuresBox, "The bot pauses itself after this many scans in a row without a known item.");
        tip.SetToolTip(_neverStopBox, "The bot keeps scanning until you pause it with the hotkey, no matter how many scans find nothing.");

        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = Padding.Empty };
        var labelPadding = new Padding(0, 6, 0, 0);
        panel.Controls.Add(new Label { Text = "Delay after each click (ms)", AutoSize = true, Margin = labelPadding });
        panel.Controls.Add(_delayBox);
        panel.Controls.Add(new Label { Text = "Ignore clicked item (ms)", AutoSize = true, Margin = new Padding(16, 6, 0, 0) });
        panel.Controls.Add(_cooldownBox);
        panel.Controls.Add(new Label { Text = "Stop after failed scans", AutoSize = true, Margin = new Padding(16, 6, 0, 0) });
        panel.Controls.Add(_maxFailuresBox);
        panel.Controls.Add(_neverStopBox);

        _neverStopBox.CheckedChanged += (_, _) => _maxFailuresBox.Enabled = !_neverStopBox.Checked;
        return panel;
    }

    private BotSettings GetSettings() => new(
        TimeSpan.FromMilliseconds((double)_delayBox.Value),
        _neverStopBox.Checked ? null : (int)_maxFailuresBox.Value,
        TimeSpan.FromMilliseconds((double)_cooldownBox.Value));
    private Control CreateLogBox()
    {
        var box = new GroupBox { Text = "Log", Dock = DockStyle.Fill };
        box.Controls.Add(_log);
        return box;
    }

    private Control CreateMapInfo()
    {
        var info = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        info.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        info.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _mapPicture.Anchor = AnchorStyles.Left;
        _mapLabel.Padding = new Padding(6, 0, 0, 0);
        info.Controls.Add(_mapPicture, 0, 0);
        info.Controls.Add(_mapLabel, 1, 0);
        return info;
    }

    private void SelectMap()
    {
        using var dialog = new MapPickerDialog();
        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SelectedPath is { } path)
            LoadMap(path);
    }

    private void CreateMap()
    {
        using var dialog = new CreateMapDialog();
        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SavedPath is { } path)
            LoadMap(path);
    }

    private void LoadMap(string path)
    {
        try
        {
            _map = GameMap.Load(path);
        }
        catch (Exception ex) when (ex is IOException or FormatException)
        {
            MessageBox.Show(this, ex.Message, "Invalid map file", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _mapLabel.Text = $"{Path.GetFileName(path)} ({_map.Count} items)";
        ShowMapPicture(path);
        Log($"Loaded map '{path}' with {_map.Count} items.");
        UpdateHotkeyLabel(false);
    }

    // The image is as high as one line of text.
    private void ShowMapPicture(string mapPath)
    {
        var height = _mapLabel.Font.Height;
        var picture = MapImage.Load(mapPath, new Size(height * 4, height));

        _mapPicture.Image?.Dispose();
        _mapPicture.Image = picture;
        _mapPicture.Visible = picture is not null;
        if (picture is not null)
            _mapPicture.Size = picture.Size;
    }

    private void SelectArea()
    {
        if (RegionSelector.SelectArea() is not { } area)
        {
            Log("Game area selection cancelled.");
            return;
        }

        _gameArea = area;
        _areaLabel.Text = $"{area.Width} x {area.Height} at ({area.X}, {area.Y})";
        Log($"Game area set to {area}.");
        Activate();
        UpdateHotkeyLabel(false);
    }

    private void Toggle()
    {
        if (_cts is not null)
        {
            _cts.Cancel();
            return;
        }

        if (_recognizer is null || _map is null || _gameArea is not { } area)
        {
            Log(_recognizer is null
                ? "Cannot start: OCR is not available."
                : "Cannot start: select a map file and the game area first.");
            return;
        }

        var bot = new Bot(_map, _mode, area, _recognizer, GetSettings());
        bot.Log += Log;
        bot.ItemsDetected += items => BeginInvoke(() => ShowItems(items));
        bot.ItemClicked += cell => BeginInvoke(() => Flash(cell));

        var cts = _cts = new CancellationTokenSource();
        SetRunning(true);
        Log("Started.");
        _ = Task.Run(async () =>
        {
            try
            {
                await bot.RunAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Message}");
            }
            finally
            {
                BeginInvoke(() =>
                {
                    _cts = null;
                    cts.Dispose();
                    SetRunning(false);
                    Log("Paused.");
                });
            }
        });
    }

    private void SetRunning(bool running)
    {
        _mapButton.Enabled = !running;
        _createMapButton.Enabled = !running;
        _areaButton.Enabled = !running;
        _modeBox.Enabled = !running;
        _delayBox.Enabled = !running;
        _cooldownBox.Enabled = !running;
        _neverStopBox.Enabled = !running;
        _maxFailuresBox.Enabled = !running && !_neverStopBox.Checked;
        UpdateHotkeyLabel(running);
    }

    private void UpdateHotkeyLabel(bool running)
    {
        if (running)
        {
            _hotkeyLabel.Text = "RUNNING - press Ctrl+Alt+K to pause.";
            _hotkeyLabel.ForeColor = Color.Green;
        }
        else
        {
            var ready = _map is not null && _gameArea is not null;
            _hotkeyLabel.Text = ready
                ? "PAUSED - press Ctrl+Alt+K to start."
                : "Select the map file and the game area, then press Ctrl+Alt+K to start or pause.";
            _hotkeyLabel.ForeColor = ready ? Color.DarkRed : SystemColors.ControlText;
        }
    }

    private void ShowItems(DetectedItem?[] items)
    {
        for (var i = 0; i < _cells.Length; i++)
        {
            var item = i < items.Length ? items[i] : null;
            _cells[i].Text = item?.Entry?.Name ?? item?.Text ?? "";
            _cellColors[i] = item is null ? EmptyColor : item.Entry is null ? UnknownColor : KnownColor;
            _cells[i].BackColor = _cellColors[i];
        }
    }

    private async void Flash(int cell)
    {
        _cells[cell].BackColor = FlashColor;
        await Task.Delay(250);
        if (!IsDisposed)
            _cells[cell].BackColor = _cellColors[cell];
    }

    private void Log(string message)
    {
        void Append() => _log.AppendText($"{DateTime.Now:HH:mm:ss} {message}{Environment.NewLine}");

        if (InvokeRequired)
            BeginInvoke(Append);
        else
            Append();
    }

    private static Button CreateButton(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        MinimumSize = new Size(0, 36),
        Padding = new Padding(8, 0, 8, 0),
    };

    private static Label CreateDescription(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        AutoSize = false,
        Height = 36,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(6, 0, 0, 0),
    };
}

