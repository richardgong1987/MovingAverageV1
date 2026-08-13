using cAlgo.Robots;
using Xunit;

namespace MovingAverageV1.Tests.Biz {
    public class ConsecutiveEntryGateTests {
        private const PdhpdlTradeDirectionModel Short = PdhpdlTradeDirectionModel.Short;
        private const PdhpdlTradeDirectionModel Long = PdhpdlTradeDirectionModel.Long;

        [Fact]
        public void first_entry_in_a_direction_is_always_allowed() {
            var gate = new ConsecutiveEntryGate();

            Assert.True(gate.IsAllowed(Short, lowerLowCount: 0, higherHighCount: 0));
            Assert.True(gate.IsAllowed(Long, lowerLowCount: 0, higherHighCount: 0));
        }

        [Fact]
        public void second_consecutive_short_is_blocked_without_a_new_lower_low() {
            var gate = new ConsecutiveEntryGate();
            gate.RecordEntry(Short, lowerLowCount: 3, higherHighCount: 1);

            Assert.False(gate.IsAllowed(Short, lowerLowCount: 3, higherHighCount: 1));
        }

        [Fact]
        public void second_consecutive_short_is_allowed_once_a_new_lower_low_prints() {
            var gate = new ConsecutiveEntryGate();
            gate.RecordEntry(Short, lowerLowCount: 3, higherHighCount: 1);

            Assert.True(gate.IsAllowed(Short, lowerLowCount: 4, higherHighCount: 1));
        }

        [Fact]
        public void each_further_short_needs_its_own_new_lower_low() {
            var gate = new ConsecutiveEntryGate();
            gate.RecordEntry(Short, lowerLowCount: 3, higherHighCount: 0);

            // The 4th LL lets the second short through, and is then used up.
            Assert.True(gate.IsAllowed(Short, lowerLowCount: 4, higherHighCount: 0));
            gate.RecordEntry(Short, lowerLowCount: 4, higherHighCount: 0);
            Assert.False(gate.IsAllowed(Short, lowerLowCount: 4, higherHighCount: 0));

            // A 5th LL is needed for the third short.
            Assert.True(gate.IsAllowed(Short, lowerLowCount: 5, higherHighCount: 0));
        }

        [Fact]
        public void a_higher_high_does_not_unlock_a_short() {
            var gate = new ConsecutiveEntryGate();
            gate.RecordEntry(Short, lowerLowCount: 2, higherHighCount: 2);

            Assert.False(gate.IsAllowed(Short, lowerLowCount: 2, higherHighCount: 9));
        }

        [Fact]
        public void switching_direction_starts_over_without_needing_a_structure_point() {
            var gate = new ConsecutiveEntryGate();
            gate.RecordEntry(Short, lowerLowCount: 3, higherHighCount: 1);

            // Long is a fresh direction, so it is allowed even though no HH has printed since.
            Assert.True(gate.IsAllowed(Long, lowerLowCount: 3, higherHighCount: 1));
            gate.RecordEntry(Long, lowerLowCount: 3, higherHighCount: 1);

            // ...and the short streak is gone: the next short is a first again.
            Assert.True(gate.IsAllowed(Short, lowerLowCount: 3, higherHighCount: 1));
        }

        [Fact]
        public void second_consecutive_long_needs_a_new_higher_high() {
            var gate = new ConsecutiveEntryGate();
            gate.RecordEntry(Long, lowerLowCount: 5, higherHighCount: 2);

            Assert.False(gate.IsAllowed(Long, lowerLowCount: 9, higherHighCount: 2));
            Assert.True(gate.IsAllowed(Long, lowerLowCount: 5, higherHighCount: 3));
        }

        [Fact]
        public void consecutive_count_tracks_the_streak_and_resets_on_direction_change() {
            var gate = new ConsecutiveEntryGate();

            gate.RecordEntry(Short, lowerLowCount: 1, higherHighCount: 0);
            Assert.Equal(1, gate.ConsecutiveCount);

            gate.RecordEntry(Short, lowerLowCount: 2, higherHighCount: 0);
            Assert.Equal(2, gate.ConsecutiveCount);

            gate.RecordEntry(Long, lowerLowCount: 2, higherHighCount: 0);
            Assert.Equal(1, gate.ConsecutiveCount);
        }
    }
}
