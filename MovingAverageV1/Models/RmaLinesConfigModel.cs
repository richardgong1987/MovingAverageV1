namespace cAlgo.Robots;

// Tunables for the RMA overlay (see LineDrawer/RmaLines).
// Pure data: no cAlgo.API references so it stays trivially inspectable.
// Defaults mirror the source Pine indicator: two Welles-Wilder (RMA) moving
// averages of period 13 and 55 computed on the 120-minute timeframe.
public class RmaLinesConfigModel {
    public MovingAverageSourceModel Source { get; set; } = MovingAverageSourceModel.HigherTimeFrame;

    public int FastPeriod { get; set; } = 13;

    public int SlowPeriod { get; set; } = 55;

    public int HigherTimeFrameMinutes { get; set; } = 60;

    public int MidPeriod { get; set; } = 13;

    // The mid line reads a shorter timeframe than the fast/slow pair on purpose: it is the first
    // of the three to turn, which is what makes the stacking rule mean something. Fixed here
    // rather than exposed as a cBot parameter — only the period is tunable.
    public int MidTimeFrameMinutes { get; set; } = 45;

    public int Thickness { get; set; } = 3;
}
