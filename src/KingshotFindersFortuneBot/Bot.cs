using System.Drawing;

namespace KingshotFindersFortuneBot;

/// <summary>An item read from one of the grid cells; <see cref="Entry"/> is null if it is not in the map.</summary>
public sealed record DetectedItem(string Text, MapEntry? Entry);

/// <summary>The main loop: screenshot the item names, OCR them and click the mapped positions.</summary>
public sealed class Bot(GameMap map, GameMode mode, Rectangle gameArea, ItemRecognizer recognizer)
{
    private static readonly TimeSpan ClickDelay = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan IdleScanDelay = TimeSpan.FromMilliseconds(100);
    private const int MaxEmptyScans = 10;

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

        while (!cancellationToken.IsCancellationRequested)
        {
            string[] texts;
            using (var screenshot = ScreenCapture.Capture(_ocrRegion))
                texts = await recognizer.ReadItemsAsync(screenshot, mode.Columns, mode.Rows);

            var items = texts.Select(t => t.Length == 0 ? null : new DetectedItem(t, map.Find(t))).ToArray();
            ItemsDetected?.Invoke(items);

            var clicked = 0;
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

                NativeMethods.Click(mode.ToScreen(entry, gameArea));
                ItemClicked?.Invoke(cell);
                Log?.Invoke($"Clicked '{entry.Name}'" + (entry.Name.Equals(item.Text, StringComparison.OrdinalIgnoreCase) ? "" : $" (read as '{item.Text}')"));
                clicked++;
                await Task.Delay(ClickDelay, cancellationToken);
            }

            if (clicked > 0)
            {
                emptyScans = 0;
                continue;
            }

            if (++emptyScans >= MaxEmptyScans)
            {
                Log?.Invoke($"No known items found in {MaxEmptyScans} scans. Paused.");
                return;
            }

            await Task.Delay(IdleScanDelay, cancellationToken);
        }
    }
}
