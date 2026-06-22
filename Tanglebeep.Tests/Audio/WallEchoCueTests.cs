using Tanglebeep.Audio;
using Xunit;

namespace Tanglebeep.Tests.Audio {
    public class WallEchoCueTests {
        [Fact]
        public void ToneLengthIsTheEnvelopeSum() {
            Assert.Equal(
                WallEchoCue.Attack + WallEchoCue.Decay + WallEchoCue.Sustain + WallEchoCue.Release,
                WallEchoCue.ToneSeconds,
                9);
        }

        [Fact]
        public void AdjacentWallIsImmediateAndDelayGrowsBeyond() {
            Assert.Equal(0.0, WallEchoCue.DelaySeconds(1), 9); // adjacent -> no latency floor
            Assert.True(WallEchoCue.DelaySeconds(5) > WallEchoCue.DelaySeconds(2));
            Assert.Equal(0.0, WallEchoCue.DelaySeconds(0), 9); // clamped (out of domain, but safe)
        }

        [Fact]
        public void AdjacentWallIsFullVolumeAndGainShrinksBeyond() {
            Assert.Equal(WallEchoCue.InitialVolume, WallEchoCue.Gain(1), 9); // adjacent reference
            Assert.True(WallEchoCue.Gain(5) < WallEchoCue.Gain(2));
        }

        [Fact]
        public void VerticalPitchesAreSymmetricAboutBase() {
            Assert.True(WallEchoCue.UpFrequencyHz > WallEchoCue.BaseFrequencyHz);
            Assert.True(WallEchoCue.DownFrequencyHz < WallEchoCue.BaseFrequencyHz);
            // ± the same semitones, so they are geometric mirrors of base.
            Assert.Equal(
                WallEchoCue.BaseFrequencyHz * WallEchoCue.BaseFrequencyHz,
                WallEchoCue.UpFrequencyHz * WallEchoCue.DownFrequencyHz,
                3);
        }
    }
}
