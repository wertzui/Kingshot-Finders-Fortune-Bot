using System.Drawing;

namespace KingshotFindersFortuneBot;

/// <summary>Full-screen overlay where the user drags a rectangle around the game area.</summary>
internal sealed class RegionSelector : Form
{
    private Point? _start;
    private Point _current;

    private RegionSelector()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
        TopMost = true;
        ShowInTaskbar = false;
        Opacity = 0.35;
        BackColor = Color.Black;
        Cursor = Cursors.Cross;
        DoubleBuffered = true;
    }

    public Rectangle Selection { get; private set; }

    public static Rectangle? SelectArea()
    {
        using var form = new RegionSelector();
        return form.ShowDialog() == DialogResult.OK ? form.Selection : null;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        _start = _current = Cursor.Position;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_start is null)
            return;

        _current = Cursor.Position;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_start is null)
            return;

        Selection = ToRectangle(_start.Value, Cursor.Position);
        DialogResult = Selection.Width > 10 && Selection.Height > 10 ? DialogResult.OK : DialogResult.Cancel;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
            DialogResult = DialogResult.Cancel;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using var font = new Font(Font.FontFamily, 16);
        e.Graphics.DrawString("Drag a rectangle over the whole game area. Esc cancels.", font, Brushes.White, 20, 20);

        if (_start is null)
            return;

        var rect = ToRectangle(_start.Value, _current);
        rect.Location = PointToClient(rect.Location);
        using var pen = new Pen(Color.Red, 2);
        e.Graphics.DrawRectangle(pen, rect);
    }

    private static Rectangle ToRectangle(Point a, Point b) =>
        Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
}

