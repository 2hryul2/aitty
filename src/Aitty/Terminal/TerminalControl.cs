using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Aitty.Terminal;

/// <summary>
/// WPF custom element that renders a <see cref="TerminalBuffer"/> using DrawingContext.
/// Implements: character grid, SGR colors, cursor blink, keyboard forwarding, mouse scroll.
/// </summary>
public sealed class TerminalControl : FrameworkElement
{
    // ─── Configuration ────────────────────────────────────────────────────────
    private const string FontFamilyName = "Consolas";
    private const double FontSize       = 12.0;
    private const int    CursorBlinkMs  = 530;
    private const int    ScrollbackPage = 3; // rows per mouse-wheel tick

    // ─── State ───────────────────────────────────────────────────────────────
    private TerminalBuffer? _buffer;

    private readonly Typeface _typeface;
    private readonly Typeface _typefaceBold;
    private double _charW = 7.22;   // updated in MeasureChar()
    private double _charH = 14.8;
    private double _ppd   = 1.0;    // pixels-per-dip

    private readonly DispatcherTimer _cursorTimer;
    private bool _cursorPhase = true;   // true = solid, false = invisible

    private int _scrollOffset = 0;     // >0 = scrolled into scrollback

    // Brush cache (Color → frozen SolidColorBrush)
    private static readonly Dictionary<Color, SolidColorBrush> _brushCache = new();
    private static readonly SolidColorBrush BrushBlack   = MakeBrush(Color.FromRgb(0x00, 0x00, 0x00));
    private static readonly SolidColorBrush BrushCursor  = MakeBrush(Color.FromArgb(0xCC, 0x00, 0xFF, 0x00));
    private static readonly SolidColorBrush BrushScrollFg = MakeBrush(Color.FromRgb(0x44, 0x44, 0x44));

    // ─── Keyboard event ───────────────────────────────────────────────────────
    /// <summary>Raw text to forward to SSH shell. Raised on UI thread.</summary>
    public event Action<string>? KeyInput;

    // ─── Constructor ─────────────────────────────────────────────────────────

    public TerminalControl()
    {
        _typeface     = new Typeface(new FontFamily(FontFamilyName),
            FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        _typefaceBold = new Typeface(new FontFamily(FontFamilyName),
            FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

        _cursorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(CursorBlinkMs) };
        _cursorTimer.Tick += (_, _) =>
        {
            _cursorPhase = !_cursorPhase;
            InvalidateVisual();
        };
        _cursorTimer.Start();

        ClipToBounds = true;
        Focusable    = true;

        Loaded += OnLoaded;
        SizeChanged += (_, _) => OnSizeChanged();
    }

    // ─── Buffer binding ───────────────────────────────────────────────────────

    public TerminalBuffer? Buffer
    {
        get => _buffer;
        set
        {
            if (_buffer is not null) _buffer.Changed -= OnBufferChanged;
            _buffer = value;
            if (_buffer is not null)
            {
                _buffer.Changed += OnBufferChanged;
                OnSizeChanged();   // initial resize
            }
            _scrollOffset = 0;
            InvalidateVisual();
        }
    }

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        MeasureChar();
        OnSizeChanged();
    }

    private void OnBufferChanged()
    {
        // DispatcherTimer already runs on UI thread, but Changed may be called
        // from background threads (e.g. when buffer is resized externally).
        Dispatcher.BeginInvoke(InvalidateVisual, DispatcherPriority.Render);
    }

    private void OnSizeChanged()
    {
        if (_buffer is null || _charW <= 0 || _charH <= 0) return;
        int cols = Math.Max(10, (int)(ActualWidth  / _charW));
        int rows = Math.Max(4,  (int)(ActualHeight / _charH));
        _buffer.Resize(cols, rows);
    }

    // ─── Rendering ───────────────────────────────────────────────────────────

    protected override Size MeasureOverride(Size av) => av;
    protected override Size ArrangeOverride(Size s)  => s;

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(BrushBlack, null, new Rect(0, 0, ActualWidth, ActualHeight));

        if (_buffer is null) return;

        int rows = _buffer.Rows;
        int cols = _buffer.Cols;

        // If scrolled into scrollback, render from scrollback rows
        for (int row = 0; row < rows; row++)
        {
            int bufRow = _scrollOffset > 0
                ? -((_scrollOffset - row))   // negative = scrollback
                : row;

            var cells = _buffer.GetRow(bufRow);
            double y   = row * _charH;

            RenderRow(dc, cells, y, cols);
        }

        // Cursor (only when showing live screen)
        if (_scrollOffset == 0 && _buffer.CursorVisible && _cursorPhase)
        {
            double cx = _buffer.CursorX * _charW;
            double cy = _buffer.CursorY * _charH;
            dc.DrawRectangle(BrushCursor, null, new Rect(cx, cy, _charW, _charH));
        }

