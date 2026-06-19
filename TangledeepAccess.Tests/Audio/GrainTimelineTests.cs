using TangledeepAccess.Audio;
using Xunit;

namespace TangledeepAccess.Tests.Audio {
    public class GrainTimelineTests {
        [Fact]
        public void EmptyTimelineIsZeroLength() {
            var t = new GrainTimeline();
            Assert.Equal(0.0, t.Duration);
            Assert.Empty(t.RenderStereo(48000));
        }

        [Fact]
        public void DurationAccountsForStartAndRate() {
            // 1 s sine at start 0.5, rate 2.0 -> occupies 0.5 .. 0.5 + 0.5 = 1.0.
            var t = new GrainTimeline().Add(new SineGrain(440), 0.5, 2.0, 0.0);
            Assert.Equal(1.0, t.Duration, 9);
        }

        [Fact]
        public void DurationIsLatestEnd() {
            var t = new GrainTimeline()
                .Add(new SineGrain(440), 0.0, 1.0, 0.0)   // ends at 1.0
                .Add(new SineGrain(440), 0.5, 1.0, 0.0);  // ends at 1.5
            Assert.Equal(1.5, t.Duration, 9);
        }

        [Fact]
        public void RenderLengthIsInterleavedStereo() {
            var t = new GrainTimeline().Add(new SineGrain(440), 0.0, 1.0, 0.0);
            float[] pcm = t.RenderStereo(48000);
            Assert.Equal(48000 * 2, pcm.Length);
        }

        [Fact]
        public void HardLeftLeavesRightChannelSilent() {
            // Enveloped tone (so it has real energy) panned hard left.
            var tone = new AdsrGrain(new SineGrain(440), 0.01, 0.02, 0.04, 0.03);
            float[] pcm = new GrainTimeline().Add(tone, 0.0, 1.0, -1.0).RenderStereo(48000);

            float leftPeak = 0f, rightPeak = 0f;
            for (int i = 0; i < pcm.Length; i += 2) {
                leftPeak = System.Math.Max(leftPeak, System.Math.Abs(pcm[i]));
                rightPeak = System.Math.Max(rightPeak, System.Math.Abs(pcm[i + 1]));
            }
            Assert.True(leftPeak > 0.1f, "left channel should carry the signal");
            Assert.Equal(0f, rightPeak, 5);
        }

        [Fact]
        public void GainScalesAmplitude() {
            var tone = new AdsrGrain(new SineGrain(440), 0.01, 0.02, 0.04, 0.03);

            float[] full = new GrainTimeline().Add(tone, 0.0, 1.0, 0.0).RenderStereo(48000);
            float[] half = new GrainTimeline().Add(tone, 0.0, 1.0, 0.0, gain: 0.5).RenderStereo(48000);

            float fullPeak = 0f, halfPeak = 0f;
            for (int i = 0; i < full.Length; i++) {
                fullPeak = System.Math.Max(fullPeak, System.Math.Abs(full[i]));
                halfPeak = System.Math.Max(halfPeak, System.Math.Abs(half[i]));
            }
            Assert.Equal(0.5 * fullPeak, halfPeak, 4);
        }

        [Fact]
        public void OverlappingGrainsSum() {
            // Two identical centered tones at the same start should sum to ~2x one tone.
            var tone = new AdsrGrain(new SineGrain(440), 0.01, 0.02, 0.04, 0.03);

            float[] one = new GrainTimeline().Add(tone, 0.0, 1.0, 0.0).RenderStereo(48000);
            float[] two = new GrainTimeline()
                .Add(tone, 0.0, 1.0, 0.0)
                .Add(tone, 0.0, 1.0, 0.0)
                .RenderStereo(48000);

            float onePeak = 0f, twoPeak = 0f;
            for (int i = 0; i < one.Length; i++) {
                onePeak = System.Math.Max(onePeak, System.Math.Abs(one[i]));
                twoPeak = System.Math.Max(twoPeak, System.Math.Abs(two[i]));
            }
            Assert.Equal(2.0 * onePeak, twoPeak, 4);
        }
    }
}
