namespace Aitty.Terminal;

/// <summary>
/// State-machine parser for VT100/ANSI escape sequences.
/// Supported: SGR colors (16/256/RGB), CUP/CUU/CUD/CUF/CUB/CHA,
/// ED, EL, save/restore cursor, cursor show/hide, RIS, OSC (stub).
/// </summary>
public sealed class VT100Parser
{
    private enum State { Normal, Escape, Csi, Osc }

    private readonly TerminalBuffer _buf;

    private State _state   = State.Normal;
    private char  _csiFinal;
    private char  _csiInter;
    private readonly List<int>               _params    = new(8);
    private readonly System.Text.StringBuilder _paramBuf = new(16);

    private TerminalAttributes _attrs = TerminalAttributes.Default;

    // Saved cursor (DECSC / SCOSC)
    private int _savedX, _savedY;

    public VT100Parser(TerminalBuffer buffer) => _buf = buffer;

    // ─── Public API ──────────────────────────────────────────────────────────

    public void Feed(string text)
    {
        foreach (var ch in text) Step(ch);
    }

    public void Feed(ReadOnlySpan<char> text)
    {
        foreach (var ch in text) Step(ch);
    }

    public void Reset()
    {
        _state = State.Normal;
        _attrs.Reset();
        _params.Clear();
        _paramBuf.Clear();
    }

    // ─── State machine ───────────────────────────────────────────────────────

    private void Step(char ch)
    {
        switch (_state)
        {
            case State.Normal:  HandleNormal(ch);  break;
            case State.Escape:  HandleEscape(ch);  break;
            case State.Csi:     HandleCsi(ch);     break;
            case State.Osc:
                // Consume OSC until ST (ESC\) or BEL
                if (ch == '\x07' || ch == '\x9C') _state = State.Normal;
                else if (ch == '\x1B')             _state = State.Escape; // might be ESC\
                break;
        }
    }

    // ─── Normal ──────────────────────────────────────────────────────────────

    private void HandleNormal(char ch)
    {
        switch (ch)
        {
            case '\x1B': BeginEscape();       break;
            case '\r':   _buf.CarriageReturn(); break;
            case '\n':
            case '\x0B':
            case '\x0C': _buf.LineFeed();       break;
            case '\b':   _buf.Backspace();      break;
            case '\t':   _buf.Tab();            break;
            case '\x07': break; // BEL — ignore
            case '\x00': break; // NUL — ignore
            default:
                if (ch >= ' ') _buf.WriteChar(ch, _attrs);
                break;
        }
    }

    private void BeginEscape()
    {
        _state    = State.Escape;
        _csiInter = '\0';
    }

    // ─── ESC ─────────────────────────────────────────────────────────────────

    private void HandleEscape(char ch)
    {
        switch (ch)
        {
            case '[':   // CSI
                _state = State.Csi;
                _params.Clear();
                _paramBuf.Clear();
                _csiInter = '\0';
                break;

            case ']':   // OSC
                _state = State.Osc;
                break;

            case 'c':   // RIS - full reset
                _buf.Clear();
                _attrs.Reset();
                _state = State.Normal;
                break;

            case '7':   // DECSC - save cursor
                _savedX = _buf.CursorX;
                _savedY = _buf.CursorY;
                _state  = State.Normal;
                break;

            case '8':   // DECRC - restore cursor
                _buf.SetCursor(_savedY, _savedX);
                _state = State.Normal;
                break;

            case 'M':   // RI - reverse index (scroll down)
                if (_buf.CursorY > 0) _buf.MoveCursor(-1, 0);
                _state = State.Normal;
                break;

            case 'D':   // IND - index (scroll up / line feed)
                _buf.LineFeed();
                _state = State.Normal;
                break;

            case 'E':   // NEL - next line
                _buf.CarriageReturn();
                _buf.LineFeed();
                _state = State.Normal;
                break;

            case '\\':  // ST (string terminator after OSC) — already handled in Osc state
                _state = State.Normal;
                break;

            case '=':   // DECKPAM
            case '>':   // DECKPNM
            case '(':   // G0 charset
            case ')':   // G1 charset
            case '*':
            case '+':
                // Skip the next character (charset designator)
                _state = State.Normal;
                break;

            default:
                _state = State.Normal;
                break;
        }
    }

