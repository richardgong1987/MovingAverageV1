using cAlgo.Robots;
using Xunit;

namespace MovingAverageV1.Tests.Biz {
    // MaDistance 闸门：|BlueRma - PurpleRma| / ATR 必须大于 X 系数，否则不开仓。
    // 这里用一个本来就成立的买入 pinbar 作基准，只改 MaDistance / MinMaDistance，
    // 确保挡掉信号的确实是这道闸门而不是别的条件。
    public class MainBizMaDistanceTests {
        [Fact]
        public void allows_entry_when_gate_is_disabled() {
            PdhpdlSignalModel signalModel = EvaluateLongPinbar(maDistance: 0.2, minMaDistance: 0.0);

            Assert.True(signalModel.IsLongSignal);
        }

        [Fact]
        public void allows_entry_when_distance_exceeds_coefficient() {
            PdhpdlSignalModel signalModel = EvaluateLongPinbar(maDistance: 1.5, minMaDistance: 1.0);

            Assert.True(signalModel.IsLongSignal);
        }

        [Fact]
        public void blocks_entry_when_distance_is_below_coefficient() {
            PdhpdlSignalModel signalModel = EvaluateLongPinbar(maDistance: 0.9, minMaDistance: 1.0);

            Assert.False(signalModel.IsLongSignal);
        }

        [Fact]
        public void blocks_entry_when_distance_equals_coefficient() {
            PdhpdlSignalModel signalModel = EvaluateLongPinbar(maDistance: 1.0, minMaDistance: 1.0);

            Assert.False(signalModel.IsLongSignal);
        }

        // ATR 缺失时 MaDistance 是 NaN。闸门开着就必须挡掉——放行等于把这道风控静默关掉。
        [Fact]
        public void blocks_entry_when_distance_is_unavailable_and_gate_is_enabled() {
            PdhpdlSignalModel signalModel = EvaluateLongPinbar(maDistance: double.NaN, minMaDistance: 1.0);

            Assert.False(signalModel.IsLongSignal);
        }

        [Fact]
        public void allows_entry_when_distance_is_unavailable_and_gate_is_disabled() {
            PdhpdlSignalModel signalModel = EvaluateLongPinbar(maDistance: double.NaN, minMaDistance: 0.0);

            Assert.True(signalModel.IsLongSignal);
        }

        // 买入 pinbar：长下影、几乎没有上影，三线是多头排列（黄 > 蓝 > 紫），蓝线又落在这根 K 线的
        // 高低点之间（信号长在蓝线上）。
        private static PdhpdlSignalModel EvaluateLongPinbar(double maDistance, double minMaDistance) {
            CandleModel current = new(open: 9.0, high: 10.0, low: 0.0, close: 9.5);
            CandleModel previous = new(open: 5.0, high: 6.0, low: 4.0, close: 5.0);
            CandleModel earlier = new(open: 5.0, high: 6.0, low: 4.0, close: 5.0);

            PdhpdlSignalModel signalModel = new() {
                HasData = true,
                Open = current.Open,
                High = current.High,
                Low = current.Low,
                Close = current.Close,
                HasRmaData = true,
                YellowRma = 9.5,
                BlueRma = 9.0,
                PurpleRma = 8.0,
                MaDistance = maDistance,
                MinMaDistance = minMaDistance
            };

            MainBiz.Evaluate(signalModel, current, previous, earlier, new ConsecutiveEntryGate());
            return signalModel;
        }
    }
}
