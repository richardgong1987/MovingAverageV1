using System;

namespace cAlgo.Robots;

public class MainBiz {
    // 开仓点到中轨至少要留出这么多个 R 的空间，不够就不值得开。
    private const double MinMiddleBandDistanceR = 1.1;

    public static void Evaluate(PdhpdlSignalModel signalModel, CandleModel current, CandleModel previous, CandleModel earlier) {
        HanJinSignalScanModel scanResult = HanJinSignals26.Scan(current, previous, earlier);
        signalModel.IsShortSignal = IsShortSignal(signalModel, scanResult, current, previous);
        signalModel.IsLongSignal = IsLongSignal(signalModel, scanResult, current, previous);
    }

    private static bool IsShortSignal(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous) {
        if (!signalModel.HasRmaData)
            return false;


        RmaPositionModel rmaPosition = RmaUtils.GetFastToSlowPosition(signalModel.FastRma, signalModel.SlowRma);

        /**
         * 蓝线在上面，作多。但这里是专门作空的。所以就跳过
         */
        if (rmaPosition == RmaPositionModel.FastAboveSlow)
            return false;

        if (!Utils.IsStrategyModeSatisfied(signalModel, current, SignalSideModel.Sell)) {
            return false;
        }

        // if (!(signalModel.manuallyDragonBand.DragonBothBands || signalModel.manuallyDragonBand.DragonUpperBand)) {
        //     return false;
        // }

        // 三根判定 K 线都没碰到布林上轨，就不在做空的位置上。
        if (!signalModel.TouchesUpperBand)
            return false;

        if (!MatchesShortPattern(signalModel, scanResult, current, previous))
            return false;

        // 空单往下走，中轨在开仓点下方才算有空间。
        return HasRoomToMiddleBand(signalModel, current, current.Close - signalModel.MiddleBand);
    }

    private static bool MatchesShortPattern(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous) {
        if (ShortPinBar(signalModel, scanResult, current) && signalModel.TouchesUpperBandCurrent) {
            return true;
        }

        if (ShortEngulf(signalModel, scanResult, current) && signalModel.TouchesUpperBandCurrent) {
            return true;
        }

        if (ShortTop(signalModel, scanResult, current, previous)) {
            return true;
        }

        if (ShortHarami(signalModel, scanResult, current, previous)) {
            return true;
        }

        return false;
    }

    private static bool IsLongSignal(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous) {
        if (!signalModel.HasRmaData)
            return false;

        RmaPositionModel rmaPosition = RmaUtils.GetFastToSlowPosition(signalModel.FastRma, signalModel.SlowRma);

        /**
         * 蓝线在下面，代表，只作空。这但这里都是作多的，所以就不走这里的逻辑了。
         */
        if (rmaPosition == RmaPositionModel.FastBelowSlow) {
            return false;
        }

        if (!Utils.IsStrategyModeSatisfied(signalModel, current, SignalSideModel.Buy)) {
            return false;
        }
        //
        // if (!(signalModel.manuallyDragonBand.DragonBothBands || signalModel.manuallyDragonBand.DragonLowerBand)) {
        //     return false;
        // }

        // 三根判定 K 线都没碰到布林下轨，就不在做多的位置上。
        if (!signalModel.TouchesLowerBand)
            return false;

        if (!MatchesLongPattern(signalModel, scanResult, current, previous))
            return false;

        // 多单往上走，中轨在开仓点上方才算有空间。
        return HasRoomToMiddleBand(signalModel, current, signalModel.MiddleBand - current.Close);
    }

    private static bool MatchesLongPattern(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous) {
        if (LongPinbar(signalModel, scanResult, current) && signalModel.TouchesLowerBandCurrent) {
            return true;
        }

        if (LongEngulf(signalModel, scanResult, current) && signalModel.TouchesLowerBandCurrent) {
            return true;
        }

        if (LongBottom(signalModel, scanResult, current, previous)) {
            return true;
        }

        if (LongHarami(signalModel, scanResult, current, previous)) {
            return true;
        }

        return false;
    }

    // 形态确定了 SL 之后才有 R 可算，所以这道闸门只能放在形态匹配之后。
    // distanceToMiddle 是顺着盈利方向的有符号距离：中轨跑到反方向去了就是负数，直接不合格。
    // R 用「收盘价到 SL」近似，与 PdhpdlOrderPlanner 的真实 R 差一个止损偏移和回撤入场模式。
    private static bool HasRoomToMiddleBand(PdhpdlSignalModel signalModel, CandleModel current, double distanceToMiddle) {
        if (double.IsNaN(distanceToMiddle))
            return false;

        double risk = Math.Abs(current.Close - signalModel.SL);

        if (risk <= 0.0)
            return false;

        return distanceToMiddle >= MinMiddleBandDistanceR * risk;
    }

    private static bool ShortTop(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous) {
        if (scanResult.FractalTop != SignalSideModel.Sell || !Utils.AnyBarIsShort(current))
            return false;

        signalModel.Label = "S_Top";
        signalModel.SL = previous.High;
        return true;
    }

    private static bool ShortHarami(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous) {
        if (scanResult.HaramiSingle != SignalSideModel.Sell)
            return false;

        signalModel.Label = "S_Harami";
        signalModel.SL = Math.Max(previous.High, current.High);
        return true;
    }

    private static bool ShortEngulf(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current) {
        if (scanResult.Engulf != SignalSideModel.Sell)
            return false;

        signalModel.Label = "S_Eng";
        signalModel.SL = current.High;
        return true;
    }

    private static bool ShortPinBar(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current) {
        if (scanResult.Pinbar != SignalSideModel.Sell)
            return false;

        signalModel.Label = "S_Pin";
        signalModel.SL = current.High;
        return true;
    }

    private static bool LongBottom(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous) {
        if (scanResult.FractalBottom != SignalSideModel.Buy || !Utils.AnyBarIsLong(current))
            return false;

        signalModel.Label = "L_Bot";
        signalModel.SL = previous.Low;
        return true;
    }

    private static bool LongHarami(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous) {
        if (scanResult.HaramiSingle != SignalSideModel.Buy)
            return false;

        signalModel.Label = "L_Harami";
        signalModel.SL = Math.Min(previous.Low, current.Low);
        return true;
    }

    private static bool LongEngulf(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current) {
        if (scanResult.Engulf != SignalSideModel.Buy)
            return false;

        signalModel.Label = "L_Eng";
        signalModel.SL = current.Low;
        return true;
    }

    private static bool LongPinbar(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current) {
        if (scanResult.Pinbar != SignalSideModel.Buy)
            return false;

        signalModel.Label = "L_Pin";
        signalModel.SL = current.Low;
        return true;
    }
}
