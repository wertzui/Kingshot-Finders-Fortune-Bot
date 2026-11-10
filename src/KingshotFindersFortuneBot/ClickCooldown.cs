namespace KingshotFindersFortuneBot;

/// <summary>
/// Remembers which items were clicked recently. After a click the game plays an animation, during which the item text is still
/// visible. The same text must not be clicked again until the cooldown is over.
/// </summary>
/// <remarks>
/// The cooldown is compared with the time a screenshot was taken, not with the time of the decision. Reading the screenshot and the
/// clicks before it take time, so a screenshot can show an item that was clicked in the meantime.
/// </remarks>
public sealed class ClickCooldown(TimeSpan cooldown, TimeProvider? time = null)
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly Dictionary<string, long> _clicked = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The current time. Take it right before a screenshot and pass it to <see cref="IsCoolingDown"/>.</summary>
    public long GetTimestamp() => _time.GetTimestamp();

    /// <summary>Whether an item seen in a screenshot, which was taken at <paramref name="observedAt"/>, is the one that was clicked.</summary>
    public bool IsCoolingDown(string item, long observedAt)
    {
        if (!_clicked.TryGetValue(item, out var clickedAt))
            return false;

        // A screenshot from before the click (negative time) shows the item as well.
        if (_time.GetElapsedTime(clickedAt, observedAt) < cooldown)
            return true;

        _clicked.Remove(item);
        return false;
    }

    public void MarkClicked(string item) => _clicked[item] = _time.GetTimestamp();
}
