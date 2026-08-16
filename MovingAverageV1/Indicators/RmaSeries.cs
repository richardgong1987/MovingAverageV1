using System;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

// One Welles-Wilder (Pine ta.rma) moving average together with the bars it is computed on.
// The timeframe belongs to the line, not to the overlay as a whole: blue/purple and yellow each
// pick their own, and none of them is the chart's timeframe.
public class RmaSeries {
    private readonly MovingAverage _movingAverage;

    public RmaSeries(MarketData marketData, IIndicatorsAccessor indicators, string symbolName, int period, int timeFrameMinutes) {
        SourceBars = marketData.GetBars(ToTimeFrame(timeFrameMinutes), symbolName);

        _movingAverage = indicators.MovingAverage(SourceBars.ClosePrices, period, MovingAverageType.WilderSmoothing);
    }

    public Bars SourceBars { get; }

    public IndicatorDataSeries Values => _movingAverage.Result;

    // Last fully closed source bar. Anything read alongside the RMA values (ATR, for instance)
    // must use this same index, otherwise the two numbers describe different bars.
    public int ConfirmedIndex => SourceBars.Count - 2;

    // The value on that closed bar. Not enough history, or an average still warming up, reads
    // as NaN/Infinity — reported as "no value" rather than handed to the caller.
    public bool TryGetConfirmedValue(out double value) {
        value = double.NaN;

        int confirmedIndex = ConfirmedIndex;
        if (confirmedIndex < 0)
            return false;

        value = Values[confirmedIndex];
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static TimeFrame ToTimeFrame(int minutes) {
        return minutes switch {
            45 => TimeFrame.Minute45,
            60 => TimeFrame.Hour,
            _ => throw new ArgumentOutOfRangeException(nameof(minutes), minutes, "Unsupported timeframe minutes for RmaSeries.")
        };
    }
}
