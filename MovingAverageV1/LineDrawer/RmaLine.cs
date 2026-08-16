using System;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

// Draws one Welles-Wilder (ta.rma) moving average on the chart.
//
// The average lives on its own timeframe, higher than the chart's, so it is drawn as a stepped
// line that holds each value until that timeframe's bar confirms — faithful to
// request.security(tickerid, "60", ...) with lookahead off.
public class RmaLine {
    private const string Prefix = "RMA_";

    // Cap the number of segments per line so a long chart cannot spawn an
    // unbounded number of chart objects.
    private const int MaxSegmentsPerLine = 500;

    private readonly Chart _chart;
    private readonly Bars _chartBars;
    private readonly Bars _sourceBars;
    private readonly IndicatorDataSeries _values;
    private readonly string _lineKey;
    private readonly Color _color;
    private readonly int _thickness;
    private readonly List<string> _objectNames = new();

    private DateTime _lastChartOpenTime = DateTime.MinValue;

    public RmaLine(Chart chart, Bars chartBars, string lineKey, RmaSeries series, Color color, int thickness) {
        _chart = chart;
        _chartBars = chartBars;
        _lineKey = lineKey;
        _sourceBars = series.SourceBars;
        _values = series.Values;
        _color = color;
        _thickness = thickness;
    }

    // Redraw once per chart bar so the higher-timeframe line's held tail keeps
    // reaching the latest candle instead of trailing behind it.
    public void Draw() {
        if (_sourceBars.Count < 2 || _chartBars.Count < 1)
            return;

        DateTime currentChartOpenTime = _chartBars.OpenTimes[_chartBars.Count - 1];

        if (currentChartOpenTime == _lastChartOpenTime)
            return;

        _lastChartOpenTime = currentChartOpenTime;

        Clear();

        DrawSteppedLine(Math.Max(1, _sourceBars.Count - MaxSegmentsPerLine));
    }

    public void Clear() {
        foreach (string name in _objectNames) {
            _chart.RemoveObject(name);
        }

        _objectNames.Clear();
    }

    // Replicate request.security with lookahead off. A
    // higher-timeframe average value is only known once its bar closes, so it is
    // held flat from that close until the next close, then steps to the new
    // value. The last confirmed value is held out to the current chart time.
    private void DrawSteppedLine(int startIndex) {
        DateTime currentTime = _chartBars.OpenTimes[_chartBars.Count - 1];
        int lastConfirmedIndex = _sourceBars.Count - 2;

        for (int i = startIndex; i <= lastConfirmedIndex; i++) {
            double value = _values[i];

            if (double.IsNaN(value))
                continue;

            DateTime holdStart = _sourceBars.OpenTimes[i + 1];
            DateTime holdEnd = i + 2 < _sourceBars.Count ? _sourceBars.OpenTimes[i + 2] : currentTime;

            if (holdEnd <= holdStart)
                holdEnd = currentTime;

            DrawSegment($"{Prefix}{_lineKey}_H_{holdStart:yyyyMMddHHmm}", holdStart, value, holdEnd, value);

            DrawStepConnector(startIndex, i, holdStart, value);
        }
    }

    // Vertical jump between the previous held value and the current one.
    private void DrawStepConnector(int startIndex, int index, DateTime stepTime, double value) {
        if (index - 1 < startIndex)
            return;

        double previousValue = _values[index - 1];

        if (double.IsNaN(previousValue))
            return;

        DrawSegment($"{Prefix}{_lineKey}_V_{stepTime:yyyyMMddHHmm}", stepTime, previousValue, stepTime, value);
    }

    private void DrawSegment(string name, DateTime fromTime, double fromValue, DateTime toTime, double toValue) {
        _chart.DrawTrendLine(name, fromTime, fromValue, toTime, toValue, _color, _thickness, LineStyle.Solid);

        _objectNames.Add(name);
    }
}
