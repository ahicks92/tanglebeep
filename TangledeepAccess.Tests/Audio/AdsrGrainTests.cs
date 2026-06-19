using TangledeepAccess.Audio;
using Xunit;

namespace TangledeepAccess.Tests.Audio {
    public class AdsrGrainTests {
        // A unit-amplitude grain so EnvelopeGain shows through Evaluate unchanged.
        private sealed class DcGrain : Grain {
            public override double Duration => 1.0;
            public override float Evaluate(double t) => (t >= 0.0 && t < 1.0) ? 1f : 0f;
        }

        [Fact]
        public void DurationIsLesserOfInnerAndEnvelope() {
            // Envelope 0.1 s < inner 1.0 s -> 0.1 (the 100 ms enveloped tone case).
            var shortEnv = new AdsrGrain(new SineGrain(440), 0.01, 0.02, 0.04, 0.03);
            Assert.Equal(0.1, shortEnv.Duration, 9);

            // Envelope 2.0 s > inner 1.0 s -> inner wins.
            var longEnv = new AdsrGrain(new SineGrain(440), 0.5, 0.5, 0.5, 0.5);
            Assert.Equal(1.0, longEnv.Duration, 9);
        }

        [Fact]
        public void EnvelopeHitsSegmentBoundaries() {
            // attack .1, decay .1, sustain .2, release .1; sustain level 0.5.
            var env = new AdsrGrain(new DcGrain(), 0.1, 0.1, 0.2, 0.1, sustainLevel: 0.5);

            Assert.Equal(0.0, env.EnvelopeGain(0.0), 6); // start of attack
            Assert.Equal(1.0, env.EnvelopeGain(0.1), 6); // peak (end of attack)
            Assert.Equal(0.5, env.EnvelopeGain(0.2), 6); // end of decay = sustain level
            Assert.Equal(0.5, env.EnvelopeGain(0.35), 6); // mid sustain
            Assert.Equal(0.0, env.EnvelopeGain(0.5), 6); // end of release
        }

        [Fact]
        public void EnvelopeRampsAreLinear() {
            var env = new AdsrGrain(new DcGrain(), 0.1, 0.1, 0.2, 0.1, sustainLevel: 0.5);
            Assert.Equal(0.5, env.EnvelopeGain(0.05), 6); // halfway up attack
            Assert.Equal(0.75, env.EnvelopeGain(0.15), 6); // halfway down decay (1 -> 0.5)
            Assert.Equal(0.25, env.EnvelopeGain(0.45), 6); // halfway down release (0.5 -> 0)
        }

        [Fact]
        public void EvaluateIsEnvelopeTimesInner() {
            var env = new AdsrGrain(new DcGrain(), 0.1, 0.1, 0.2, 0.1, sustainLevel: 0.5);
            Assert.Equal(0.5f, env.Evaluate(0.35), 5); // sustain * 1.0
            Assert.Equal(0f, env.Evaluate(0.0), 5);
        }

        [Fact]
        public void ZeroLengthSegmentsDoNotDivideByZero() {
            // No attack/decay: jumps straight to sustain, then releases.
            var env = new AdsrGrain(new DcGrain(), 0.0, 0.0, 0.1, 0.1, sustainLevel: 0.8);
            Assert.Equal(0.8, env.EnvelopeGain(0.05), 6);
            Assert.Equal(0.4, env.EnvelopeGain(0.15), 6); // halfway down release
        }
    }
}
