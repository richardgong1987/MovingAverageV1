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

    public StrategyModel Strategy { get; set; }

    public bool IsBigK { get; set; }

    // MarketStructure 到这根 K 线为止累计标出的 LL / HH 次数，供 ConsecutiveEntryGate 比较。
    public int LowerLowCount { get; set; }

    public int HigherHighCount { get; set; }

    public bool HasRmaData { get; set; }

    public DateTime RmaSourceBarTime { get; set; }

    public double FastRma { get; set; }

    public double SlowRma { get; set; }

    // |FastRma - SlowRma| / ATR14，都取均线来源周期（默认 120m）上同一根已收 K 线。
    // 除以 ATR 是为了把间距换算成「几个 ATR」，阈值才能跨品种、跨波动率通用。ATR 缺失时为 NaN。
    public double MaDistance { get; set; }

    // X 系数：MaDistance 必须大于它才允许开仓。0 = 不启用。
    public double MinMaDistance { get; set; }
}
