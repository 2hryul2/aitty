namespace Aitty.Terminal;

/// <summary>
/// Fixed-size character grid with scrollback.
/// Thread-safe for concurrent writes; <see cref="Changed"/> is raised on every mutation.
/// </summary>
public sealed class TerminalBuffer
{
    private readonly object _lock = new();
    private TerminalCell[,] _grid;
    private readonly List<TerminalCell[]> _scrollback = new();

    public int Cols { get; private set; }
    public int Rows { get; private set; }
    public int CursorX { get; private set; }
    public int CursorY { get; private set; }
    public bool CursorVisible { get; private set; } = true;
    public int ScrollbackCount => _scrollback.Count;

    /// <summary>Fires after every mutation. Subscribe from UI thread to call InvalidateVisual.</summary>
    public event Action? Changed;

    public TerminalBuffer(int cols = 120, int rows = 40)
    {
        Cols  = cols;
        Rows  = rows;
        _grid = new TerminalCell[rows, cols];
        FillEmpty(_grid, rows, cols);
    }

    // ─── Resize ──────────────────────────────────────────────────────────────

    public void Resize(int cols, int rows)
    {
        lock (_lock)
        {
            var newGrid = new TerminalCell[rows, cols];
            FillEmpty(newGrid, rows, cols);
            int copyRows = Math.Min(rows, Rows);
            int copyCols = Math.Min(cols, Cols);
            for (int r = 0; r < copyRows; r++)
                for (int c = 0; c < copyCols; c++)
                    newGrid[r, c] = _grid[r, c];
            Cols  = cols;
            Rows  = rows;
            _grid = newGrid;
            CursorX = Math.Clamp(CursorX, 0, cols - 1);
            CursorY = Math.Clamp(CursorY, 0, rows - 1);
        }
        RaiseChanged();
    }

    // ─── Cell Access ─────────────────────────────────────────────────────────

    /// <summary>
    /// Return a snapshot row. row 0..Rows-1 = live screen;
    /// negative row reads from scrollback (−1 = last scrollback row).
    /// </summary>
    public TerminalCell[] GetRow(int row)
    {
        lock (_lock)
        {
            var result = new TerminalCell[Cols];
            if (row >= 0 && row < Rows)
            {
                for (int c = 0; c < Cols; c++) result[c] = _grid[row, c];
            }
            else if (row < 0)
            {
                int sbIdx = _scrollback.Count + row;
                if (sbIdx >= 0 && sbIdx < _scrollback.Count)
                {
                    var src = _scrollback[sbIdx];
                    Array.Copy(src, result, Math.Min(Cols, src.Length));
                }
            }
            return result;
        }
    }

    // ─── Write ───────────────────────────────────────────────────────────────

    public void WriteChar(char ch, TerminalAttributes attrs)
    {
        lock (_lock)
        {
            if (CursorX >= Cols)
            {
                DoLineFeed();
                CursorX = 0;
            }
            _grid[CursorY, CursorX] = new TerminalCell { Char = ch, Attrs = attrs };
            CursorX++;
        }
        RaiseChanged();
    }

    public void CarriageReturn()
    {
        lock (_lock) { CursorX = 0; }
        RaiseChanged();
    }

    public void LineFeed()
    {
        lock (_lock) { DoLineFeed(); }
        RaiseChanged();
    }

    public void Backspace()
    {
        lock (_lock) { if (CursorX > 0) CursorX--; }
        RaiseChanged();
    }

    public void Tab()
    {
        lock (_lock)
        {
            int next = ((CursorX / 8) + 1) * 8;
            CursorX = Math.Min(next, Cols - 1);
        }
        RaiseChanged();
    }

    // ─── Cursor ──────────────────────────────────────────────────────────────

    public void SetCursor(int row, int col)
    {
        lock (_lock)
        {
            CursorY = Math.Clamp(row, 0, Rows - 1);
            CursorX = Math.Clamp(col, 0, Cols - 1);
        }
        RaiseChanged();
    }

    public void MoveCursor(int deltaRow, int deltaCol)
    {
        lock (_lock)
        {
            CursorY = Math.Clamp(CursorY + deltaRow, 0, Rows - 1);
            CursorX = Math.Clamp(CursorX + deltaCol, 0, Cols - 1);
        }
        RaiseChanged();
    }

    public void SetCursorVisible(bool visible)
    {
        CursorVisible = visible;
        RaiseChanged();
    }

    // ─── Erase ───────────────────────────────────────────────────────────────

    /// <param name="mode">0 = cursor→end-of-line, 1 = start→cursor, 2 = whole line</param>
    public void EraseLine(int mode)
    {
        lock (_lock)
        {
            int start = mode == 1 ? 0      : CursorX;
            int end   = mode == 0 ? Cols   : (mode == 1 ? CursorX + 1 : Cols);
            for (int c = start; c < end; c++)
                _grid[CursorY, c] = TerminalCell.Empty;
        }
        RaiseChanged();
    }

    /// <param name="mode">0 = cursor→end, 1 = start→cursor, 2/3 = whole screen</param>
    public void EraseDisplay(int mode)
    {
        lock (_lock)
        {
            if (mode is 2 or 3)
            {
                FillEmpty(_grid, Rows, Cols);
                return;
            }
            // Partial erase
            int startRow = mode == 0 ? CursorY : 0;
            int endRow   = mode == 0 ? Rows     : CursorY + 1;
            for (int r = startRow; r < endRow; r++)
                for (int c = 0; c < Cols; c++)
                    _grid[r, c] = TerminalCell.Empty;
            // Cursor line partial
            if (mode == 0) EraseLine(0);
            else if (mode == 1) EraseLine(1);
        }
        RaiseChanged();
    }

    public void Clear()
    {
        lock (_lock)
        {
            FillEmpty(_grid, Rows, Cols);
            CursorX = 0;
            CursorY = 0;
        }
        RaiseChanged();
    }

    public void ClearScrollback()
    {
        lock (_lock) { _scrollback.Clear(); }
    }

    // ─── Internal ────────────────────────────────────────────────────────────

    /// <summary>Must be called inside _lock.</summary>
    private void DoLineFeed()
    {
        if (CursorY < Rows - 1)
        {
            CursorY++;
        }
        else
        {
            // Scroll: push top row to scrollback, shift up, blank bottom
            var saved = new TerminalCell[Cols];
            for (int c = 0; c < Cols; c++) saved[c] = _grid[0, c];
            _scrollback.Add(saved);

            for (int r = 0; r < Rows - 1; r++)
                for (int c = 0; c < Cols; c++)
                    _grid[r, c] = _grid[r + 1, c];

            for (int c = 0; c < Cols; c++)
                _grid[Rows - 1, c] = TerminalCell.Empty;
        }
    }

    private static void FillEmpty(TerminalCell[,] grid, int rows, int cols)
    {
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                grid[r, c] = TerminalCell.Empty;
    }

    private void RaiseChanged() => Changed?.Invoke();
}
