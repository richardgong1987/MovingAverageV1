using System;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

// C# port of the "Market Structure HH, HL, LH and LL" Pine indicator
// (pine-script/lib/Market-Structure-HH-HL-LH-and-LL.pine). Chart-only: it draws the zigzag
// and its swing labels and feeds nothing into the strategy.
//
// The swing engine is a breakout zigzag, not a fractal one:
//     toUp   = this bar's high is the highest of the last ZigZagLength bars
//     toDown = this bar's low  is the lowest  of the last ZigZagLength bars
// Trend starts at +1 and flips only on the opposite breakout, and the swing that just ended is
// recorded at the moment of the flip. A leg therefore only appears once price has already
// broken the other way; that lag is the indicator's design, not a porting artefact.
public class MarketStructure {
    // The Pine study exposes these as inputs. Here they stay constants: they change nothing
    // about strategy or operational behaviour, and chart-only knobs must not become cBot
    // parameters. Values are the Pine defaults.
    private const int ZigZagLength = 40;
    private const int ZigZagWidth = 2;
    private const int LabelFontSize = 8; // Pine size.tiny

    private static readonly Color ZigZagColor = Color.Blue;

    private const string Prefix = "MARKET_STRUCTURE_";

    // Mirrors Pine's max_lines_count: cap the chart objects so a long history cannot spawn an
    // unbounded number of them. The oldest are dropped first.
    private const int MaxObjectsPerKind = 500;

    // Pine's ta.barssince is na until the condition has been true at least once.
    private const int NeverHappened = -1;

    private readonly Chart _chart;
    private readonly Bars _bars;

    private readonly Queue<string> _legNames = new();
    private readonly Queue<string> _labelNames = new();
    private readonly HashSet<string> _liveNames = new();

    private int _nextBarIndex;

    private int _trend = 1;
    private bool _hasPreviousTrend;
    private bool _previousBarToUp;
    private bool _previousBarToDown;
    private int _barsSinceUpBreakout = NeverHappened;
    private int _barsSinceDownBreakout = NeverHappened;

    // Pine pushes onto arrays pre-filled with na and only ever reads the last two entries, so
    // two shifting slots reproduce it exactly — including the NaN that makes the first
    // comparison false and labels the first high "LH" and the first low "HL".
    private double _latestHigh = double.NaN;
    private double _previousHigh = double.NaN;
    private int _latestHighIndex = -1;

    private double _latestLow = double.NaN;
    private double _previousLow = double.NaN;
    private int _latestLowIndex = -1;

    public MarketStructure(Chart chart, Bars bars) {
        _chart = chart;
        _bars = bars;
    }

    // Advance over every bar that has closed since the last call. Working on closed bars only
    // keeps the zigzag from repainting as the newest candle moves.
    public void Update() {
        int lastClosedBarIndex = _bars.Count - 2;

        for (; _nextBarIndex <= lastClosedBarIndex; _nextBarIndex++) {
            ProcessBar(_nextBarIndex);
        }
    }

    private void ProcessBar(int index) {
        bool toUp = false;
        bool toDown = false;

        // ta.highest / ta.lowest are na until `length` bars exist, and `high >= na` is false.
        if (index >= ZigZagLength - 1) {
            toUp = _bars.HighPrices[index] >= HighestHigh(index, ZigZagLength);
            toDown = _bars.LowPrices[index] <= LowestLow(index, ZigZagLength);
        }

        // ta.barssince(toUp[1]): the Pine source counts from the bar AFTER a breakout, which is
        // what excludes the breakout bar itself from the swing window measured below.
        _barsSinceUpBreakout = StepBarsSince(_barsSinceUpBreakout, index > 0 && _previousBarToUp);
        _barsSinceDownBreakout = StepBarsSince(_barsSinceDownBreakout, index > 0 && _previousBarToDown);

        int previousTrend = _hasPreviousTrend ? _trend : 1; // nz(trend[1], 1)
        int trend = previousTrend == 1 && toDown ? -1 : previousTrend == -1 && toUp ? 1 : previousTrend;

        (double swingLow, int swingLowIndex) = LowestSwing(index, SwingWindow(_barsSinceUpBreakout));
        (double swingHigh, int swingHighIndex) = HighestSwing(index, SwingWindow(_barsSinceDownBreakout));

        // ta.change(trend) != 0. On the first bar trend[1] is na, so the comparison is na and no
        // swing is ever recorded there.
        if (_hasPreviousTrend && trend != previousTrend) {
            // Flipping up confirms the low that ended the decline; flipping down confirms the high.
            if (trend == 1) {
                _previousLow = _latestLow;
                _latestLow = swingLow;
                _latestLowIndex = swingLowIndex;
            }

            if (trend == -1) {
                _previousHigh = _latestHigh;
                _latestHigh = swingHigh;
                _latestHighIndex = swingHighIndex;
            }

            DrawSwing(trend);
        }

        _trend = trend;
        _hasPreviousTrend = true;
        _previousBarToUp = toUp;
        _previousBarToDown = toDown;
    }