        // Scrollback indicator
        if (_scrollOffset > 0)
        {
            var hint = MakeText($"↑ Scrollback ({_scrollOffset}L) — Esc to return",
                BrushScrollFg, _typeface);
            dc.DrawText(hint, new Point(4, 2));
        }
    }

    private void RenderRow(DrawingContext dc, TerminalCell[] cells, double y, int cols)
    {
        // Pass 1: background fills (only non-default)
        int c = 0;
        while (c < cols && c < cells.Length)
        {
            ref var cell = ref cells[c];
            if (!cell.Attrs.Background.IsDefault)
            {
                var bgColor = Resolve(cell.Attrs.Background, false);
                int end = c + 1;
                while (end < cols && end < cells.Length &&
                       cells[end].Attrs.Background == cell.Attrs.Background)
                    end++;
                dc.DrawRectangle(GetBrush(bgColor), null,
                    new Rect(c * _charW, y, (end - c) * _charW, _charH));
                c = end;
            }
            else c++;
        }

        // Pass 2: text (group consecutive cells with same fg + bold flag)
        c = 0;
        var sb = new System.Text.StringBuilder(cols);
        while (c < cols && c < cells.Length)
        {
            ref var cell = ref cells[c];
            char ch = cell.Char is '\0' ? ' ' : cell.Char;
            if (ch == ' ' && cell.Attrs.Foreground.IsDefault && !cell.Attrs.Reverse)
            {
                c++;
                continue;
            }

            var fgColor = ResolveEffectiveFg(ref cells[c]);
            bool bold   = cell.Attrs.Bold;

            // Find run end
            int end = c + 1;
            while (end < cols && end < cells.Length)
            {
                char next = cells[end].Char is '\0' ? ' ' : cells[end].Char;
                if (ResolveEffectiveFg(ref cells[end]) != fgColor || cells[end].Attrs.Bold != bold) break;
                if (next == ' ' && cells[end].Attrs.Foreground.IsDefault && !cells[end].Attrs.Reverse) break;
                end++;
            }

            sb.Clear();
            for (int i = c; i < end; i++)
                sb.Append(cells[i].Char is '\0' ? ' ' : cells[i].Char);

            var typeface = bold ? _typefaceBold : _typeface;
            var ft = MakeText(sb.ToString(), GetBrush(fgColor), typeface);
            dc.DrawText(ft, new Point(c * _charW, y));
            c = end;
        }
    }

    // ─── Keyboard ────────────────────────────────────────────────────────────

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var text = KeyToVt(e);
        if (text is not null)
        {
            e.Handled = true;
            KeyInput?.Invoke(text);
        }
    }

    protected override void OnTextInput(TextCompositionEventArgs e)
    {
        base.OnTextInput(e);
        if (!string.IsNullOrEmpty(e.Text))
        {
            e.Handled = true;
            KeyInput?.Invoke(e.Text);
        }
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_buffer is null) return;

        int delta = e.Delta > 0 ? ScrollbackPage : -ScrollbackPage;
        _scrollOffset = Math.Clamp(_scrollOffset + delta, 0, _buffer.ScrollbackCount);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == Key.Escape && _scrollOffset > 0)
        {
            _scrollOffset = 0;
            InvalidateVisual();
            e.Handled = true;
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void MeasureChar()
    {
        var ft = MakeText("M", Brushes.White, _typeface);
        _charW = ft.Width;
        _charH = ft.Height;
    }

    private FormattedText MakeText(string text, Brush brush, Typeface tf) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            tf, FontSize, brush, _ppd);

    private static Color Resolve(TerminalColor c, bool fg)
    {
        var (r, g, b) = c.Resolve(fg);
        return Color.FromRgb(r, g, b);
    }

    private static Color ResolveEffectiveFg(ref TerminalCell cell)
    {
        if (cell.Attrs.Reverse)
        {
            var (r, g, b) = cell.Attrs.Background.Resolve(false);
            return Color.FromRgb(r, g, b);
        }
        return Resolve(cell.Attrs.Foreground, true);
    }

    private static SolidColorBrush GetBrush(Color c)
    {
        if (_brushCache.TryGetValue(c, out var b)) return b;
        b = MakeBrush(c);
        _brushCache[c] = b;
        return b;
    }

    private static SolidColorBrush MakeBrush(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static string? KeyToVt(KeyEventArgs e)
    {
        bool ctrl  = (e.KeyboardDevice.Modifiers & ModifierKeys.Control) != 0;
        bool shift = (e.KeyboardDevice.Modifiers & ModifierKeys.Shift)   != 0;

        if (ctrl)
        {
            return e.Key switch
            {
                Key.C => "\x03",
                Key.D => "\x04",
                Key.Z => "\x1A",
                Key.A => "\x01",
                Key.E => "\x05",
                Key.K => "\x0B",
                Key.L => "\x0C",
                Key.R => "\x12",
                Key.U => "\x15",
                Key.W => "\x17",
                Key.OemOpenBrackets or Key.OemCloseBrackets => "\x1B",
                _    => null,
            };
        }

        return e.Key switch
        {
            Key.Enter  => "\r",
            Key.Back   => "\x7F",
            Key.Tab    => "\t",
            Key.Escape => "\x1B",
            Key.Up     => "\x1B[A",
            Key.Down   => "\x1B[B",
            Key.Right  => "\x1B[C",
            Key.Left   => "\x1B[D",
            Key.Home   => "\x1B[H",
            Key.End    => "\x1B[F",
            Key.Delete => "\x1B[3~",
            Key.Insert => "\x1B[2~",
            Key.PageUp   => "\x1B[5~",
            Key.PageDown => "\x1B[6~",
            Key.F1  => "\x1BOP",
            Key.F2  => "\x1BOQ",
            Key.F3  => "\x1BOR",
            Key.F4  => "\x1BOS",
            Key.F5  => "\x1B[15~",
            Key.F6  => "\x1B[17~",
            Key.F7  => "\x1B[18~",
            Key.F8  => "\x1B[19~",
            Key.F9  => "\x1B[20~",
            Key.F10 => "\x1B[21~",
            Key.F11 => "\x1B[23~",
            Key.F12 => "\x1B[24~",
            _ => null,
        };
    }
}
