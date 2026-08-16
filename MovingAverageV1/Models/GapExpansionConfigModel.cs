namespace cAlgo.Robots;

// 开口扩大闸门（GapExpansion）的参数。蓝紫线拉开还不够，得在「继续拉开」才允许开仓。
// Pure data: no cAlgo.API references so it stays trivially inspectable.
public class GapExpansionConfigModel {
    // N：往前回看几根 15 分钟 K 线。4 = 1 小时前，8 = 2 小时前。
    public int LookbackBars { get; set; } = 4;

    // N 的单位。蓝紫线跑在 60 分钟上，这个数只是把 N 换算成分钟，本身不是均线周期。
    public int LookbackBarMinutes { get; set; } = 15;

    // X：开口的扩大幅度用 60 分钟 ATR14 归一化后，至少要达到这个倍数。V1 基准值 0.10。
    // 0 = 关闭这道闸门。
    public double MinExpansionAtr { get; set; } = 0.10;

    public int LookbackMinutes => LookbackBars * LookbackBarMinutes;
}
