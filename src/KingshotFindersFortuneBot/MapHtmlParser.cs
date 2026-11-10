using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;

namespace KingshotFindersFortuneBot;

/// <summary>Converts the board HTML of kingshotmastery.com into the map file format.</summary>
public static class MapHtmlParser
{
    public sealed record ParsedMap(string Name, string FileContent, string? ImageUrl);

    public static ParsedMap Parse(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var board = document.DocumentNode.SelectSingleNode("//*[@id='finders-fortune-board' or contains(concat(' ', normalize-space(@class), ' '), ' finders-fortune-board ')]")
            ?? document.DocumentNode;

        var items = new List<(string Name, double X, double Y)>();
        foreach (var button in board.SelectNodes(".//button[@data-item-id]") ?? Enumerable.Empty<HtmlNode>())
        {
            var name = HtmlEntity.DeEntitize(button.GetAttributeValue("aria-label", button.GetAttributeValue("title", ""))).Trim();
            var style = HtmlEntity.DeEntitize(button.GetAttributeValue("style", ""));
            if (name.Length == 0
                || !TryGetPercent(style, "left", out var left)
                || !TryGetPercent(style, "top", out var top)
                || !TryGetPercent(style, "width", out var width)
                || !TryGetPercent(style, "height", out var height))
                continue;

            // The script in the browser stores a point inside the item's real shape (the bounding box centre can be empty space).
            var x = TryGetAttribute(button, "data-click-x", out var clickX) ? clickX : left + width / 2;
            var y = TryGetAttribute(button, "data-click-y", out var clickY) ? clickY : top + height / 2;
            items.Add((name, x / 100, y / 100));
        }

        if (items.Count == 0)
            throw new FormatException("No items were found in the HTML. Did the script copy the right element?");

        var mapName = GetMapName(board);
        var text = new StringBuilder();
        text.AppendLine($"# Map '{mapName}' created from kingshotmastery.com. Lines starting with # are comments.");
        text.AppendLine("# Name;x;y as fractions of the board picture");
        foreach (var (name, x, y) in items)
            text.AppendLine($"{name.Replace(';', ',')};{Format(x)};{Format(y)}");

        return new ParsedMap(mapName, text.ToString(), GetImageUrl(board));
    }

    private static string? GetImageUrl(HtmlNode board)
    {
        // The background is the direct child image of the board. The item images are inside buttons.
        var src = (board.SelectSingleNode("./img[@src]") ?? board.SelectSingleNode(".//img[@src and not(ancestor::button)]"))
            ?.GetAttributeValue("src", "");
        return string.IsNullOrWhiteSpace(src) ? null : HtmlEntity.DeEntitize(src).Trim();
    }

    private static string GetMapName(HtmlNode board)
    {
        var alt = HtmlEntity.DeEntitize(board.SelectSingleNode(".//img[@alt]")?.GetAttributeValue("alt", "") ?? "").Trim();
        if (alt.EndsWith(" map", StringComparison.OrdinalIgnoreCase))
            alt = alt[..^4].TrimEnd();

        return alt.Length > 0 ? alt : "Finders Fortune";
    }

    private static bool TryGetAttribute(HtmlNode node, string name, out double value)
    {
        value = 0;
        return node.Attributes.Contains(name)
            && double.TryParse(node.GetAttributeValue(name, ""), CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetPercent(string style, string property, out double value)
    {
        var match = Regex.Match(style, $@"(?<![\w-]){property}\s*:\s*(-?[\d.]+)%", RegexOptions.IgnoreCase);
        value = 0;
        return match.Success && double.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out value);
    }

    private static string Format(double value) => Math.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture);
}
