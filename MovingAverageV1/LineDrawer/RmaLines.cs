using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

// C# port of the "MA 1 + MA 2" Pine indicator, plus a third reference line:
//   MA 1 (fast) blue, MA 2 (slow) yellow — the pair the strategy trades off,
//   MA 3 (mid)  purple — a shorter-timeframe RMA drawn for context only.
public class RmaLines {
    private static readonly Color FastColor = Color.Blue;
    private static readonly Color SlowColor = Color.Yellow;
    private static readonly Color MidColor = Color.Purple;

    private readonly List<RmaLine> _lines;

    public RmaLines(Chart chart, Bars chartBars, DualRmaSeries signalPair, RmaSeries midSeries, int thickness) {
        _lines = new List<RmaLine> {
            new RmaLine(chart, chartBars, "FAST", signalPair.Fast, FastColor, thickness),
            new RmaLine(chart, chartBars, "SLOW", signalPair.Slow, SlowColor, thickness),
            new RmaLine(chart, chartBars, "MID", midSeries, MidColor, thickness)
        };
    }

    public void Draw() {
        foreach (RmaLine line in _lines) {
            line.Draw();
        }
    }

    public void Clear() {
        foreach (RmaLine line in _lines) {
            line.Clear();
        }
    }
}
