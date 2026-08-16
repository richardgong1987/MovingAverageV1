using System;

namespace cAlgo.Robots;

public static class RmaUtils {
    // 三线排列。周期越短的线越贴近价格，所以按周期从短到长自上而下排开才算多头站住：
    // 黄 > 蓝 > 紫，也就是 45M RMA13 > 60M RMA13 > 60M RMA55。空头是完全对称的反向排列。
    // 相等不算排列成立：三条线叠在一起时行情没有方向。
    public static bool IsBullishStack(double yellowRma, double blueRma, double purpleRma) {
        RequireFiniteValues(yellowRma, blueRma, purpleRma);

        return yellowRma > blueRma && blueRma > purpleRma;
    }

    public static bool IsBearishStack(double yellowRma, double blueRma, double purpleRma) {
        RequireFiniteValues(yellowRma, blueRma, purpleRma);

        return yellowRma < blueRma && blueRma < purpleRma;
    }

    private static void RequireFiniteValues(double yellowRma, double blueRma, double purpleRma) {
        if (!double.IsFinite(yellowRma) || !double.IsFinite(blueRma) || !double.IsFinite(purpleRma))
            throw new ArgumentException("RMA values must be finite numbers.");
    }
}
