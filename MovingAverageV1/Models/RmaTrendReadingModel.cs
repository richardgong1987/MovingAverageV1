using System;

namespace cAlgo.Robots;

// The three RMA values the direction filter compares, each off the last closed bar of its own
// timeframe: fast and slow share one bar (SourceBarTime), mid comes from its own. Carrying them
// together is what keeps a live value from being compared against a stale one.
public class RmaTrendReadingModel {
    public RmaTrendReadingModel(DateTime sourceBarTime, double fast, double slow, double mid) {
        SourceBarTime = sourceBarTime;
        Fast = fast;
        Slow = slow;
        Mid = mid;
    }

    public DateTime SourceBarTime { get; }

    public double Fast { get; }

    public double Slow { get; }

    public double Mid { get; }
}
