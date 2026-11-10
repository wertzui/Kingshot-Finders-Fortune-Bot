using System.Drawing;

namespace KingshotFindersFortuneBot;

/// <summary>
/// A game mode. The modes differ in the layout of the item buttons (which area is read with OCR)
/// and in where the board picture sits on the screen. The maps work for all modes.
/// </summary>
public sealed class GameMode
{
    /// <summary>The multiplayer mode. Six items in two rows. Measured on a 1138 x 2035 screenshot.</summary>
    public static readonly GameMode Fusion = new(
        "Finders Fusion (multiplayer, 6 items)",
        columns: 3,
        rows: 2,
        ocrTop: 0.85,
        ocrBottom: 1.0,
        picture: new Margins(96.0 / 1138, 125.0 / 2035, (1138.0 - 1046) / 1138, (2035.0 - 1683) / 2035));

    /// <summary>
    /// The singleplayer mode. Three items in one row. Above the row is a timer, below it is a progress bar,
    /// so the OCR area is limited to the row of buttons. Measured on a 576 x 1032 screenshot.
    /// </summary>
    public static readonly GameMode Fortune = new(
        "Finders Fortune (singleplayer, 3 items)",
        columns: 3,
        rows: 1,
        ocrTop: 914.0 / 1032,
        ocrBottom: 980.0 / 1032,
        picture: new Margins(47.0 / 576, 63.0 / 1032, (576.0 - 531) / 576, (1032.0 - 857) / 1032));

    public static IReadOnlyList<GameMode> All { get; } = [Fusion, Fortune];

    /// <summary>The margins of the board picture inside the game area, as fractions of the game area.</summary>
    public readonly record struct Margins(double Left, double Top, double Right, double Bottom);

    private readonly double _ocrTop;
    private readonly double _ocrBottom;
    private readonly Margins _picture;

    private GameMode(string name, int columns, int rows, double ocrTop, double ocrBottom, Margins picture)
    {
        Name = name;
        Columns = columns;
        Rows = rows;
        _ocrTop = ocrTop;
        _ocrBottom = ocrBottom;
        _picture = picture;
    }

    public string Name { get; }
    public int Columns { get; }
    public int Rows { get; }
    public int CellCount => Columns * Rows;

    /// <summary>The part of the game area with the item names. It must not contain anything else with text.</summary>
    public Rectangle GetOcrRegion(Rectangle gameArea)
    {
        var top = gameArea.Y + (int)Math.Round(gameArea.Height * _ocrTop);
        var bottom = gameArea.Y + (int)Math.Round(gameArea.Height * _ocrBottom);
        return new Rectangle(gameArea.X, top, gameArea.Width, bottom - top);
    }

    /// <summary>Converts a map position (a fraction of the board picture) to a screen position inside the game area.</summary>
    public Point ToScreen(MapEntry entry, Rectangle gameArea)
    {
        var left = gameArea.X + gameArea.Width * _picture.Left;
        var top = gameArea.Y + gameArea.Height * _picture.Top;
        var width = gameArea.Width * (1 - _picture.Left - _picture.Right);
        var height = gameArea.Height * (1 - _picture.Top - _picture.Bottom);
        return new Point((int)Math.Round(left + entry.X * width), (int)Math.Round(top + entry.Y * height));
    }

    public override string ToString() => Name;
}
