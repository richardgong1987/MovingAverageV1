using cAlgo.Robots;
using Xunit;

namespace MovingAverageV1.Tests.Biz {
    public class ConsecutiveEntryGateTests {
        private const PdhpdlTradeDirectionModel Short = PdhpdlTradeDirectionModel.Short;
        private const PdhpdlTradeDirectionModel Long = PdhpdlTradeDirectionModel.Long;

        private const MarketStructurePivotModel LowerLow = MarketStructurePivotModel.LowerLow;
        private const MarketStructurePivotModel LowerHigh = MarketStructurePivotModel.LowerHigh;
        private const MarketStructurePivotModel HigherHigh = MarketStructurePivotModel.HigherHigh;
        private const MarketStructurePivotModel HigherLow = MarketStructurePivotModel.HigherLow;
        private const MarketStructurePivotModel NoPivot = MarketStructurePivotModel.None;

        [Fact]
        public void first_entry_in_a_direction_is_always_allowed() {
            var gate = new ConsecutiveEntryGate();

            // 换方向后的第一笔既不看颜色也不看新旧，连一个结构点都还没出也放行。
            Assert.True(gate.IsAllowed(Short, NoPivot, pivotCount: 0));
            Assert.True(gate.IsAllowed(Long, NoPivot, pivotCount: 0));
            Assert.True(gate.IsAllowed(Short, HigherHigh, pivotCount: 7));
        }

        [Theory]
        [InlineData(LowerLow)]
        [InlineData(LowerHigh)]
        public void consecutive_short_is_allowed_on_a_new_red_pivot(MarketStructurePivotModel latestPivot) {
            var gate = new ConsecutiveEntryGate();
            gate.RecordEntry(Short, pivotCount: 3);

            Assert.True(gate.IsAllowed(Short, latestPivot, pivotCount: 4));
        }

        [Theory]
        [InlineData(HigherHigh)]
        [InlineData(HigherLow)]
        [InlineData(NoPivot)]
        public void consecutive_short_is_blocked_on_a_green_pivot(MarketStructurePivotModel latestPivot) {
            var gate = new ConsecutiveEntryGate();
            gate.RecordEntry(Short, pivotCount: 3);

            Assert.False(gate.IsAllowed(Short, latestPivot, pivotCount: 4));
        }

        [Theory]
        [InlineData(HigherHigh)]
        [InlineData(HigherLow)]
        public void consecutive_long_is_allowed_on_a_new_green_pivot(MarketStructurePivotModel latestPivot) {
            var gate = new ConsecutiveEntryGate();
            gate.RecordEntry(Long, pivotCount: 3);

            Assert.True(gate.IsAllowed(Long, latestPivot, pivotCount: 4));
        }

        [Theory]
        [InlineData(LowerLow)]
        [InlineData(LowerHigh)]
        [InlineData(NoPivot)]
        public void consecutive_long_is_blocked_on_a_red_pivot(MarketStructurePivotModel latestPivot) {
            var gate = new ConsecutiveEntryGate();
            gate.RecordEntry(Long, pivotCount: 3);

            Assert.False(gate.IsAllowed(Long, latestPivot, pivotCount: 4));
        }

        // 令牌语义：颜色对了也不够，结构点没更新就不能再用。
        [Fact]
        public void a_red_pivot_is_spent_by_the_short_it_lets_through() {
            var gate = new ConsecutiveEntryGate();
            gate.RecordEntry(Short, pivotCount: 3);

            // 第 4 个结构点是新的红标记，放行第 2 笔作空，然后被这笔用掉。
            Assert.True(gate.IsAllowed(Short, LowerLow, pivotCount: 4));
            gate.RecordEntry(Short, pivotCount: 4);
            Assert.False(gate.IsAllowed(Short, LowerLow, pivotCount: 4));

            // 第 3 笔要等第 5 个结构点。
            Assert.True(gate.IsAllowed(Short, LowerHigh, pivotCount: 5));
        }

        [Fact]
        public void a_green_pivot_is_spent_by_the_long_it_lets_through() {
            var gate = new ConsecutiveEntryGate();
            gate.RecordEntry(Long, pivotCount: 3);

            Assert.True(gate.IsAllowed(Long, HigherHigh, pivotCount: 4));
            gate.RecordEntry(Long, pivotCount: 4);
            Assert.False(gate.IsAllowed(Long, HigherHigh, pivotCount: 4));

            Assert.True(gate.IsAllowed(Long, HigherLow, pivotCount: 5));
        }

        // 中间夹了几个反向结构点也没关系：只要最后那个是自己的颜色，而且比上一笔入场时新。
        [Fact]
        public void an_opposite_pivot_in_between_does_not_block_the_next_aligned_one() {
            var gate = new ConsecutiveEntryGate();
            gate.RecordEntry(Short, pivotCount: 3);

            Assert.False(gate.IsAllowed(Short, HigherHigh, pivotCount: 4));
            Assert.True(gate.IsAllowed(Short, LowerLow, pivotCount: 5));
        }

        [Fact]
        public void switching_direction_starts_over_regardless_of_the_pivot() {
            var gate = new ConsecutiveEntryGate();
            gate.RecordEntry(Short, pivotCount: 3);

            // 作多是新方向，结构点既没更新、颜色也是红的，照样放行。
            Assert.True(gate.IsAllowed(Long, LowerLow, pivotCount: 3));
            gate.RecordEntry(Long, pivotCount: 3);

            // ……作空那一串就断了，下一笔作空又算第一笔。
            Assert.True(gate.IsAllowed(Short, HigherHigh, pivotCount: 3));
        }

        [Fact]
        public void consecutive_count_tracks_the_streak_and_resets_on_direction_change() {
            var gate = new ConsecutiveEntryGate();

            gate.RecordEntry(Short, pivotCount: 1);
            Assert.Equal(1, gate.ConsecutiveCount);

            gate.RecordEntry(Short, pivotCount: 2);
            Assert.Equal(2, gate.ConsecutiveCount);

            gate.RecordEntry(Long, pivotCount: 2);
            Assert.Equal(1, gate.ConsecutiveCount);
        }
    }
}
