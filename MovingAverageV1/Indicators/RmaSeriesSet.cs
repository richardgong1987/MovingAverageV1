using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

// The three RMA lines the cBot runs, built from one config:
//   Fast / Slow - the pair on the configured trend timeframe. MaDistance measures the gap
//                 between them, so they must always be taken from the same closed bar.
//   Mid         - on its own, shorter timeframe (45m by default). It has its own closed bar,
//                 which is the point: it reacts first.
// The direction filter compares all three (RmaUtils.IsBullishStack / IsBearishStack).
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

    // All three lines off the last closed bar of their own timeframe. Any one of them missing
    // means there is no usable reading at all: the direction filter compares all three, so a
    // partial reading could only ever produce a wrong answer, never a cautious one.
    public bool TryReadConfirmedTrend(out RmaTrendReadingModel reading) {
        reading = null;

        if (!Fast.TryGetConfirmedValue(out double fastRma) ||
            !Slow.TryGetConfirmedValue(out double slowRma) ||
            !Mid.TryGetConfirmedValue(out double midRma))
            return false;

        reading = new RmaTrendReadingModel(Fast.SourceBars.OpenTimes[TrendConfirmedIndex], fastRma, slowRma, midRma);
        return true;
    }
}
