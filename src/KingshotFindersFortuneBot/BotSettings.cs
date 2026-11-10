namespace KingshotFindersFortuneBot;

/// <summary>The settings of a bot run.</summary>
/// <param name="ClickDelay">The wait after each click.</param>
/// <param name="MaxEmptyScans">The bot pauses itself after this many scans in a row without a known item. Null means it never stops.</param>
/// <param name="ItemCooldown">How long an item is ignored after it was clicked. It covers the animation of the game, in which the item text is still visible.</param>
public sealed record BotSettings(TimeSpan ClickDelay, int? MaxEmptyScans, TimeSpan ItemCooldown)
{
    public static BotSettings Default { get; } = new(TimeSpan.FromMilliseconds(200), 10, TimeSpan.FromMilliseconds(500));
}
