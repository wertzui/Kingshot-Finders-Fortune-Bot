using System.Drawing;

namespace KingshotFindersFortuneBot;

/// <summary>The application icon (the icon of the exe), used for all windows.</summary>
internal static class AppIcon
{
    public static Icon? Instance { get; } = Load();

    public static string Version { get; } =
        typeof(AppIcon).Assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "";

    private static Icon? Load() =>
        Environment.ProcessPath is { } path ? Icon.ExtractAssociatedIcon(path) : null;
}
