namespace Aitty.Terminal;

/// <summary>
/// A terminal color — default, ANSI-16, ANSI-256 (cube/grayscale), or 24-bit RGB.
/// </summary>
public readonly struct TerminalColor : IEquatable<TerminalColor>
{
    private readonly byte _kind; // 0=default, 1=ansi16, 2=rgb
    private readonly byte _r, _g, _b;

    public static readonly TerminalColor Default = default;

    private TerminalColor(byte kind, byte r, byte g, byte b)
        => (_kind, _r, _g, _b) = (kind, r, g, b);

    public static TerminalColor FromAnsi16(int index)
        => new(1, (byte)index, 0, 0);

    public static TerminalColor FromRgb(byte r, byte g, byte b)
        => new(2, r, g, b);

    public bool IsDefault => _kind == 0;

    /// <summary>Resolve to (R, G, B) using xterm default palette.</summary>
    public (byte R, byte G, byte B) Resolve(bool foreground)
    {
        return _kind switch
        {
            1 => Ansi16ToRgb(_r),
            2 => (_r, _g, _b),
            _ => foreground
                ? ((byte)0xCC, (byte)0xCC, (byte)0xCC)  // default fg: light gray
                : ((byte)0x00, (byte)0x00, (byte)0x00),  // default bg: black
        };
    }

    private static (byte, byte, byte) Ansi16ToRgb(byte idx) => idx switch
    {
        0  => (0x00, 0x00, 0x00),
        1  => (0xAA, 0x00, 0x00),
        2  => (0x00, 0xAA, 0x00),
        3  => (0xAA, 0xAA, 0x00),
        4  => (0x00, 0x00, 0xAA),
        5  => (0xAA, 0x00, 0xAA),
        6  => (0x00, 0xAA, 0xAA),
        7  => (0xAA, 0xAA, 0xAA),
        8  => (0x55, 0x55, 0x55),
        9  => (0xFF, 0x55, 0x55),
        10 => (0x55, 0xFF, 0x55),
        11 => (0xFF, 0xFF, 0x55),
        12 => (0x55, 0x55, 0xFF),
        13 => (0xFF, 0x55, 0xFF),
        14 => (0x55, 0xFF, 0xFF),
        15 => (0xFF, 0xFF, 0xFF),
        _  => (0xCC, 0xCC, 0xCC),
    };

    public bool Equals(TerminalColor other)
        => _kind == other._kind && _r == other._r && _g == other._g && _b == other._b;

    public override bool Equals(object? obj) => obj is TerminalColor c && Equals(c);
    public override int GetHashCode() => HashCode.Combine(_kind, _r, _g, _b);
    public static bool operator ==(TerminalColor a, TerminalColor b) => a.Equals(b);
    public static bool operator !=(TerminalColor a, TerminalColor b) => !a.Equals(b);
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>SGR attribute state carried per character cell.</summary>
public struct TerminalAttributes
{
    public TerminalColor Foreground;
    public TerminalColor Background;
    public bool Bold;
    public bool Dim;
    public bool Italic;
    public bool Underline;
    public bool Reverse;

    public static TerminalAttributes Default => new()
    {
        Foreground = TerminalColor.Default,
        Background = TerminalColor.Default,
    };

    public void Reset() => this = Default;
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>A single character cell in the terminal grid.</summary>
public struct TerminalCell
{
    public char Char;
    public TerminalAttributes Attrs;

    public static TerminalCell Empty => new() { Char = ' ', Attrs = TerminalAttributes.Default };
}
