using System.Diagnostics;
using System.Drawing;
using System.Net.Http;
using System.Text;

namespace KingshotFindersFortuneBot;

/// <summary>Guides the user through copying the board HTML from kingshotmastery.com and saves it as a map file.</summary>
internal sealed class CreateMapDialog : Form
{
    private const string Url = "https://kingshotmastery.com/tools/finders-fortune";

    // The script (CopyBoard.js) is embedded and flattened to one line, because it is typed into the address bar.
    // Chrome and Edge strip a pasted "javascript:" prefix, so the user has to type it (the explanation says so).
    private static readonly string Script = "javascript:" + LoadScript();

    private static string LoadScript()
    {
        using var stream = typeof(CreateMapDialog).Assembly.GetManifestResourceStream("KingshotFindersFortuneBot.CopyBoard.js")
            ?? throw new InvalidOperationException("The embedded script CopyBoard.js is missing.");
        using var reader = new StreamReader(stream);
        return string.Concat(reader.ReadToEnd().Split('\n').Select(l => l.Trim()));
    }
    private readonly Button _openButton = new() { Text = "1. Open website and copy script", AutoSize = true, Padding = new Padding(8, 4, 8, 4) };
    private readonly Button _nextButton = new() { Text = "4. Next: read HTML and save map", AutoSize = true, Padding = new Padding(8, 4, 8, 4) };

    public CreateMapDialog()
    {
        Text = "Create map";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Icon = AppIcon.Instance;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        // The label wraps at a fixed width and grows in height, so the dialog always fits its text.
        var explanation = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(ScaleLogical(640), 0),
            Margin = new Padding(12),
            Text =
                "1. Click the first button. Your browser opens " + Url + " and a script is copied to the clipboard.\r\n\r\n" +
                "2. Open the map you want in the browser. Click the address bar, type \"javascript:\" yourself, paste the script after it and press Enter.\r\n" +
                "    (Browsers remove \"javascript:\" from pasted text, so it has to be typed.)\r\n\r\n" +
                "3. The script finds the best click point inside every item (also for irregular shapes), adds it to the HTML of the board (div.finders-fortune-board), copies that to the clipboard and shows a message.\r\n" +
                "    If the browser blocks the copy, a box with a Copy button appears. Click it (or press Ctrl+C in the box).\r\n\r\n" +
                "4. Click the second button. The HTML is read from the clipboard, converted to a map file, and a Save As dialog opens with the file name of the map. The map image is saved next to it with the same name.",
        };

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(8),
        };
        buttons.Controls.Add(_openButton);
        buttons.Controls.Add(_nextButton);

        var root = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Dock = DockStyle.Fill,
        };
        root.Controls.Add(explanation, 0, 0);
        root.Controls.Add(buttons, 0, 1);
        Controls.Add(root);

        _openButton.Click += (_, _) => OpenWebsite();
        _nextButton.Click += (_, _) => CreateMap();
    }

    private int ScaleLogical(int pixels) => (int)Math.Round(pixels * DeviceDpi / 96.0);
    /// <summary>The path of the saved map file, or null if nothing was saved.</summary>
    public string? SavedPath { get; private set; }

    private void OpenWebsite()
    {
        Clipboard.SetText(Script);
        try
        {
            Process.Start(new ProcessStartInfo(Url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(this, $"The browser could not be opened: {ex.Message}\r\nOpen {Url} manually.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async void CreateMap()
    {
        MapHtmlParser.ParsedMap map;
        try
        {
            var html = Clipboard.ContainsText() ? Clipboard.GetText() : "";
            if (!html.Contains("<button", StringComparison.OrdinalIgnoreCase))
                throw new FormatException("The clipboard does not contain the board HTML. Run the script in the browser first (steps 1 to 3).");

            map = MapHtmlParser.Parse(html);
        }
        catch (FormatException ex)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var invalid = Path.GetInvalidFileNameChars();
        var fileName = new string(map.Name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        using var dialog = new SaveFileDialog
        {
            Title = "Save the map file",
            Filter = "Map files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = fileName + ".txt",
            DefaultExt = "txt",
            OverwritePrompt = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        File.WriteAllText(dialog.FileName, map.FileContent, new UTF8Encoding(false));
        SavedPath = dialog.FileName;

        if (map.ImageUrl is not null)
        {
            _nextButton.Enabled = false;
            UseWaitCursor = true;
            try
            {
                await SaveImageAsync(map.ImageUrl, dialog.FileName);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or UriFormatException or TaskCanceledException)
            {
                MessageBox.Show(this, $"The map was saved, but the map image could not be saved: {ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                UseWaitCursor = false;
                _nextButton.Enabled = true;
            }
        }

        DialogResult = DialogResult.OK;
    }

    /// <summary>Downloads the board image and saves it next to the map file with the same name.</summary>
    private static async Task SaveImageAsync(string imageUrl, string mapPath)
    {
        var uri = new Uri(new Uri(Url), imageUrl);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var bytes = await http.GetByteArrayAsync(uri);

        var extension = Path.GetExtension(uri.AbsolutePath);
        if (extension.Length == 0)
            extension = ".png";

        await File.WriteAllBytesAsync(Path.ChangeExtension(mapPath, extension), bytes);
    }
}