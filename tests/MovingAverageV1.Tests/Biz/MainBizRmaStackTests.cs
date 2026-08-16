using cAlgo.Robots;
using Xunit;

namespace MovingAverageV1.Tests.Biz {
    // 方向过滤：三线排列。多头要 紫 > 蓝 > 黄（45M RMA13 > 60M RMA13 > 60M RMA55），空头完全反过来。
    // 每个用例都用一根本来就成立的 pinbar 作基准，只改三条均线的值，确保挡掉信号的是排列这道闸门。
    public class MainBizRmaStackTests {
        [Fact]
        public void allows_long_when_lines_stack_bullish() {
            PdhpdlSignalModel signalModel = EvaluateLongPinbar(midRma: 9.5, fastRma: 9.0, slowRma: 8.0);

            Assert.True(signalModel.IsLongSignal);
        }

        // 紫线掉到蓝线下面：短周期先转弱，多头排列不成立。
        [Fact]
        public void blocks_long_when_mid_is_below_fast() {
            PdhpdlSignalModel signalModel = EvaluateLongPinbar(midRma: 8.5, fastRma: 9.0, slowRma: 8.0);

            Assert.False(signalModel.IsLongSignal);
        }

        [Fact]
        public void blocks_long_when_fast_is_below_slow() {
            PdhpdlSignalModel signalModel = EvaluateLongPinbar(midRma: 9.5, fastRma: 8.0, slowRma: 9.0);

            Assert.False(signalModel.IsLongSignal);
        }

        // 三条线叠在一起时行情没有方向，不算排列成立。
        [Fact]
        public void blocks_long_when_lines_are_equal() {
            PdhpdlSignalModel signalModel = EvaluateLongPinbar(midRma: 9.0, fastRma: 9.0, slowRma: 9.0);

            Assert.False(signalModel.IsLongSignal);
        }

        [Fact]
        public void allows_short_when_lines_stack_bearish() {
            PdhpdlSignalModel signalModel = EvaluateShortPinbar(midRma: 4.0, fastRma: 5.0, slowRma: 8.0);

            Assert.True(signalModel.IsShortSignal);
        }

        [Fact]
        public void blocks_short_when_mid_is_above_fast() {
            PdhpdlSignalModel signalModel = EvaluateShortPinbar(midRma: 6.0, fastRma: 5.0, slowRma: 8.0);

            Assert.False(signalModel.IsShortSignal);
        }

        [Fact]
        public void blocks_short_when_fast_is_above_slow() {
            PdhpdlSignalModel signalModel = EvaluateShortPinbar(midRma: 4.0, fastRma: 8.0, slowRma: 5.0);

            Assert.False(signalModel.IsShortSignal);
        }

        // 买入 pinbar：长下影、几乎没有上影，快线落在这根 K 线的高低点之间（信号长在快线上）。
        private static PdhpdlSignalModel EvaluateLongPinbar(double midRma, double fastRma, double slowRma) {
            CandleModel current = new(open: 9.0, high: 10.0, low: 0.0, close: 9.5);

            return Evaluate(current, midRma, fastRma, slowRma);
        }

        // 卖出 pinbar：上下影与买入 pinbar 对称。
        private static PdhpdlSignalModel EvaluateShortPinbar(double midRma, double fastRma, double slowRma) {
            CandleModel current = new(open: 1.0, high: 10.0, low: 0.0, close: 0.5);

            return Evaluate(current, midRma, fastRma, slowRma);
        }

        private static PdhpdlSignalModel Evaluate(CandleModel current, double midRma, double fastRma, double slowRma) {
            CandleModel previous = new(open: 5.0, high: 6.0, low: 4.0, close: 5.0);
            CandleModel earlier = new(open: 5.0, high: 6.0, low: 4.0, close: 5.0);

            PdhpdlSignalModel signalModel = new() {
                HasData = true,
                Open = current.Open,
                High = current.High,
                Low = current.Low,
                Close = current.Close,
                HasRmaData = true,
                MidRma = midRma,
                FastRma = fastRma,
                SlowRma = slowRma,
                MaDistance = double.NaN,
                MinMaDistance = 0.0
            };

            MainBiz.Evaluate(signalModel, current, previous, earlier, new ConsecutiveEntryGate());
            return signalModel;
        }
    }
}