    // ─── CSI ─────────────────────────────────────────────────────────────────

    private void HandleCsi(char ch)
    {
        if (ch >= 0x20 && ch <= 0x2F)          // intermediate bytes
        {
            _csiInter = ch;
            return;
        }
        if (ch >= '0' && ch <= '9')             // digit
        {
            _paramBuf.Append(ch);
            return;
        }
        if (ch == ';')                          // param separator
        {
            FlushParam();
            return;
        }
        if (ch >= 0x40 && ch <= 0x7E)          // final byte
        {
            FlushParam();
            _csiFinal = ch;
            DispatchCsi();
            _params.Clear();
            _state = State.Normal;
        }
        // else: ignore malformed
    }

    private void FlushParam()
    {
        _params.Add(_paramBuf.Length > 0 && int.TryParse(_paramBuf.ToString(), out int v) ? v : 0);
        _paramBuf.Clear();
    }

    /// <summary>Get param by index with a default (0 means "use default").</summary>
    private int P(int idx, int def = 1) =>
        idx < _params.Count ? (_params[idx] == 0 ? def : _params[idx]) : def;

    /// <summary>Get param raw (0 stays 0).</summary>
    private int PR(int idx, int def = 0) =>
        idx < _params.Count ? _params[idx] : def;

    private void DispatchCsi()
    {
        // DEC private sequences: ESC [ ? ... h/l
        if (_csiInter == '?')
        {
            DispatchDecPrivate();
            return;
        }

        switch (_csiFinal)
        {
            // ── Cursor movement ─────────────────────────────────────────────
            case 'A': _buf.MoveCursor(-P(0),  0);         break; // CUU
            case 'B': _buf.MoveCursor( P(0),  0);         break; // CUD
            case 'C': _buf.MoveCursor( 0,  P(0));         break; // CUF
            case 'D': _buf.MoveCursor( 0, -P(0));         break; // CUB
            case 'E': _buf.SetCursor(_buf.CursorY + P(0), 0); break; // CNL
            case 'F': _buf.SetCursor(_buf.CursorY - P(0), 0); break; // CPL
            case 'G': _buf.SetCursor(_buf.CursorY, P(0) - 1); break; // CHA

            case 'H': // CUP
            case 'f': // HVP
                _buf.SetCursor(P(0) - 1, P(1) - 1);
                break;

            case 'd': // VPA - line position absolute
                _buf.SetCursor(P(0) - 1, _buf.CursorX);
                break;

            case 's': // SCOSC
                _savedX = _buf.CursorX;
                _savedY = _buf.CursorY;
                break;

            case 'u': // SCORC
                _buf.SetCursor(_savedY, _savedX);
                break;

            // ── Erase ───────────────────────────────────────────────────────
            case 'J': _buf.EraseDisplay(PR(0)); break; // ED
            case 'K': _buf.EraseLine(PR(0));    break; // EL

            // ── Scroll ──────────────────────────────────────────────────────
            case 'S': // SU - scroll up
                for (int i = 0; i < P(0); i++) _buf.LineFeed();
                break;

            case 'T': // SD - scroll down (rare)
                break;

            // ── SGR ─────────────────────────────────────────────────────────
            case 'm': ApplySgr(); break;

            // ── Insert/Delete (vi / nano use these) ─────────────────────────
            case 'P': // DCH - delete n chars at cursor
                _buf.EraseLine(0); // simplified: erase to EOL
                break;

            case '@': // ICH - insert n spaces
                break;

            case 'L': // IL - insert n lines
            case 'M': // DL - delete n lines
                break;

            // ── Device / Misc ────────────────────────────────────────────────
            case 'c': // DA - device attributes (ignore)
            case 'n': // DSR
            case 'r': // DECSTBM - scroll region (stub: ignore)
            case 'h': // SM
            case 'l': // RM
                break;
        }
    }

