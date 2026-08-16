using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

// The three RMA lines the cBot runs, built from one config and named after the colour they are
// drawn in:
//   Blue   - 60m RMA13, paired with purple. MaDistance measures the gap between the two, so they
//   Purple - 60m RMA55, must always be taken from the same closed bar.
//   Yellow - 45m RMA13. Its own timeframe means its own closed bar, which is the point: it
//            reacts first.
// The direction filter compares all three (RmaUtils.IsBullishStack / IsBearishStack).
public class RmaSeriesSet {
    public RmaSeriesSet(MarketData marketData, IIndicatorsAccessor indicators, string symbolName, RmaLinesConfigModel config) {
        Blue = new RmaSeries(marketData, indicators, symbolName, config.RMABluePeriod, config.BluePurpleTimeFrameMinutes);
        Purple = new RmaSeries(marketData, indicators, symbolName, config.RMAPurplePeriod, config.BluePurpleTimeFrameMinutes);
        Yellow = new RmaSeries(marketData, indicators, symbolName, config.RMAYellowPeriod, config.YellowTimeFrameMinutes);
    }

    public RmaSeries Blue { get; }

    public RmaSeries Purple { get; }

    public RmaSeries Yellow { get; }

    // Blue and purple share a timeframe, so one index describes both. Whatever is read alongside
    // them — the ATR behind MaDistance — must use this same index.
    public int BluePurpleConfirmedIndex => Blue.ConfirmedIndex;

    // All three lines off the last closed bar of their own timeframe. Any one of them missing
    // means there is no usable reading at all: the direction filter compares all three, so a
    // partial reading could only ever produce a wrong answer, never a cautious one.
    public bool TryReadConfirmedTrend(out RmaTrendReadingModel reading) {
        reading = null;

        if (!Blue.TryGetConfirmedValue(out double blueRma) || !Purple.TryGetConfirmedValue(out double purpleRma) ||
            !Yellow.TryGetConfirmedValue(out double yellowRma))
            return false;

        reading = new RmaTrendReadingModel(Blue.SourceBars.OpenTimes[BluePurpleConfirmedIndex], blueRma, purpleRma, yellowRma);
        return true;
    }
}
