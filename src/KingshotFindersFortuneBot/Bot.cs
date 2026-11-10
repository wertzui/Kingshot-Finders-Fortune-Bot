using System.Drawing;

namespace KingshotFindersFortuneBot;

/// <summary>An item read from one of the grid cells; <see cref="Entry"/> is null if it is not in the map.</summary>
public sealed record DetectedItem(string Text, MapEntry? Entry);

/// <summary>The main loop: screenshot the item names, OCR them and click the mapped positions.</summary>
public sealed class Bot(GameMap map, GameMode mode, Rectangle gameArea, ItemRecognizer recognizer, BotSettings settings)
{
    private static readonly TimeSpan IdleScanDelay = TimeSpan.FromMilliseconds(100);

    // Only the row(s) with the item names are captured, which also keeps OCR fast.
    private readonly Rectangle _ocrRegion = mode.GetOcrRegion(gameArea);
    public event Action<string>? Log;

    /// <summary>Raised after each scan with one element per grid cell (null if the cell is empty).</summary>
    public event Action<DetectedItem?[]>? ItemsDetected;

    /// <summary>Raised after clicking the item of the given grid cell.</summary>
    public event Action<int>? ItemClicked;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var unknown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var emptyScans = 0;
        var cooldown = new ClickCooldown(settings.ItemCooldown);

        while (!cancellationToken.IsCancellationRequested)
        {
            string[] texts;
            var capturedAt = cooldown.GetTimestamp();
            using (var screenshot = ScreenCapture.Capture(_ocrRegion))
                texts = await recognizer.ReadItemsAsync(screenshot, mode.Columns, mode.Rows);

            var items = texts.Select(t => t.Length == 0 ? null : new DetectedItem(t, map.Find(t))).ToArray();
            ItemsDetected?.Invoke(items);

            var clicked = 0;
            var coolingDown = 0;
            for (var cell = 0; cell < items.Length; cell++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (items[cell] is not { } item)
                    continue;

                if (item.Entry is not { } entry)
                {
                    if (unknown.Add(item.Text))
                        Log?.Invoke($"Unknown item: '{item.Text}'");
                    continue;
                }

                // The item was clicked a moment ago and is still visible, because the game plays an animation.
                if (cooldown.IsCoolingDown(entry.Name, capturedAt))
                {
                    coolingDown++;
                    continue;
                }

                NativeMethods.Click(mode.ToScreen(entry, gameArea));
                cooldown.MarkClicked(entry.Name);
                ItemClicked?.Invoke(cell);
                Log?.Invoke($"Clicked '{entry.Name}'" + (entry.Name.Equals(item.Text, StringComparison.OrdinalIgnoreCase) ? "" : $" (read as '{item.Text}')"));
                clicked++;
                await Task.Delay(settings.ClickDelay, cancellationToken);
            }

            if (clicked > 0)
            {
                emptyScans = 0;
                continue;
            }

            // Waiting for an animation to end is no failed scan.
            if (coolingDown > 0)
            {
                emptyScans = 0;
                await Task.Delay(IdleScanDelay, cancellationToken);
                continue;
            }
            if (settings.MaxEmptyScans is { } maxEmptyScans && ++emptyScans >= maxEmptyScans)
            {
                Log?.Invoke($"No known items found in {maxEmptyScans} scans. Paused.");
                return;
            }

            await Task.Delay(IdleScanDelay, cancellationToken);
        }
    }
}
