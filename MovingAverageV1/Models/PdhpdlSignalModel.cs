using System;

namespace cAlgo.Robots;

public class PdhpdlSignalModel {
    public bool IsUseWickRatio { get; set; }
    public bool HasData { get; set; }

    public DateTime BarTime { get; set; }

    public int BarIndex { get; set; }

    public double High { get; set; }

    public double Low { get; set; }

    public double Close { get; set; }

    public double Open { get; set; }

    public bool IsLongSignal { get; set; }

    public bool IsShortSignal { get; set; }

    public string Label { get; set; }

    public string KeyLevel { get; set; }

    public double SL { get; set; }

    public bool IsBigK { get; set; }

    // MarketStructure 到这根 K 线为止最后标出的结构点，及它的编号（第几个）。
    // PivotEntryGate 用前者判断方向、后者判断新旧。
    public MarketStructurePivotModel LatestPivot { get; set; }

    public int PivotCount { get; set; }

    public bool HasRmaData { get; set; }

    public DateTime RmaSourceBarTime { get; set; }

    // 蓝线 60m RMA13、紫线 60m RMA55，同一个周期上的同一根已收 K 线。
    public double BlueRma { get; set; }

    public double PurpleRma { get; set; }

    // 黄线 45m RMA13。它和蓝紫线不在同一个周期上，取的是自己周期上最后一根已收 K 线。
    public double YellowRma { get; set; }

    // 蓝紫开口的扩大幅度，多头视角：((BlueRma - PurpleRma)现在 - (BlueRma - PurpleRma)N根15M K线前) / 60m ATR14。
    // 正数 = 蓝线正在往紫线上方拉开（多头在加速），负数 = 反过来（空头在加速）。
    // 除以 ATR 是为了把增量换算成「几个 ATR」，阈值才能跨品种、跨波动率通用。ATR 缺失或历史不够时为 NaN。
    public double GapExpansion { get; set; }

    // X 系数：GapExpansion 要达到这个 ATR 倍数才允许开仓（空头看的是取负之后的值）。0 = 不启用。
    public double MinGapExpansion { get; set; }
}
