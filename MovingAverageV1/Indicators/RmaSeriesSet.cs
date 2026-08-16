using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

// The three RMA lines the cBot runs, built from one config and named after the colour they are
// drawn in:
//   Blue   - 60m RMA13, paired with purple. The gap between the two drives the GapExpansion
//   Purple - 60m RMA55, gate, so both must always be taken from the same closed bar.
//   Yellow - 45m RMA13. Its own timeframe means its own closed bar, which is the point: it
//            reacts first.
// The direction filter compares all three (RmaUtils.IsBullishStack / IsBearishStack).
public class RmaSeriesSet {
    public RmaSeriesSet(MarketData marketData, IIndicatorsAccessor indicators, string symbolName, RmaLinesConfigModel config) {
        Blue = new RmaSeries(marketData, indicators, symbolName, config.RMABluePeriod, config.BluePurpleTimeFrameMinutes);
        Purple = new RmaSeries(marketData, indicators, symbolName, config.RMAPurplePeriod, config.BluePurpleTimeFrameMinutes);
        Yellow = new RmaSeries(marketData, indicators, symbolName, config.RMAYellowPeriod, config.YellowTimeFrameMinutes);
        BluePurpleTimeFrameMinutes = config.BluePurpleTimeFrameMinutes;
    }

    public RmaSeries Blue { get; }

    public RmaSeries Purple { get; }

    public RmaSeries Yellow { get; }

    // The timeframe blue and purple run on. Callers that count back in some other unit (the
    // GapExpansion lookback is in 15-minute bars) need it to convert.
    public int BluePurpleTimeFrameMinutes { get; }

    // Blue and purple share a timeframe, so one index describes both. Whatever is read alongside
    // them — the ATR behind GapExpansion — must use this same index.
    public int BluePurpleConfirmedIndex => Blue.ConfirmedIndex;

    // 蓝紫开口（Blue - Purple），从最后一根已收 K 线往前数 barsAgo 根。历史不够、或者均线还没
    // 预热出值，就当成读不到 —— 拿 NaN 去算扩大幅度只会得出一个假的数字。
    public bool TryGetBluePurpleGap(int barsAgo, out double gap) {
        gap = double.NaN;

        int index = BluePurpleConfirmedIndex - barsAgo;
        if (index < 0)
            return false;

        double blueRma = Blue.Values[index];
        double purpleRma = Purple.Values[index];
        if (!double.IsFinite(blueRma) || !double.IsFinite(purpleRma))
            return false;

        gap = blueRma - purpleRma;
        return true;
    }

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
