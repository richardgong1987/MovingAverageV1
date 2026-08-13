using System;

namespace cAlgo.Robots;

public class MainBiz {
    public static void Evaluate(PdhpdlSignalModel signalModel, CandleModel current, CandleModel previous, CandleModel earlier,
        ConsecutiveEntryGate entryGate) {
        HanJinSignalScanModel scanResult = HanJinSignals26.Scan(current, previous, earlier);
        signalModel.IsShortSignal = IsShortSignal(signalModel, scanResult, current, previous, entryGate);
        signalModel.IsLongSignal = IsLongSignal(signalModel, scanResult, current, previous, entryGate);
    }

    private static bool IsShortSignal(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, ConsecutiveEntryGate entryGate) {
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

        if (!MatchesShortPattern(signalModel, scanResult, current, previous))
            return false;

        // 连续第 2 笔以上的作空，必须等 MarketStructure 又新标出一个 LL。
        return entryGate.IsAllowed(PdhpdlTradeDirectionModel.Short, signalModel.LowerLowCount, signalModel.HigherHighCount);
    }

    private static bool MatchesShortPattern(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous) {
        if (ShortPinBar(signalModel, scanResult, current)) {
            return true;
        }

        if (ShortEngulf(signalModel, scanResult, current)) {
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
        CandleModel previous, ConsecutiveEntryGate entryGate) {
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

        if (!MatchesLongPattern(signalModel, scanResult, current, previous))
            return false;

        // 连续第 2 笔以上的作多，必须等 MarketStructure 又新标出一个 HH。
        return entryGate.IsAllowed(PdhpdlTradeDirectionModel.Long, signalModel.LowerLowCount, signalModel.HigherHighCount);
    }

    private static bool MatchesLongPattern(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous) {
        if (LongPinbar(signalModel, scanResult, current)) {
            return true;
        }

        if (LongEngulf(signalModel, scanResult, current)) {
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
