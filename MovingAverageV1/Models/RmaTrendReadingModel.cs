using System;

namespace cAlgo.Robots;

// The three RMA values the direction filter compares, each off the last closed bar of its own
// timeframe: blue and purple share one bar (SourceBarTime), yellow comes from its own. Carrying
// them together is what keeps a live value from being compared against a stale one.
public class RmaTrendReadingModel {
    public RmaTrendReadingModel(DateTime sourceBarTime, double blue, double purple, double yellow) {
        SourceBarTime = sourceBarTime;
        Blue = blue;
        Purple = purple;
        Yellow = yellow;
    }

    // Open time of the blue/purple bar. Yellow's own bar is younger and is not tracked here.
    public DateTime SourceBarTime { get; }

    public double Blue { get; }

    public double Purple { get; }

    public double Yellow { get; }
}