    private static int StepBarsSince(int barsSince, bool conditionHolds) {
        if (conditionHolds)
            return 0;

        return barsSince == NeverHappened ? NeverHappened : barsSince + 1;
    }

    // nz(barsSince > 0 ? barsSince : 1): both "never happened" and "happened on this bar"
    // collapse to a one-bar window.
    private static int SwingWindow(int barsSince) {
        return barsSince > 0 ? barsSince : 1;
    }

    private double HighestHigh(int index, int length) {
        double highest = double.MinValue;

        for (int i = index - length + 1; i <= index; i++) {
            highest = Math.Max(highest, _bars.HighPrices[i]);
        }

        return highest;
    }

    private double LowestLow(int index, int length) {
        double lowest = double.MaxValue;

        for (int i = index - length + 1; i <= index; i++) {
            lowest = Math.Min(lowest, _bars.LowPrices[i]);
        }

        return lowest;
    }

    // Pine locates the pivot with ta.barssince(low_val == low), which reports the MOST RECENT
    // bar where the condition held. Hence <= rather than <: on a tie the later bar wins.
    private (double Value, int Index) LowestSwing(int index, int length) {
        double lowest = double.MaxValue;
        int lowestIndex = index;

        for (int i = Math.Max(0, index - length + 1); i <= index; i++) {
            if (_bars.LowPrices[i] <= lowest) {
                lowest = _bars.LowPrices[i];
                lowestIndex = i;
            }
        }

        return (lowest, lowestIndex);
    }

    private (double Value, int Index) HighestSwing(int index, int length) {
        double highest = double.MinValue;
        int highestIndex = index;

        for (int i = Math.Max(0, index - length + 1); i <= index; i++) {
            if (_bars.HighPrices[i] >= highest) {
                highest = _bars.HighPrices[i];
                highestIndex = i;
            }
        }

        return (highest, highestIndex);
    }

    private void DrawSwing(int trend) {
        if (trend == 1)
            DrawLeg(_latestHighIndex, _latestHigh, _latestLowIndex, _latestLow);

        if (trend == -1)
            DrawLeg(_latestLowIndex, _latestLow, _latestHighIndex, _latestHigh);

        // Pine relabels BOTH pivots on every flip, so the side that did not move repeats its
        // previous text at its previous point. Keying the chart object on the pivot bar makes
        // that repeat overwrite itself instead of stacking duplicates.
        bool isHigherHigh = _latestHigh > _previousHigh;
        DrawPivotLabel($"{Prefix}HIGH_{_latestHighIndex}", isHigherHigh ? "HH" : "LH", _latestHighIndex, _latestHigh,
            isHigherHigh ? Color.Green : Color.Red, isAbovePrice: true);

        bool isLowerLow = _latestLow < _previousLow;
        DrawPivotLabel($"{Prefix}LOW_{_latestLowIndex}", isLowerLow ? "LL" : "HL", _latestLowIndex, _latestLow,
            isLowerLow ? Color.Red : Color.Green, isAbovePrice: false);
    }

    private void DrawLeg(int fromIndex, double fromPrice, int toIndex, double toPrice) {
        // Until both sides have a pivot, Pine's line coordinates are na and nothing is drawn.
        if (double.IsNaN(fromPrice) || double.IsNaN(toPrice))
            return;

        string name = $"{Prefix}LEG_{fromIndex}_{toIndex}";
        _chart.DrawTrendLine(name, fromIndex, fromPrice, toIndex, toPrice, ZigZagColor, ZigZagWidth, LineStyle.Solid);
        Register(_legNames, name);
    }

    private void DrawPivotLabel(string name, string text, int barIndex, double price, Color color, bool isAbovePrice) {
        if (double.IsNaN(price) || barIndex < 0)
            return;

        ChartText label = _chart.DrawText(name, text, barIndex, price, color);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        // Pine uses label.style_label_down above highs and label.style_label_up below lows.
        label.VerticalAlignment = isAbovePrice ? VerticalAlignment.Top : VerticalAlignment.Bottom;
        label.FontSize = LabelFontSize;
        Register(_labelNames, name);
    }

    // Redrawing an existing name overwrites its chart object, so it must not be queued twice:
    // the eviction would then delete an object that is still current.
    private void Register(Queue<string> names, string name) {
        if (!_liveNames.Add(name))
            return;

        names.Enqueue(name);

        if (names.Count <= MaxObjectsPerKind)
            return;

        string oldest = names.Dequeue();
        _liveNames.Remove(oldest);
        _chart.RemoveObject(oldest);
    }
}
