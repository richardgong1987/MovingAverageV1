using System;
using cAlgo.API;

namespace cAlgo.Robots;

// Reads the last fully closed bar and assembles the signal model, then hands the candle-pattern
// decision to MainBiz.
// OnBar fires when a new bar opens, so the closed bar is Count - 2.
public class PdhpdlSignalDetector {
    private readonly Bars _chartBars;
    private readonly RmaSeriesSet _rmaSeries;
    private readonly Atr14Series _rmaSourceAtr14;
    private readonly MarketStructure _marketStructure;
    private readonly PivotEntryGate _entryGate;
    private readonly double _minGapExpansion;
    private readonly int _gapLookbackBars;

    public PdhpdlSignalDetector(Bars chartBars, RmaSeriesSet rmaSeries, Atr14Series rmaSourceAtr14, MarketStructure marketStructure,
        PivotEntryGate entryGate, GapExpansionConfigModel gapExpansion) {
        _chartBars = chartBars;
        _rmaSeries = rmaSeries;
        _rmaSourceAtr14 = rmaSourceAtr14;
        _marketStructure = marketStructure;
        _entryGate = entryGate;
        _minGapExpansion = gapExpansion.MinExpansionAtr;

        // N 是按 15 分钟 K 线数的，蓝紫线却跑在 60 分钟上，所以这里换算成 60 分钟的根数：
        // N=4（1 小时前）→ 1 根，N=8（2 小时前）→ 2 根。不够一根 60 分钟 K 线的回看没有意义，
        // 与其悄悄当成 1 根，不如在启动时就报错。
        _gapLookbackBars = gapExpansion.LookbackMinutes / rmaSeries.BluePurpleTimeFrameMinutes;

        if (_gapLookbackBars < 1)
            throw new ArgumentOutOfRangeException(nameof(gapExpansion), gapExpansion.LookbackMinutes,
                $"Gap-expansion lookback must cover at least one {rmaSeries.BluePurpleTimeFrameMinutes}-minute bar.");
    }

    public PdhpdlSignalModel DetectOnClosedBar(bool IsUseWickRatio) {
        PdhpdlSignalModel signalModel = new();
        signalModel.IsUseWickRatio = IsUseWickRatio;
        if (_chartBars.Count < 2)
            return signalModel;

        int closedBarIndex = _chartBars.Count - 2; // last fully closed bar in OnBar()
        CandleModel current = ReadCandle(closedBarIndex);
        CandleModel previous = ReadCandle(closedBarIndex - 1);
        CandleModel earlier = ReadCandle(closedBarIndex - 2);

        signalModel.HasData = true;
        signalModel.BarIndex = closedBarIndex;
        signalModel.BarTime = _chartBars.OpenTimes[closedBarIndex];
        signalModel.Open = current.Open;
        signalModel.Close = current.Close;
        signalModel.High = current.High;
        signalModel.Low = current.Low;

        signalModel.LatestPivot = _marketStructure.LatestPivot;
        signalModel.PivotCount = _marketStructure.PivotCount;

        FillRmaData(signalModel);
        MainBiz.Evaluate(signalModel, current, previous, earlier, _entryGate);

        return signalModel;
    }


    private CandleModel ReadCandle(int index) {
        return new CandleModel(open: _chartBars.OpenPrices[index], high: _chartBars.HighPrices[index], low: _chartBars.LowPrices[index],
            close: _chartBars.ClosePrices[index]);
    }

    private void FillRmaData(PdhpdlSignalModel signalModel) {
        signalModel.BlueRma = double.NaN;
        signalModel.PurpleRma = double.NaN;
        signalModel.YellowRma = double.NaN;
        signalModel.GapExpansion = double.NaN;
        signalModel.MinGapExpansion = _minGapExpansion;

        if (!_rmaSeries.TryReadConfirmedTrend(out RmaTrendReadingModel trend))
            return;

        signalModel.HasRmaData = true;
        signalModel.RmaSourceBarTime = trend.SourceBarTime;
        signalModel.BlueRma = trend.Blue;
        signalModel.PurpleRma = trend.Purple;
        signalModel.YellowRma = trend.Yellow;
        signalModel.GapExpansion = CalculateGapExpansion(trend);
    }

    // 开口扩大幅度（多头视角）：(现在的蓝紫开口 - N 根 15 分钟 K 线之前的开口) / 60 分钟 ATR14。
    // ATR 必须取蓝紫线所在周期上的同一根已收 K 线：拿图表周期的 ATR 去除以高周期均线的间距，
    // 分子分母量纲不同，算出来的倍数没有意义。
    private double CalculateGapExpansion(RmaTrendReadingModel trend) {
        if (!_rmaSourceAtr14.TryGetValue(_rmaSeries.BluePurpleConfirmedIndex, out double atr) || atr <= 0.0)
            return double.NaN;

        if (!_rmaSeries.TryGetBluePurpleGap(_gapLookbackBars, out double pastGap))
            return double.NaN;

        double currentGap = trend.Blue - trend.Purple;
        return (currentGap - pastGap) / atr;
    }
}
