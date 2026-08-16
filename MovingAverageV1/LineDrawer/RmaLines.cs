using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

// C# port of the "MA 1 + MA 2" Pine indicator, plus a third line:
//   MA 1 (fast) blue, MA 2 (slow) yellow — the pair on the trend timeframe,
//   MA 3 (mid)  purple — the shorter-timeframe RMA.
// All three feed the direction filter; how they stack decides which way the bot may trade.
public class RmaLines {
    private static readonly Color FastColor = Color.Blue;
    private static readonly Color SlowColor = Color.Yellow;
    private static readonly Color MidColor = Color.Purple;

    private readonly List<RmaLine> _lines;

    public RmaLines(Chart chart, Bars chartBars, RmaSeriesSet rmaSeries, int thickness) {
        _lines = new List<RmaLine> {
            new RmaLine(chart, chartBars, "FAST", rmaSeries.Fast, FastColor, thickness),
            new RmaLine(chart, chartBars, "SLOW", rmaSeries.Slow, SlowColor, thickness),
            new RmaLine(chart, chartBars, "MID", rmaSeries.Mid, MidColor, thickness)
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
