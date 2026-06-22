using Tanglebeep.Audio;
using Xunit;

namespace Tanglebeep.Tests.Audio {
    public class BufferGrainTests {
        // data: [_, 10, 20, 30, _] with the slice covering indices 1..3 (count 3).
        private static BufferGrain Slice() {
            var data = new float[] { 99f, 10f, 20f, 30f, 99f };
            return new BufferGrain(data, offset: 1, count: 3, sampleRate: 1000);
        }

        [Fact]
        public void DurationIsCountOverSampleRate() {
            Assert.Equal(3.0 / 1000.0, Slice().Duration, 9);
        }

        [Fact]
        public void ReadsSliceSamplesAtTheirTimes() {
            var g = Slice();
            Assert.Equal(10f, g.Evaluate(0.0), 5);          // sample 0 of the slice
            Assert.Equal(20f, g.Evaluate(1.0 / 1000.0), 5); // sample 1
            Assert.Equal(30f, g.Evaluate(2.0 / 1000.0), 5); // sample 2
        }

        [Fact]
        public void InterpolatesBetweenSamples() {
            Assert.Equal(15f, Slice().Evaluate(0.5 / 1000.0), 5); // halfway between 10 and 20
        }

        [Fact]
        public void DoesNotBleedPastTheSlice() {
            // Near the last sample, interpolation must use only data[3]=30, never data[4]=99.
            var g = Slice();
            Assert.Equal(30f, g.Evaluate(2.5 / 1000.0), 5);
        }

        [Fact]
        public void SilentOutsideDuration() {
            var g = Slice();
            Assert.Equal(0f, g.Evaluate(-0.001));
            Assert.Equal(0f, g.Evaluate(3.0 / 1000.0));
        }
    }
}
