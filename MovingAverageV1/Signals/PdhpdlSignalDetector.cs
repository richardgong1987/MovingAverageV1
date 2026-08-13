using System;
using cAlgo.API;

namespace cAlgo.Robots;

// Reads the last fully closed bar and assembles the signal model, then hands the candle-pattern
// decision to MainBiz.
// OnBar fires when a new bar opens, so the closed bar is Count - 2.
public class PdhpdlSignalDetector {
    private readonly Bars _chartBars;
    private readonly DualRmaSeries _rmaSeries;
    public PdhpdlSignalDetector(Bars chartBars,DualRmaSeries rmaSeries) {
        _chartBars = chartBars;
        _rmaSeries = rmaSeries;
    }

    public PdhpdlSignalModel DetectOnClosedBar(StrategyModel strategy, BollingerFlatDetector detector,
        ManuallyDragonBand manuallyDragonBand) {
        PdhpdlSignalModel signalModel = new();
        signalModel.Strategy = strategy;
        signalModel.manuallyDragonBand = manuallyDragonBand;
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

        // 布林带逐根变化，每根 K 线只能和它自己那一根的带值比。bar index 只有这里有，
        // 所以触碰判断在这里算完，MainBiz 只读结果。
        signalModel.TouchesUpperBand = AnyBarTouchesBand(detector.bollingerBands.Top, closedBarIndex, current, previous, earlier);
        signalModel.TouchesUpperBandCurrent = TouchesBand(detector.bollingerBands.Top, closedBarIndex, current);
        signalModel.TouchesUpperBandPrevious = TouchesBand(detector.bollingerBands.Top, closedBarIndex - 1, previous);
        signalModel.TouchesUpperBandEarlier = TouchesBand(detector.bollingerBands.Top, closedBarIndex - 2, earlier);

        signalModel.TouchesLowerBand = AnyBarTouchesBand(detector.bollingerBands.Bottom, closedBarIndex, current, previous, earlier);
        signalModel.TouchesLowerBandCurrent = TouchesBand(detector.bollingerBands.Bottom, closedBarIndex, current);
        signalModel.TouchesLowerBandPrevious = TouchesBand(detector.bollingerBands.Bottom, closedBarIndex - 1, previous);
        signalModel.TouchesLowerBandEarlier = TouchesBand(detector.bollingerBands.Bottom, closedBarIndex - 2, earlier);

        // 中轨：MainBiz 用它算「开仓点到中轨还有几个 R」的空间过滤。
        signalModel.MiddleBand = detector.bollingerBands.Main[closedBarIndex];
        FillRmaData(signalModel);
        MainBiz.Evaluate(signalModel, current, previous, earlier);

        return signalModel;
    }

    private static bool AnyBarTouchesBand(IndicatorDataSeries band, int closedBarIndex, CandleModel current, CandleModel previous,
        CandleModel earlier) {
        return TouchesBand(band, closedBarIndex, current) || TouchesBand(band, closedBarIndex - 1, previous) ||
               TouchesBand(band, closedBarIndex - 2, earlier);
    }

    // 触碰 = 这根 K 线的最高/最低价把该根的带值夹在中间。
    private static bool TouchesBand(IndicatorDataSeries band, int barIndex, CandleModel candle) {
        if (barIndex < 0)
            return false;

        double bandValue = band[barIndex];

        // 样本不足的前几根上布林带是 NaN。NaN 参与比较恒为 false，显式挡掉是为了让意图看得见。
        if (double.IsNaN(bandValue))
            return false;

        return candle.Low <= bandValue && candle.High >= bandValue;
    }

    private CandleModel ReadCandle(int index) {
        return new CandleModel(open: _chartBars.OpenPrices[index], high: _chartBars.HighPrices[index], low: _chartBars.LowPrices[index],
            close: _chartBars.ClosePrices[index]);
    }

    private void FillRmaData(PdhpdlSignalModel signalModel) {
        signalModel.FastRma = double.NaN;
        signalModel.SlowRma = double.NaN;

        if (!_rmaSeries.TryGetLastConfirmedValues(out DateTime sourceBarTime, out double fastRma, out double slowRma))
            return;

        signalModel.HasRmaData = true;
        signalModel.RmaSourceBarTime = sourceBarTime;
        signalModel.FastRma = fastRma;
        signalModel.SlowRma = slowRma;
    }
}
