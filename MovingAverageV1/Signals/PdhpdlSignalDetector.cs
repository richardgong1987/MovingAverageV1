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
    private readonly ConsecutiveEntryGate _entryGate;
    private readonly double _minMaDistance;

    public PdhpdlSignalDetector(Bars chartBars, RmaSeriesSet rmaSeries, Atr14Series rmaSourceAtr14, MarketStructure marketStructure,
        ConsecutiveEntryGate entryGate, double minMaDistance) {
        _chartBars = chartBars;
        _rmaSeries = rmaSeries;
        _rmaSourceAtr14 = rmaSourceAtr14;
        _marketStructure = marketStructure;
        _entryGate = entryGate;
        _minMaDistance = minMaDistance;
    }

    public PdhpdlSignalModel DetectOnClosedBar() {
        PdhpdlSignalModel signalModel = new();
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
        signalModel.MaDistance = double.NaN;
        signalModel.MinMaDistance = _minMaDistance;

        if (!_rmaSeries.TryReadConfirmedTrend(out RmaTrendReadingModel trend))
            return;

        signalModel.HasRmaData = true;
        signalModel.RmaSourceBarTime = trend.SourceBarTime;
        signalModel.BlueRma = trend.Blue;
        signalModel.PurpleRma = trend.Purple;
        signalModel.YellowRma = trend.Yellow;
        signalModel.MaDistance = CalculateMaDistance(trend);
    }

    // ATR 必须取蓝紫线所在周期上的同一根已收 K 线：拿图表周期的 ATR 去除以高周期均线的间距，
    // 分子分母量纲不同，算出来的倍数没有意义。
    private double CalculateMaDistance(RmaTrendReadingModel trend) {
        if (!_rmaSourceAtr14.TryGetValue(_rmaSeries.BluePurpleConfirmedIndex, out double atr))
            return double.NaN;

        return Math.Abs(trend.Blue - trend.Purple) / atr;
    }
}
