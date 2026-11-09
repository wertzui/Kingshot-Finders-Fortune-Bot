using System.Drawing;
using System.Globalization;

namespace KingshotFindersFortuneBot;

/// <summary>An item name and its click position as fractions (0 to 1) of the board picture.</summary>
public sealed record MapEntry(string Name, double X, double Y);

/// <summary>
/// Item-to-position mapping, independent of the screen size and aspect ratio.
/// Format (lines starting with '#' and empty lines are ignored):
/// <code>
/// 0.0844 0.0614 0.0808 0.1730   (left top right bottom margin of the board picture as fractions of the game area)
/// Key;0.7273;0.78               (item name;x;y as fractions of the board picture)
/// </code>
/// </summary>
public sealed class GameMap
{
    private readonly List<(MapEntry Entry, string Normalized)> _entries;

    private GameMap(double marginLeft, double marginTop, double marginRight, double marginBottom, List<(MapEntry, string)> entries)
    {
        MarginLeft = marginLeft;
        MarginTop = marginTop;
        MarginRight = marginRight;
        MarginBottom = marginBottom;
        _entries = entries;
    }

    public double MarginLeft { get; }
    public double MarginTop { get; }
    public double MarginRight { get; }
    public double MarginBottom { get; }
    public int Count => _entries.Count;

    public static GameMap Load(string path)
    {
        var lines = File.ReadAllLines(path)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToList();

        if (lines.Count < 1)
            throw new FormatException("The map file needs a line with the margins of the board picture.");

        var margins = lines[0].Split([' ', '\t', ',', ';'], StringSplitOptions.RemoveEmptyEntries);
        if (margins.Length != 4
            || margins.Any(m => !double.TryParse(m, CultureInfo.InvariantCulture, out var v) || v < 0 || v >= 1))
            throw new FormatException($"Invalid margin line '{lines[0]}'. Expected four fractions 'left top right bottom'.");

        var m = margins.Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        if (m[0] + m[2] >= 1 || m[1] + m[3] >= 1)
            throw new FormatException("The margins in the map file leave no room for the board picture.");

        var entries = new List<(MapEntry, string)>();
        for (var i = 1; i < lines.Count; i++)
        {
            var parts = lines[i].Split(';', StringSplitOptions.TrimEntries);
            if (parts.Length != 3
                || parts[0].Length == 0
                || !double.TryParse(parts[1], CultureInfo.InvariantCulture, out var x)
                || !double.TryParse(parts[2], CultureInfo.InvariantCulture, out var y))
                throw new FormatException($"Invalid item line '{lines[i]}'. Expected 'Name;x;y'.");

            entries.Add((new MapEntry(parts[0], x, y), Normalize(parts[0])));
        }

        return new GameMap(m[0], m[1], m[2], m[3], entries);
    }
    /// <summary>Finds the entry for an OCR text, tolerating small OCR errors.</summary>
    public MapEntry? Find(string text)
    {
        var normalized = Normalize(text);
        if (normalized.Length == 0)
            return null;

        MapEntry? best = null;
        var bestDistance = int.MaxValue;
        foreach (var (entry, name) in _entries)
        {
            var distance = name == normalized ? 0 : Levenshtein(name, normalized);
            if (distance < bestDistance)
            {
                best = entry;
                bestDistance = distance;
            }
        }

        return best is not null && bestDistance <= Math.Max(1, Normalize(best.Name).Length / 4) ? best : null;
    }

    /// <summary>Converts a map position to a screen position inside the user's game area.</summary>
    public Point ToScreen(MapEntry entry, Rectangle area)
    {
        var left = area.X + area.Width * MarginLeft;
        var top = area.Y + area.Height * MarginTop;
        var width = area.Width * (1 - MarginLeft - MarginRight);
        var height = area.Height * (1 - MarginTop - MarginBottom);
        return new Point((int)Math.Round(left + entry.X * width), (int)Math.Round(top + entry.Y * height));
    }
    private static string Normalize(string text) =>
        new(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static int Levenshtein(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
            previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}

