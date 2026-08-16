using System;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

// The fast/slow RMA pair the strategy trades off: both are read together (relative position,
// MaDistance), so they must live on the same timeframe and be read at the same bar index.
public class DualRmaSeries {
    public DualRmaSeries(MarketData marketData, IIndicatorsAccessor indicators, string symbolName, Bars chartBars,
        RmaLinesConfigModel config) {
        Fast = new RmaSeries(marketData, indicators, symbolName, chartBars, config.Source, config.FastPeriod,
            config.HigherTimeFrameMinutes);
        Slow = new RmaSeries(marketData, indicators, symbolName, chartBars, config.Source, config.SlowPeriod,
            config.HigherTimeFrameMinutes);
    }

    public RmaSeries Fast { get; }

    public RmaSeries Slow { get; }

    public Bars SourceBars => Fast.SourceBars;

    // Last fully closed source bar. Anything read alongside the RMA values (ATR, for instance)
    // must use this same index, otherwise the two numbers describe different bars.
    public int ConfirmedIndex => Fast.ConfirmedIndex;

    public bool TryGetLastConfirmedValues(out DateTime sourceBarTime, out double fastRma, out double slowRma) {
        sourceBarTime = DateTime.MinValue;
        fastRma = double.NaN;
        slowRma = double.NaN;

        int confirmedIndex = ConfirmedIndex;
        if (confirmedIndex < 0)
            return false;

        fastRma = Fast.Values[confirmedIndex];
        slowRma = Slow.Values[confirmedIndex];
        if (double.IsNaN(fastRma) || double.IsInfinity(fastRma) ||
            double.IsNaN(slowRma) || double.IsInfinity(slowRma))
            return false;

        sourceBarTime = SourceBars.OpenTimes[confirmedIndex];
        return true;
    }
}
