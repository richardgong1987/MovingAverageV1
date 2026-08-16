using System;

namespace cAlgo.Robots;

public class PdhpdlSignalModel {
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
    // ConsecutiveEntryGate 用前者判断方向、后者判断新旧。
    public MarketStructurePivotModel LatestPivot { get; set; }

    public int PivotCount { get; set; }

    public bool HasRmaData { get; set; }

    public DateTime RmaSourceBarTime { get; set; }

    // 蓝线 60m RMA13、紫线 60m RMA55，同一个周期上的同一根已收 K 线。
    public double BlueRma { get; set; }

    public double PurpleRma { get; set; }

    // 黄线 45m RMA13。它和蓝紫线不在同一个周期上，取的是自己周期上最后一根已收 K 线。
    public double YellowRma { get; set; }

    // |BlueRma - PurpleRma| / ATR14，都取蓝紫线所在周期（60m）上同一根已收 K 线。
    // 除以 ATR 是为了把间距换算成「几个 ATR」，阈值才能跨品种、跨波动率通用。ATR 缺失时为 NaN。
    public double MaDistance { get; set; }

    // X 系数：MaDistance 必须大于它才允许开仓。0 = 不启用。
    public double MinMaDistance { get; set; }
}
