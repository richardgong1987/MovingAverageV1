using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

// The three RMA lines the cBot runs, built from one config:
//   Fast / Slow - the trend pair, both on the same timeframe. Direction filtering and MaDistance
//                 read them together, so they must always be taken from the same closed bar.
//   Mid         - a reference line on its own timeframe (45m by default). Drawn only; no rule
//                 reads it.
public class RmaSeriesSet {
    public RmaSeriesSet(MarketData marketData, IIndicatorsAccessor indicators, string symbolName, Bars chartBars,
        RmaLinesConfigModel config) {
        Fast = new RmaSeries(marketData, indicators, symbolName, chartBars, config.Source, config.FastPeriod,
            config.HigherTimeFrameMinutes);
        Slow = new RmaSeries(marketData, indicators, symbolName, chartBars, config.Source, config.SlowPeriod,
            config.HigherTimeFrameMinutes);
        Mid = new RmaSeries(marketData, indicators, symbolName, chartBars, config.Source, config.MidPeriod,
            config.MidTimeFrameMinutes);
    }

    public RmaSeries Fast { get; }

    public RmaSeries Slow { get; }

    public RmaSeries Mid { get; }

    // Fast and Slow share a timeframe, so one index describes both. Whatever is read alongside
    // them — the ATR behind MaDistance — must use this same index.
    public int TrendConfirmedIndex => Fast.ConfirmedIndex;

    // Both trend values off the last closed bar of their shared timeframe. Either one missing
    // means there is no usable reading: a rule that compared one live value against one stale
    // value would be comparing different bars.
    public bool TryReadConfirmedTrend(out RmaTrendReadingModel reading) {
        reading = null;

        if (!Fast.TryGetConfirmedValue(out double fastRma) || !Slow.TryGetConfirmedValue(out double slowRma))
            return false;

        reading = new RmaTrendReadingModel(Fast.SourceBars.OpenTimes[TrendConfirmedIndex], fastRma, slowRma);
        return true;
    }
}