    private void DispatchDecPrivate()
    {
        switch (_csiFinal)
        {
            case 'h': // set mode
                foreach (var p in _params)
                    switch (p)
                    {
                        case 25: _buf.SetCursorVisible(true);  break; // DECTCEM
                        // 47/1049: alternate screen — ignore
                    }
                break;

            case 'l': // reset mode
                foreach (var p in _params)
                    switch (p)
                    {
                        case 25: _buf.SetCursorVisible(false); break;
                    }
                break;
        }
    }

    // ─── SGR ─────────────────────────────────────────────────────────────────

    private void ApplySgr()
    {
        if (_params.Count == 0)
        {
            _attrs.Reset();
            return;
        }

        int i = 0;
        while (i < _params.Count)
        {
            int p = _params[i];
            switch (p)
            {
                case 0:  _attrs.Reset();         break;
                case 1:  _attrs.Bold      = true; break;
                case 2:  _attrs.Dim       = true; break;
                case 3:  _attrs.Italic    = true; break;
                case 4:  _attrs.Underline = true; break;
                case 7:  _attrs.Reverse   = true; break;
                case 21: _attrs.Underline = true; break; // doubly underlined → treat as underline
                case 22: _attrs.Bold = _attrs.Dim = false; break;
                case 23: _attrs.Italic    = false; break;
                case 24: _attrs.Underline = false; break;
                case 27: _attrs.Reverse   = false; break;
                case 39: _attrs.Foreground = TerminalColor.Default; break;
                case 49: _attrs.Background = TerminalColor.Default; break;

                case >= 30 and <= 37:
                    _attrs.Foreground = TerminalColor.FromAnsi16(p - 30);
                    break;
                case >= 40 and <= 47:
                    _attrs.Background = TerminalColor.FromAnsi16(p - 40);
                    break;
                case >= 90 and <= 97:
                    _attrs.Foreground = TerminalColor.FromAnsi16(p - 90 + 8);
                    break;
                case >= 100 and <= 107:
                    _attrs.Background = TerminalColor.FromAnsi16(p - 100 + 8);
                    break;

                case 38: // extended foreground
                    i += TryParseExtColor(i + 1, out var fg) ? (fg.offset) : 0;
                    if (fg.color.HasValue) _attrs.Foreground = fg.color.Value;
                    break;

                case 48: // extended background
                    i += TryParseExtColor(i + 1, out var bg) ? (bg.offset) : 0;
                    if (bg.color.HasValue) _attrs.Background = bg.color.Value;
                    break;
            }
            i++;
        }
    }

    private bool TryParseExtColor(int start, out (bool HasValue, TerminalColor? color, int offset) result)
    {
        if (start >= _params.Count) { result = (false, null, 0); return false; }

        int mode = _params[start];
        if (mode == 2 && start + 3 < _params.Count) // RGB
        {
            result = (true,
                TerminalColor.FromRgb((byte)_params[start + 1], (byte)_params[start + 2], (byte)_params[start + 3]),
                3);
            return true;
        }
        if (mode == 5 && start + 1 < _params.Count) // 256-color
        {
            result = (true, Ansi256((_params[start + 1])), 2);
            return true;
        }
        result = (false, null, 0);
        return false;
    }

    private static TerminalColor Ansi256(int idx)
    {
        if (idx < 16) return TerminalColor.FromAnsi16(idx);

        if (idx >= 232) // grayscale ramp
        {
            byte v = (byte)(8 + (idx - 232) * 10);
            return TerminalColor.FromRgb(v, v, v);
        }

        // 6×6×6 color cube
        idx -= 16;
        int ri = idx / 36;
        int gi = (idx % 36) / 6;
        int bi = idx % 6;
        return TerminalColor.FromRgb(
            ri > 0 ? (byte)(55 + ri * 40) : (byte)0,
            gi > 0 ? (byte)(55 + gi * 40) : (byte)0,
            bi > 0 ? (byte)(55 + bi * 40) : (byte)0);
    }
}
