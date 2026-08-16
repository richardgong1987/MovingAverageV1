namespace cAlgo.Robots;

public class Utils {
    public static bool AnyBarIsLong(params CandleModel[] candles) {
        foreach (CandleModel candle in candles) {
            if (!candle.IsBullish) {
                return false;
            }
        }

        return true;
    }

    public static bool AnyBarIsShort(params CandleModel[] candles) {
        foreach (CandleModel candle in candles) {
            if (!candle.IsBearish) {
                return false;
            }
        }

        return true;
    }
}
