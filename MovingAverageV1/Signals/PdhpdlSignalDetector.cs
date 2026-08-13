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

    public PdhpdlSignalModel DetectOnClosedBar(StrategyModel strategy) {
        PdhpdlSignalModel signalModel = new();
        signalModel.Strategy = strategy;
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

        FillRmaData(signalModel);
        MainBiz.Evaluate(signalModel, current, previous, earlier);

        return signalModel;
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
