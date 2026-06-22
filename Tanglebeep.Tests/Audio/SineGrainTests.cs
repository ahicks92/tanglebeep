using System;
using Tanglebeep.Audio;
using Xunit;

namespace Tanglebeep.Tests.Audio {
    public class SineGrainTests {
        [Fact]
        public void DurationIsOneSecond() {
            Assert.Equal(1.0, new SineGrain(440).Duration);
        }

        [Fact]
        public void StartsAtZeroCrossing() {
            Assert.Equal(0f, new SineGrain(440).Evaluate(0.0), 5);
        }

        [Fact]
        public void QuarterPeriodIsPeak() {
            // sin(2π·f·t) = 1 at t = 1/(4f).
            var g = new SineGrain(2.0, amplitude: 0.5);
            Assert.Equal(0.5f, g.Evaluate(1.0 / 8.0), 5);
        }

        [Fact]
        public void IsPeriodicAtFrequency() {
            var g = new SineGrain(5.0);
            double t = 0.03;
            Assert.Equal(g.Evaluate(t), g.Evaluate(t + 1.0 / 5.0), 5);
        }

        [Fact]
        public void SilentOutsideDuration() {
            var g = new SineGrain(440);
            Assert.Equal(0f, g.Evaluate(-0.01));
            Assert.Equal(0f, g.Evaluate(1.0));
            Assert.Equal(0f, g.Evaluate(2.0));
        }
    }
}
