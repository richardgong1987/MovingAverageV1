using System;

namespace cAlgo.Robots;

public static class RmaUtils {
    // 三线排列。周期越短的线越贴近价格，所以按周期从短到长自上而下排开才算多头站住：
    // 紫 > 蓝 > 黄，也就是 45M RMA13 > 60M RMA13 > 60M RMA55。空头是完全对称的反向排列。
    // 相等不算排列成立：三条线叠在一起时行情没有方向。
    public static bool IsBullishStack(double midRma, double fastRma, double slowRma) {
        RequireFiniteValues(midRma, fastRma, slowRma);

        return midRma > fastRma && fastRma > slowRma;
    }

    public static bool IsBearishStack(double midRma, double fastRma, double slowRma) {
        RequireFiniteValues(midRma, fastRma, slowRma);

        return midRma < fastRma && fastRma < slowRma;
    }

    private static void RequireFiniteValues(double midRma, double fastRma, double slowRma) {
        if (!double.IsFinite(midRma) || !double.IsFinite(fastRma) || !double.IsFinite(slowRma))
            throw new ArgumentException("RMA values must be finite numbers.");
    }
}
