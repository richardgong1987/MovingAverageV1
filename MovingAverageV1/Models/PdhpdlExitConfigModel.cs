namespace cAlgo.Robots;

// 两级止盈的可调项。与 PdhpdlRiskGuardConfigModel 同一套路：纯数据，把一组参数从组合根
// 带到用它的类，免得构造函数挂一长串 double。
public class PdhpdlExitConfigModel {
    // 第一目标：这么多个 R，或者中轨，谁先到算谁。到了平半仓。
    public double FirstTargetR { get; set; } = 2.0;

    // 第二目标：这么多个 R，或者对面轨道，谁先到算谁。到了平掉剩下的半仓。
    public double SecondTargetR { get; set; } = 4.0;

    // Once protection triggers, the stop moves this many ticks past the entry price, in the
    // profitable direction. Same tick unit as PdhpdlOrderPlanner's stop offset.
    public int BreakevenOffsetTicks { get; set; } = 50;
}
