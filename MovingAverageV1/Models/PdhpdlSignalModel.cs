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


    public bool TouchesUpperBandCurrent { get; set; }
    public bool TouchesUpperBandPrevious { get; set; }
    public bool TouchesUpperBandEarlier { get; set; }

    public bool TouchesLowerBandCurrent { get; set; }
    public bool TouchesLowerBandPrevious { get; set; }
    public bool TouchesLowerBandEarlier { get; set; }
    public bool TouchesUpperBand { get; set; }

    public bool TouchesLowerBand { get; set; }

    // 信号 K 线上的布林中轨。样本不足时是 NaN。
    public double MiddleBand { get; set; }

    public ManuallyDragonBand manuallyDragonBand { get; set; }

    public bool HasRmaData { get; set; }

    public DateTime RmaSourceBarTime { get; set; }

    public double FastRma { get; set; }

    public double SlowRma { get; set; }

}
