using cAlgo.Robots;
using Xunit;

namespace MovingAverageV1.Tests.Biz {
    // 方向过滤：三线排列。多头要 黄 > 蓝 > 紫（45M RMA13 > 60M RMA13 > 60M RMA55），空头完全反过来。
    // 每个用例都用一根本来就成立的 pinbar 作基准，只改三条均线的值，确保挡掉信号的是排列这道闸门。
    public class MainBizRmaStackTests {
        [Fact]
        public void allows_long_when_lines_stack_bullish() {
            PdhpdlSignalModel signalModel = EvaluateLongPinbar(yellowRma: 9.5, blueRma: 9.0, purpleRma: 8.0);

            Assert.True(signalModel.IsLongSignal);
        }

        // 黄线掉到蓝线下面：短周期先转弱，多头排列不成立。
        [Fact]
        public void blocks_long_when_yellow_is_below_blue() {
            PdhpdlSignalModel signalModel = EvaluateLongPinbar(yellowRma: 8.5, blueRma: 9.0, purpleRma: 8.0);

            Assert.False(signalModel.IsLongSignal);
        }

        [Fact]
        public void blocks_long_when_blue_is_below_purple() {
            PdhpdlSignalModel signalModel = EvaluateLongPinbar(yellowRma: 9.5, blueRma: 8.0, purpleRma: 9.0);

            Assert.False(signalModel.IsLongSignal);
        }

        // 三条线叠在一起时行情没有方向，不算排列成立。
        [Fact]
        public void blocks_long_when_lines_are_equal() {
            PdhpdlSignalModel signalModel = EvaluateLongPinbar(yellowRma: 9.0, blueRma: 9.0, purpleRma: 9.0);

            Assert.False(signalModel.IsLongSignal);
        }

        [Fact]
        public void allows_short_when_lines_stack_bearish() {
            PdhpdlSignalModel signalModel = EvaluateShortPinbar(yellowRma: 4.0, blueRma: 5.0, purpleRma: 8.0);

            Assert.True(signalModel.IsShortSignal);
        }

        [Fact]
        public void blocks_short_when_yellow_is_above_blue() {
            PdhpdlSignalModel signalModel = EvaluateShortPinbar(yellowRma: 6.0, blueRma: 5.0, purpleRma: 8.0);

            Assert.False(signalModel.IsShortSignal);
        }

        [Fact]
        public void blocks_short_when_blue_is_above_purple() {
            PdhpdlSignalModel signalModel = EvaluateShortPinbar(yellowRma: 4.0, blueRma: 8.0, purpleRma: 5.0);

            Assert.False(signalModel.IsShortSignal);
        }

        // 买入 pinbar：长下影、几乎没有上影，蓝线落在这根 K 线的高低点之间（信号长在蓝线上）。
        private static PdhpdlSignalModel EvaluateLongPinbar(double yellowRma, double blueRma, double purpleRma) {
            CandleModel current = new(open: 9.0, high: 10.0, low: 0.0, close: 9.5);

            return Evaluate(current, yellowRma, blueRma, purpleRma, MarketStructurePivotModel.HigherHigh);
        }

        // 卖出 pinbar：上下影与买入 pinbar 对称。
        private static PdhpdlSignalModel EvaluateShortPinbar(double yellowRma, double blueRma, double purpleRma) {
            CandleModel current = new(open: 1.0, high: 10.0, low: 0.0, close: 0.5);

            return Evaluate(current, yellowRma, blueRma, purpleRma, MarketStructurePivotModel.LowerLow);
        }

        private static PdhpdlSignalModel Evaluate(CandleModel current, double yellowRma, double blueRma, double purpleRma,
            MarketStructurePivotModel latestPivot) {
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
                GapExpansion = double.NaN,
                MinGapExpansion = 0.0,
                LatestPivot = latestPivot,
                PivotCount = 1
            };

            MainBiz.Evaluate(signalModel, current, previous, earlier, new PivotEntryGate());
            return signalModel;
        }
    }
}
