namespace cAlgo.Robots;

// Tunables for the RMA overlay (see LineDrawer/RmaLines). The three lines are named after the
// colour they are drawn in, because that is how they are read on the chart and in every rule:
//   Blue   - 60m RMA13
//   Purple - 60m RMA55
//   Yellow - 45m RMA13
// Pure data: no cAlgo.API references so it stays trivially inspectable.
public class RmaLinesConfigModel {
    public int RMABluePeriod { get; set; } = 13;

    public int RMAPurplePeriod { get; set; } = 55;

    // Blue and purple share one timeframe: the GapExpansion gate measures the gap between them,
    // which only means something if both come off the same bar.
    public int BluePurpleTimeFrameMinutes { get; set; } = 60;

    public int RMAYellowPeriod { get; set; } = 13;

    // Yellow runs on a shorter timeframe on purpose: it is the first of the three to turn, which
    // is what makes the stacking rule mean something. Fixed here rather than exposed as a cBot
    // parameter — only the period is tunable.
    public int YellowTimeFrameMinutes { get; set; } = 45;

    public int Thickness { get; set; } = 3;
}
