using System;

namespace cAlgo.Robots;

public class MainBiz {
    public static void Evaluate(PdhpdlSignalModel signalModel, CandleModel current, CandleModel previous, CandleModel earlier,
        ConsecutiveEntryGate entryGate) {
        HanJinSignalScanModel scanResult = HanJinSignals26.Scan(current, previous, earlier);
        signalModel.IsLongSignal = IsLongSignal(signalModel, scanResult, current, previous, earlier, entryGate);
        signalModel.IsShortSignal = IsShortSignal(signalModel, scanResult, current, previous, earlier, entryGate);
    }

    private static bool IsShortSignal(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier, ConsecutiveEntryGate entryGate) {
        if (!signalModel.HasRmaData)
            return false;

        if (!IsMaDistanceSatisfied(signalModel))
            return false;

        /**
         * 作空要的是空头排列：紫 < 蓝 < 黄，也就是 45M RMA13 < 60M RMA13 < 60M RMA55。
         * 排不出来就是趋势没站在空头这一边，跳过。
         */
        if (!RmaUtils.IsBearishStack(signalModel.MidRma, signalModel.FastRma, signalModel.SlowRma))
            return false;

        if (!MatchesShortPattern(signalModel, scanResult, current, previous, earlier))
            return false;

        // 连续第 2 笔以上的作空，MarketStructure 最后标出的必须是 LL（LH 不算），
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
        CandleModel previous, CandleModel earlier, ConsecutiveEntryGate entryGate) {
        if (!signalModel.HasRmaData)
            return false;

        if (!IsMaDistanceSatisfied(signalModel))
            return false;

        /**
         * 作多要的是多头排列：紫 > 蓝 > 黄，也就是 45M RMA13 > 60M RMA13 > 60M RMA55。
         * 排不出来就是趋势没站在多头这一边，跳过。
         */
        if (!RmaUtils.IsBullishStack(signalModel.MidRma, signalModel.FastRma, signalModel.SlowRma)) {
            return false;
        }

        if (!MatchesLongPattern(signalModel, scanResult, current, previous, earlier))
            return false;

        // 连续第 2 笔以上的作多，MarketStructure 最后标出的必须是 HH（HL 不算），
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

    // 快慢线贴在一起时行情多半在震荡，形态信号在那里的胜率很差。用 ATR 归一化后的间距
    // MaDistance = |FastRma - SlowRma| / ATR 必须大于 X 系数才放行。
    // X = 0 表示这道闸门关闭；此时 ATR 缺失也不该把所有交易挡掉。
    private static bool IsMaDistanceSatisfied(PdhpdlSignalModel signalModel) {
        if (signalModel.MinMaDistance <= 0.0)
            return true;

        if (double.IsNaN(signalModel.MaDistance))
            return false;

        return signalModel.MaDistance > signalModel.MinMaDistance;
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
