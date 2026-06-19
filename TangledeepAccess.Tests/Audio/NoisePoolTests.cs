using System;
using TangledeepAccess.Audio;
using Xunit;

namespace TangledeepAccess.Tests.Audio {
    public class NoisePoolTests {
        private const int Sr = 48000;

        // length 10000 samples ≈ 0.21 s; tones of 0.1 s (4800 samples) take two-ish slices per fill.
        private static NoisePool Pool() {
            return new NoisePool(1000.0, 15.0, Sr, lengthSamples: 10000, warmupSamples: 128, targetRms: 0.3, seed: 1);
        }

        [Fact]
        public void TakeReturnsRequestedLengthAtPoolRate() {
            BufferGrain g = Pool().Take(0.1);
            Assert.Equal((int)Math.Ceiling(0.1 * Sr), g.Count);
            Assert.Equal(Sr, g.SampleRate);
        }

        [Fact]
        public void SuccessiveTakesAdvanceTheCursor() {
            var pool = Pool();
            BufferGrain a = pool.Take(0.05);
            BufferGrain b = pool.Take(0.05);
            Assert.Same(a.Data, b.Data);
            Assert.Equal(a.Offset + a.Count, b.Offset); // contiguous, non-overlapping
        }

        [Fact]
        public void RefillsWhenExhausted() {
            var pool = Pool();
            BufferGrain first = pool.Take(0.1); // offset 0
            pool.Take(0.1);                     // fills most of the ~0.21 s pool
            BufferGrain afterRefill = pool.Take(0.1); // not enough left -> regenerate

            Assert.Equal(0, afterRefill.Offset);     // cursor reset by the refill
            Assert.NotSame(first.Data, afterRefill.Data); // a fresh array
        }

        [Fact]
        public void IsNormalizedTowardTargetRms() {
            BufferGrain g = Pool().Take(0.1);
            double sum = 0.0;
            for (int i = 0; i < g.Count; i++) {
                float s = g.Data[g.Offset + i];
                sum += (double)s * s;
            }
            double rms = Math.Sqrt(sum / g.Count);
            Assert.InRange(rms, 0.2, 0.4); // ~0.3, allowing for slice-vs-pool variation
        }
    }
}
