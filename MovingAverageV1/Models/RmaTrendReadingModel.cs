using System;

namespace cAlgo.Robots;

// The fast and slow RMA as read off one closed bar of their shared timeframe, plus which bar
// that was. Carrying them together is what keeps the two values from drifting onto different
// bars once they are passed around.
public class RmaTrendReadingModel {
    public RmaTrendReadingModel(DateTime sourceBarTime, double fast, double slow) {
        SourceBarTime = sourceBarTime;
        Fast = fast;
        Slow = slow;
    }

    public DateTime SourceBarTime { get; }

    public double Fast { get; }

    public double Slow { get; }
}
