using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

// C# port of the "MA 1 + MA 2" Pine indicator, plus a third line. Each line is named after the
// colour it is drawn in, so what the chart shows and what the rules say cannot drift apart:
//   blue   - 60m RMA13
//   purple - 60m RMA55
//   yellow - 45m RMA13
// All three feed the direction filter; how they stack decides which way the bot may trade.
public class RmaLines {
    private static readonly Color BlueLineColor = Color.Blue;
    private static readonly Color PurpleLineColor = Color.Purple;
    private static readonly Color YellowLineColor = Color.Yellow;

    private readonly List<RmaLine> _lines;

    public RmaLines(Chart chart, Bars chartBars, RmaSeriesSet rmaSeries, int thickness) {
        _lines = new List<RmaLine> {
            new RmaLine(chart, chartBars, "BLUE", rmaSeries.Blue, BlueLineColor, thickness),
            new RmaLine(chart, chartBars, "PURPLE", rmaSeries.Purple, PurpleLineColor, thickness),
            new RmaLine(chart, chartBars, "YELLOW", rmaSeries.Yellow, YellowLineColor, thickness)
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
