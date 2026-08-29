using System;

namespace cAlgo.Robots;

public class MainBiz {
    public static void Evaluate(PdhpdlSignalModel signalModel, CandleModel current, CandleModel previous, CandleModel earlier,
        PivotEntryGate entryGate) {
        HanJinSignalScanModel scanResult = HanJinSignals26.Scan(current, previous, earlier);
        signalModel.IsLongSignal = IsLongSignal(signalModel, scanResult, current, previous, earlier, entryGate);
        signalModel.IsShortSignal = IsShortSignal(signalModel, scanResult, current, previous, earlier, entryGate);
    }

    private static bool IsShortSignal(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier, PivotEntryGate entryGate) {
        if (!signalModel.HasRmaData)
            return false;

        if (!IsGapExpanding(signalModel, SignalSideModel.Sell))
            return false;

        /**
         * 作空要的是空头排列：黄 < 蓝 < 紫，也就是 45M RMA13 < 60M RMA13 < 60M RMA55。
         * 排不出来就是趋势没站在空头这一边，跳过。
         */
        if (!RmaUtils.IsBearishStack(signalModel.YellowRma, signalModel.BlueRma, signalModel.PurpleRma))
            return false;

        if (!MatchesShortPattern(signalModel, scanResult, current, previous, earlier))
            return false;

        // 每一笔作空都要吃掉一个新的 LL：MarketStructure 最后标出的必须是 LL（LH 不算），
        // 而且要是上一笔作空之后才新出的那一个。
        return entryGate.IsAllowed(PdhpdlTradeDirectionModel.Short, signalModel.LatestPivot, signalModel.PivotCount);
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
        CandleModel previous, CandleModel earlier, PivotEntryGate entryGate) {
        if (!signalModel.HasRmaData)
            return false;

        if (!IsGapExpanding(signalModel, SignalSideModel.Buy))
            return false;

        /**
         * 作多要的是多头排列：黄 > 蓝 > 紫，也就是 45M RMA13 > 60M RMA13 > 60M RMA55。
         * 排不出来就是趋势没站在多头这一边，跳过。
         */
        if (!RmaUtils.IsBullishStack(signalModel.YellowRma, signalModel.BlueRma, signalModel.PurpleRma)) {
            return false;
        }

        if (!MatchesLongPattern(signalModel, scanResult, current, previous, earlier))
            return false;

        // 每一笔作多都要吃掉一个新的 HH：MarketStructure 最后标出的必须是 HH（HL 不算），
        // 而且要是上一笔作多之后才新出的那一个。
        return entryGate.IsAllowed(PdhpdlTradeDirectionModel.Long, signalModel.LatestPivot, signalModel.PivotCount);
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

    // 蓝紫线拉开还不够，得在「继续拉开」：把这 N 根 15 分钟 K 线里开口的增量用 60 分钟 ATR14
    // 归一化，至少要有 X 个 ATR 才放行。震荡里开口来回收窄，这道闸门把那里的形态挡在外面。
    // 空头的开口是 PurpleRma - BlueRma，正好是 GapExpansion 的相反数，所以取负再比。
    // X = 0 表示这道闸门关闭；此时 ATR 缺失也不该把所有交易挡掉。
    private static bool IsGapExpanding(PdhpdlSignalModel signalModel, SignalSideModel side) {
        if (signalModel.MinGapExpansion <= 0.0)
            return true;

        if (double.IsNaN(signalModel.GapExpansion))
            return false;

        double expansion = side == SignalSideModel.Buy ? signalModel.GapExpansion : -signalModel.GapExpansion;
        return expansion >= signalModel.MinGapExpansion;
    }

    // 信号必须长在黄蓝这条带上：45M RMA13 ~ 60M RMA13 之间的区域，碰到其中任意一条线、
    // 或者整根落在两条线之间，都算数（影线算数）。构成这个形态的 K 线里至少要有一根和这条带重叠。
    // 哪条线在上不固定（多头黄在上，空头蓝在上），所以带的上下沿要取两条线的最大最小值。
    // 传进来的只能是这个形态自己用到的那几根 —— pinbar 一根、吞没两根、分型和孕线三根。
    // 笼统地拿三根去判断是错的：单根形态会被隔壁那根的触碰放行。
    private static bool AnyTouchesYellowBlueBand(PdhpdlSignalModel signalModel, params CandleModel[] patternCandles) {
        if (double.IsNaN(signalModel.YellowRma) || double.IsNaN(signalModel.BlueRma))
            return false;

        double bandLow = Math.Min(signalModel.YellowRma, signalModel.BlueRma);
        double bandHigh = Math.Max(signalModel.YellowRma, signalModel.BlueRma);

        foreach (CandleModel candle in patternCandles) {
            signalModel.KeyLevel = "";
            if (candle.Low <= bandHigh && candle.High >= bandLow)
                return true;
        }

        return false;
    }

    private static bool ShortTop(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current, CandleModel previous,
        CandleModel earlier) {
        if (scanResult.FractalTop != SignalSideModel.Sell || !Utils.AnyBarIsShort(current))
            return false;

        if (!AnyTouchesYellowBlueBand(signalModel, current, previous, earlier))
            return false;

        signalModel.Label = "S_Top";
        signalModel.SL = previous.High;
        return true;
    }

    private static bool ShortHarami(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier) {
        if (scanResult.HaramiSingle != SignalSideModel.Sell)
            return false;

        if (!AnyTouchesYellowBlueBand(signalModel, current, previous, earlier))
            return false;

        signalModel.Label = "S_Harami";
        signalModel.SL = Math.Max(previous.High, current.High);
        return true;
    }

    private static bool ShortEngulf(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous) {
        if (scanResult.Engulf != SignalSideModel.Sell)
            return false;

        if (!AnyTouchesYellowBlueBand(signalModel, current, previous))
            return false;

        signalModel.Label = "S_Eng";
        signalModel.SL = current.High;
        return true;
    }

    private static bool ShortPinBar(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current) {
        if (scanResult.Pinbar != SignalSideModel.Sell)
            return false;

        if (!AnyTouchesYellowBlueBand(signalModel, current))
            return false;

        signalModel.Label = "S_Pin";
        signalModel.SL = current.High;
        return true;
    }

    private static bool LongBottom(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier) {
        if (scanResult.FractalBottom != SignalSideModel.Buy || !Utils.AnyBarIsLong(current))
            return false;

        if (!AnyTouchesYellowBlueBand(signalModel, current, previous, earlier))
            return false;

        signalModel.Label = "L_Bot";
        signalModel.SL = previous.Low;
        return true;
    }

    private static bool LongHarami(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier) {
        if (scanResult.HaramiSingle != SignalSideModel.Buy)
            return false;

        if (!AnyTouchesYellowBlueBand(signalModel, current, previous, earlier))
            return false;

        signalModel.Label = "L_Harami";
        signalModel.SL = Math.Min(previous.Low, current.Low);
        return true;
    }

    private static bool LongEngulf(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous) {
        if (scanResult.Engulf != SignalSideModel.Buy)
            return false;

        if (!AnyTouchesYellowBlueBand(signalModel, current, previous))
            return false;

        signalModel.Label = "L_Eng";
        signalModel.SL = current.Low;
        return true;
    }

    private static bool LongPinbar(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current) {
        if (scanResult.Pinbar != SignalSideModel.Buy)
            return false;

        if (!AnyTouchesYellowBlueBand(signalModel, current))
            return false;

        signalModel.Label = "L_Pin";
        signalModel.SL = current.Low;
        return true;
    }
}
