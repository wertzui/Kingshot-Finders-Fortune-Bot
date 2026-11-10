using System.Globalization;

namespace KingshotFindersFortuneBot;

/// <summary>An item name and its click position as fractions (0 to 1) of the board picture.</summary>
public sealed record MapEntry(string Name, double X, double Y);

/// <summary>
/// Item-to-position mapping, independent of the screen size and aspect ratio.
/// Format (lines starting with '#' and empty lines are ignored):
/// <code>
/// Key;0.7273;0.78   (item name;x;y as fractions of the board picture)
/// </code>
/// Older files have a first line with four margins. It is ignored, because the margins depend on the <see cref="GameMode"/>.
/// </summary>
public sealed class GameMap
{
    private readonly List<(MapEntry Entry, string Normalized)> _entries;

    private GameMap(List<(MapEntry, string)> entries) => _entries = entries;

    public int Count => _entries.Count;

    public static GameMap Load(string path)
    {
        var lines = File.ReadAllLines(path)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToList();

        if (lines.Count > 0 && IsLegacyMarginLine(lines[0]))
            lines.RemoveAt(0);

        var entries = new List<(MapEntry, string)>();
        foreach (var line in lines)
        {
            var parts = line.Split(';', StringSplitOptions.TrimEntries);
            if (parts.Length != 3
                || parts[0].Length == 0
                || !double.TryParse(parts[1], CultureInfo.InvariantCulture, out var x)
                || !double.TryParse(parts[2], CultureInfo.InvariantCulture, out var y))
                throw new FormatException($"Invalid item line '{line}'. Expected 'Name;x;y'.");

            entries.Add((new MapEntry(parts[0], x, y), Normalize(parts[0])));
        }

        return new GameMap(entries);
    }

    private static bool IsLegacyMarginLine(string line)
    {
        var parts = line.Split([' ', '\t', ','], StringSplitOptions.RemoveEmptyEntries);
        return !line.Contains(';')
            && parts.Length == 4
            && parts.All(p => double.TryParse(p, CultureInfo.InvariantCulture, out _));
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

