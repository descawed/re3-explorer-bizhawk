using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Re3Explorer;

public class CallTablePanel(IList<(int, IList<uint>)> callHistory): Panel {
    private int _maxColumns;

    private const int RowHeight = 30;
    private const int TextMarginX = 5;
    private const int FrameColumnWidth = 80;
    private const int CallColumnWidth = 100;

    private bool _drawEnabled = true;

    public bool DrawEnabled {
        get => _drawEnabled;
        set {
            if (value && !_drawEnabled) {
                Invalidate();
            }

            _drawEnabled = value;
        }
    }

    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);

        if (!DrawEnabled) {
            return;
        }

        var g = e.Graphics;
        var startRow = callHistory.Count - Math.Max(1, -AutoScrollPosition.Y / RowHeight + 1);
        var endRow = Math.Max(startRow - (ClientRectangle.Height / RowHeight + 1), 0);
        var callStart = -AutoScrollPosition.X - FrameColumnWidth;
        var startColumn = callStart >= 0 ? Math.Max(0, callStart / CallColumnWidth) + 1 : 0;
        var endColumn = Math.Max(startColumn, startColumn + (ClientRectangle.Width - FrameColumnWidth) / CallColumnWidth) + 1;
        
        for (int row = startRow, x = 0, y = 0; row > endRow; row--) {
            var (frame, calls) = callHistory[row];

            int callIndex;
            if (startColumn == 0) {
                g.DrawRectangle(Pens.Black, 0, y, FrameColumnWidth, RowHeight);
                var frameString = $"{frame}";
                var textSize = g.MeasureString(frameString, Font);
                g.DrawString(frameString, Font, Brushes.Black, TextMarginX, y + (RowHeight - textSize.Height) / 2);
                callIndex = 0;
                x += FrameColumnWidth;
            } else {
                callIndex = startColumn - 1;
            }

            for (var column = callIndex; column + 1 < endColumn; column++) {
                g.DrawRectangle(Pens.Black, x, y, CallColumnWidth, RowHeight);

                if (column < calls.Count) {
                    var callString = $"{calls[column]:X08}";
                    var textSize = g.MeasureString(callString, Font);
                    g.DrawString(callString, Font, Brushes.Black, x + TextMarginX,
                        y + (RowHeight - textSize.Height) / 2);
                }

                x += CallColumnWidth;
            }
            
            x = 0;
            y += RowHeight;
        }
    }

    protected override void OnScroll(ScrollEventArgs se) {
        base.OnScroll(se);
        if (DrawEnabled) {
            Invalidate();
        }
    }

    public void Add(int frame, IList<uint> calls) {
        callHistory.Add((frame, calls));
        var scrollHeight = RowHeight * callHistory.Count;
        Size scrollSize;
        if (calls.Count > _maxColumns) {
            _maxColumns = calls.Count;
            scrollSize = new Size(FrameColumnWidth + CallColumnWidth * _maxColumns, scrollHeight);
        } else {
            scrollSize = AutoScrollMinSize with { Height = scrollHeight };
        }
        AutoScrollMinSize = scrollSize;
        if (DrawEnabled) {
            Invalidate();
        }
    }
}