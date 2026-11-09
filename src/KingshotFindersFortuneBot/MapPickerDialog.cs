using System.Drawing;
using SkiaSharp;

namespace KingshotFindersFortuneBot;

/// <summary>Shows the maps of the maps folder as a grid of images, with a fallback to the normal file dialog.</summary>
internal sealed class MapPickerDialog : Form
{
    private static readonly string[] ImageExtensions = [".webp", ".png", ".jpg", ".jpeg", ".bmp"];
    private static readonly Size ThumbnailSize = new(150, 250);
    private static readonly Size TileSize = new(174, 310);

    private readonly FlowLayoutPanel _grid = new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        Padding = new Padding(8),
    };

    private readonly List<Image> _thumbnails = [];

    public MapPickerDialog()
    {
        Text = "Select the map";
        ClientSize = new Size(ScaleLogical(780), ScaleLogical(520));
        MinimumSize = new Size(ScaleLogical(420), ScaleLogical(300));
        StartPosition = FormStartPosition.CenterParent;
        Icon = AppIcon.Instance;
        ShowInTaskbar = false;

        var otherButton = new Button { Text = "Open another map file...", AutoSize = true, Padding = new Padding(8, 4, 8, 4) };
        var cancelButton = new Button { Text = "Cancel", AutoSize = true, Padding = new Padding(8, 4, 8, 4), DialogResult = DialogResult.Cancel };
        otherButton.Click += (_, _) => OpenOtherFile();

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(8),
            FlowDirection = FlowDirection.LeftToRight,
        };
        bottom.Controls.Add(otherButton);
        bottom.Controls.Add(cancelButton);

        Controls.Add(_grid);
        Controls.Add(bottom);
        CancelButton = cancelButton;

        Shown += async (_, _) => await FillGridAsync();
    }

    public static string MapsDirectory => Path.Combine(AppContext.BaseDirectory, "maps");

    /// <summary>The path of the chosen map file, or null if nothing was chosen.</summary>
    public string? SelectedPath { get; private set; }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            foreach (var thumbnail in _thumbnails)
                thumbnail.Dispose();

        base.Dispose(disposing);
    }

    private async Task FillGridAsync()
    {
        var maps = Directory.Exists(MapsDirectory)
            ? Directory.EnumerateFiles(MapsDirectory, "*.txt").OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase).ToList()
            : [];

        if (maps.Count == 0)
        {
            _grid.Controls.Add(new Label
            {
                AutoSize = true,
                MaximumSize = new Size(ClientSize.Width - 40, 0),
                Text = $"No maps found in '{MapsDirectory}'. Use \"Create Map\" or open a map file from somewhere else.",
            });
            return;
        }

        var tiles = new List<(Button Tile, string Path)>();
        foreach (var path in maps)
        {
            var tile = new Button
            {
                Size = TileSize,
                Text = Path.GetFileNameWithoutExtension(path),
                TextImageRelation = TextImageRelation.ImageAboveText,
                ImageAlign = ContentAlignment.TopCenter,
                TextAlign = ContentAlignment.BottomCenter,
                Margin = new Padding(6),
            };
            tile.Click += (_, _) => Choose(path);
            _grid.Controls.Add(tile);
            tiles.Add((tile, path));
        }

        // The images are decoded in the background, so the dialog opens right away even with many maps.
        foreach (var (tile, path) in tiles)
        {
            var thumbnail = await Task.Run(() => MapImage.Load(path, ThumbnailSize));
            if (IsDisposed)
            {
                thumbnail?.Dispose();
                return;
            }

            if (thumbnail is null)
                continue;

            _thumbnails.Add(thumbnail);
            tile.Image = thumbnail;
        }
    }

    private void OpenOtherFile()
    {
        using var dialog = new OpenFileDialog { Title = "Select the map file", Filter = "Map files (*.txt)|*.txt|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            Choose(dialog.FileName);
    }

    private void Choose(string path)
    {
        SelectedPath = path;
        DialogResult = DialogResult.OK;
    }

    private int ScaleLogical(int pixels) => (int)Math.Round(pixels * DeviceDpi / 96.0);
}
