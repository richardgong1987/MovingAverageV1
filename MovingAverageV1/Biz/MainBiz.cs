using System;

namespace cAlgo.Robots;

public class MainBiz {
    public static void Evaluate(PdhpdlSignalModel signalModel, CandleModel current, CandleModel previous, CandleModel earlier,
        ConsecutiveEntryGate entryGate) {
        HanJinSignalScanModel scanResult = HanJinSignals26.Scan(current, previous, earlier);
        signalModel.IsShortSignal = IsShortSignal(signalModel, scanResult, current, previous, earlier, entryGate);
        signalModel.IsLongSignal = IsLongSignal(signalModel, scanResult, current, previous, earlier, entryGate);
    }

    private static bool IsShortSignal(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier, ConsecutiveEntryGate entryGate) {
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

        if (!MatchesShortPattern(signalModel, scanResult, current, previous, earlier))
            return false;

        // 连续第 2 笔以上的作空，必须等 MarketStructure 又新标出一个 LL。
        return entryGate.IsAllowed(PdhpdlTradeDirectionModel.Short, signalModel.LowerLowCount, signalModel.HigherHighCount);
    }

    private static bool MatchesShortPattern(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier) {
        if (ShortPinBar(signalModel, scanResult, current)) {
            return true;
        }

        if (ShortEngulf(signalModel, scanResult, current, previous)) {
            return true;
        }

        if (ShortTop(signalModel, scanResult, current, previous, earlier)) {
            return true;
        }

        if (ShortHarami(signalModel, scanResult, current, previous, earlier)) {
            return true;
        }

        return false;
    }

    private static bool IsLongSignal(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier, ConsecutiveEntryGate entryGate) {
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

        if (!MatchesLongPattern(signalModel, scanResult, current, previous, earlier))
            return false;

        // 连续第 2 笔以上的作多，必须等 MarketStructure 又新标出一个 HH。
        return entryGate.IsAllowed(PdhpdlTradeDirectionModel.Long, signalModel.LowerLowCount, signalModel.HigherHighCount);
    }

    private static bool MatchesLongPattern(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier) {
        if (LongPinbar(signalModel, scanResult, current)) {
            return true;
        }

        if (LongEngulf(signalModel, scanResult, current, previous)) {
            return true;
        }

        if (LongBottom(signalModel, scanResult, current, previous, earlier)) {
            return true;
        }

        if (LongHarami(signalModel, scanResult, current, previous, earlier)) {
            return true;
        }

        return false;
    }

    // 信号必须长在快线上：构成这个形态的 K 线里，至少有一根把快线夹在自己的高低点之间（影线算数）。
    // 传进来的只能是这个形态自己用到的那几根 —— pinbar 一根、吞没两根、分型和孕线三根。
    // 笼统地拿三根去判断是错的：单根形态会被隔壁那根的触碰放行。
    private static bool AnyTouchesFastRma(PdhpdlSignalModel signalModel, params CandleModel[] patternCandles) {
        if (double.IsNaN(signalModel.FastRma))
            return false;

        foreach (CandleModel candle in patternCandles) {
            if (candle.Low <= signalModel.FastRma && candle.High >= signalModel.FastRma)
                return true;
        }

        return false;
    }

    private static bool ShortTop(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current, CandleModel previous,
        CandleModel earlier) {
        if (scanResult.FractalTop != SignalSideModel.Sell || !Utils.AnyBarIsShort(current))
            return false;

        if (!AnyTouchesFastRma(signalModel, current, previous, earlier))
            return false;

        signalModel.Label = "S_Top";
        signalModel.SL = previous.High;
        return true;
    }

    private static bool ShortHarami(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier) {
        if (scanResult.HaramiSingle != SignalSideModel.Sell)
            return false;

        if (!AnyTouchesFastRma(signalModel, current, previous, earlier))
            return false;

        signalModel.Label = "S_Harami";
        signalModel.SL = Math.Max(previous.High, current.High);
        return true;
    }

    private static bool ShortEngulf(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous) {
        if (scanResult.Engulf != SignalSideModel.Sell)
            return false;

        if (!AnyTouchesFastRma(signalModel, current, previous))
            return false;

        signalModel.Label = "S_Eng";
        signalModel.SL = current.High;
        return true;
    }

    private static bool ShortPinBar(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current) {
        if (scanResult.Pinbar != SignalSideModel.Sell)
            return false;

        if (!AnyTouchesFastRma(signalModel, current))
            return false;

        signalModel.Label = "S_Pin";
        signalModel.SL = current.High;
        return true;
    }

    private static bool LongBottom(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier) {
        if (scanResult.FractalBottom != SignalSideModel.Buy || !Utils.AnyBarIsLong(current))
            return false;

        if (!AnyTouchesFastRma(signalModel, current, previous, earlier))
            return false;

        signalModel.Label = "L_Bot";
        signalModel.SL = previous.Low;
        return true;
    }

    private static bool LongHarami(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier) {
        if (scanResult.HaramiSingle != SignalSideModel.Buy)
            return false;

        if (!AnyTouchesFastRma(signalModel, current, previous, earlier))
            return false;

        signalModel.Label = "L_Harami";
        signalModel.SL = Math.Min(previous.Low, current.Low);
        return true;
    }

    private static bool LongEngulf(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous) {
        if (scanResult.Engulf != SignalSideModel.Buy)
            return false;

        if (!AnyTouchesFastRma(signalModel, current, previous))
            return false;

        signalModel.Label = "L_Eng";
        signalModel.SL = current.Low;
        return true;
    }

    private static bool LongPinbar(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current) {
        if (scanResult.Pinbar != SignalSideModel.Buy)
            return false;

        if (!AnyTouchesFastRma(signalModel, current))
            return false;

        signalModel.Label = "L_Pin";
        signalModel.SL = current.Low;
        return true;
    }
}
