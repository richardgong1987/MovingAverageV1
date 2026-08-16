using cAlgo.Robots;
using Xunit;

namespace MovingAverageV1.Tests.Biz {
    // 信号位闸门：形态 K 线要落在黄蓝带上（45M RMA13 ~ 60M RMA13）。碰到任一条线、跨过整条带、
    // 或者整根夹在两条线之间都算数；完全在带外才挡掉。
    // 每个用例都是一根本来就成立的买入 pinbar（三线也是多头排列），只挪这根 K 线的高低点。
    public class MainBizYellowBlueBandTests {
        [Fact]
        public void allows_when_candle_spans_the_whole_band() {
            CandleModel current = new(open: 9.0, high: 10.0, low: 0.0, close: 9.5);

            Assert.True(EvaluateLongPinbar(current).IsLongSignal);
        }

        // 整根都在蓝线上方，只够到黄线。旧规则只看蓝线，会把它挡掉。
        [Fact]
        public void allows_when_candle_touches_only_the_yellow_line() {
            CandleModel current = new(open: 11.5, high: 12.0, low: 9.2, close: 11.6);

            Assert.True(EvaluateLongPinbar(current).IsLongSignal);
        }

        // 整根夹在两条线之间，一条线都没碰到。旧规则同样会挡掉。
        [Fact]
        public void allows_when_candle_sits_entirely_inside_the_band() {
            CandleModel current = new(open: 11.2, high: 11.5, low: 9.5, close: 11.3);

            Assert.True(EvaluateLongPinbar(current, yellowRma: 12.0).IsLongSignal);
        }

        [Fact]
        public void blocks_when_candle_stays_below_the_band() {
            CandleModel current = new(open: 8.0, high: 8.8, low: 0.0, close: 8.5);

            Assert.False(EvaluateLongPinbar(current).IsLongSignal);
        }

        [Fact]
        public void blocks_when_candle_stays_above_the_band() {
            CandleModel current = new(open: 12.4, high: 13.0, low: 9.6, close: 12.5);

            Assert.False(EvaluateLongPinbar(current).IsLongSignal);
        }

        // 蓝 9.0、黄 9.5（或调用方指定），带就是 9.0 ~ 9.5；紫 8.0 让三线维持多头排列。
        private static PdhpdlSignalModel EvaluateLongPinbar(CandleModel current, double yellowRma = 9.5) {
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
                BlueRma = 9.0,
                PurpleRma = 8.0,
                MaDistance = double.NaN,
                MinMaDistance = 0.0
            };

            MainBiz.Evaluate(signalModel, current, previous, earlier, new ConsecutiveEntryGate());
            return signalModel;
        }
    }
}
