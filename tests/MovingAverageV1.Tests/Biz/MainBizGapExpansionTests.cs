using cAlgo.Robots;
using Xunit;

namespace MovingAverageV1.Tests.Biz {
    // 开口扩大闸门：(现在的蓝紫开口 - N 根 15M K 线之前的开口) / 60m ATR14 ≥ X 才放行。
    // GapExpansion 存的是多头视角的带符号值，空头看的是它取负之后的数。
    // 这里用一根本来就成立的 pinbar 作基准，只改 GapExpansion / MinGapExpansion，
    // 确保挡掉信号的确实是这道闸门而不是别的条件。
    public class MainBizGapExpansionTests {
        [Fact]
        public void allows_long_when_gate_is_disabled() {
            PdhpdlSignalModel signalModel = EvaluateLongPinbar(gapExpansion: 0.01, minGapExpansion: 0.0);

            Assert.True(signalModel.IsLongSignal);
        }

        [Fact]
        public void allows_long_when_expansion_exceeds_coefficient() {
            PdhpdlSignalModel signalModel = EvaluateLongPinbar(gapExpansion: 0.15, minGapExpansion: 0.10);

            Assert.True(signalModel.IsLongSignal);
        }

        // 门槛是「≥ X」，正好等于 X 也放行。
        [Fact]
        public void allows_long_when_expansion_equals_coefficient() {
            PdhpdlSignalModel signalModel = EvaluateLongPinbar(gapExpansion: 0.10, minGapExpansion: 0.10);

            Assert.True(signalModel.IsLongSignal);
        }

        [Fact]
        public void blocks_long_when_expansion_is_below_coefficient() {
            PdhpdlSignalModel signalModel = EvaluateLongPinbar(gapExpansion: 0.09, minGapExpansion: 0.10);

            Assert.False(signalModel.IsLongSignal);
        }

        // 开口在收窄：三线排列还是多头，但蓝紫线正在靠拢，不许开多。
        [Fact]
        public void blocks_long_when_gap_is_shrinking() {
            PdhpdlSignalModel signalModel = EvaluateLongPinbar(gapExpansion: -0.30, minGapExpansion: 0.10);

            Assert.False(signalModel.IsLongSignal);
        }

        // ATR 缺失或历史不够时 GapExpansion 是 NaN。闸门开着就必须挡掉——放行等于把这道风控静默关掉。
        [Fact]
        public void blocks_long_when_expansion_is_unavailable_and_gate_is_enabled() {
            PdhpdlSignalModel signalModel = EvaluateLongPinbar(gapExpansion: double.NaN, minGapExpansion: 0.10);

            Assert.False(signalModel.IsLongSignal);
        }

        [Fact]
        public void allows_long_when_expansion_is_unavailable_and_gate_is_disabled() {
            PdhpdlSignalModel signalModel = EvaluateLongPinbar(gapExpansion: double.NaN, minGapExpansion: 0.0);

            Assert.True(signalModel.IsLongSignal);
        }

        // 空头的开口是 紫 - 蓝，所以 GapExpansion 要够负才算在扩大。
        [Fact]
        public void allows_short_when_gap_expands_downwards() {
            PdhpdlSignalModel signalModel = EvaluateShortPinbar(gapExpansion: -0.15, minGapExpansion: 0.10);

            Assert.True(signalModel.IsShortSignal);
        }

        [Fact]
        public void blocks_short_when_expansion_is_below_coefficient() {
            PdhpdlSignalModel signalModel = EvaluateShortPinbar(gapExpansion: -0.09, minGapExpansion: 0.10);

            Assert.False(signalModel.IsShortSignal);
        }

        // 多头方向的开口在扩大，对作空来说就是反向的，必须挡掉。
        [Fact]
        public void blocks_short_when_gap_expands_upwards() {
            PdhpdlSignalModel signalModel = EvaluateShortPinbar(gapExpansion: 0.30, minGapExpansion: 0.10);

            Assert.False(signalModel.IsShortSignal);
        }

        // 买入 pinbar：长下影、几乎没有上影，三线是多头排列（黄 > 蓝 > 紫），
        // K 线又和黄蓝带重叠（信号长在带上）。
        private static PdhpdlSignalModel EvaluateLongPinbar(double gapExpansion, double minGapExpansion) {
            CandleModel current = new(open: 9.0, high: 10.0, low: 0.0, close: 9.5);

            return Evaluate(current, yellowRma: 9.5, blueRma: 9.0, purpleRma: 8.0, gapExpansion, minGapExpansion);
        }

        // 卖出 pinbar：上下影与买入 pinbar 对称，三线是空头排列（黄 < 蓝 < 紫）。
        private static PdhpdlSignalModel EvaluateShortPinbar(double gapExpansion, double minGapExpansion) {
            CandleModel current = new(open: 1.0, high: 10.0, low: 0.0, close: 0.5);

            return Evaluate(current, yellowRma: 4.0, blueRma: 5.0, purpleRma: 8.0, gapExpansion, minGapExpansion);
        }

        private static PdhpdlSignalModel Evaluate(CandleModel current, double yellowRma, double blueRma, double purpleRma,
            double gapExpansion, double minGapExpansion) {
            CandleModel previous = new(open: 5.0, high: 6.0, low: 4.0, close: 5.0);
            CandleModel earlier = new(open: 5.0, high: 6.0, low: 4.0, close: 5.0);

            PdhpdlSignalModel signalModel = new() {
                HasData = true,
                Open = current.Open,
                High = current.High,
                Low = current.Low,
                Close = current.Close,
                HasRmaData = true,
                YellowRma = yellowRma,
                BlueRma = blueRma,
                PurpleRma = purpleRma,
                GapExpansion = gapExpansion,
                MinGapExpansion = minGapExpansion
            };

            MainBiz.Evaluate(signalModel, current, previous, earlier, new ConsecutiveEntryGate());
            return signalModel;
        }
    }
}
