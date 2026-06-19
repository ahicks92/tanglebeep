using System;
using TangledeepAccess.Audio;
using Xunit;

namespace TangledeepAccess.Tests.Audio {
    public class BiquadTests {
        private const int Sr = 48000;

        private static float[] Sine(double freq, int n) {
            var b = new float[n];
            for (int i = 0; i < n; i++) {
                b[i] = (float)Math.Sin(2.0 * Math.PI * freq * i / Sr);
            }
            return b;
        }

        // RMS of the settled tail (skip the filter warm-up at the front).
        private static double TailRms(float[] b, int skip) {
            double sum = 0.0;
            for (int i = skip; i < b.Length; i++) {
                sum += (double)b[i] * b[i];
            }
            return Math.Sqrt(sum / (b.Length - skip));
        }

        private static double FilteredTailRms(double signalHz, double centerHz, double q) {
            float[] sig = Sine(signalHz, 8000);
            Biquad.Bandpass(centerHz, q, Sr).ProcessInPlace(sig);
            return TailRms(sig, 2000);
        }

        [Fact]
        public void PassesTheCenterFrequency() {
            // A tone at the band center should survive with substantial amplitude.
            double atCenter = FilteredTailRms(1000.0, 1000.0, 15.0);
            Assert.True(atCenter > 0.3, "center tone RMS was " + atCenter);
        }

        [Fact]
        public void RejectsFrequenciesFarFromCenter() {
            double atCenter = FilteredTailRms(1000.0, 1000.0, 15.0);
            double farAbove = FilteredTailRms(8000.0, 1000.0, 15.0);
            double farBelow = FilteredTailRms(120.0, 1000.0, 15.0);

            Assert.True(farAbove < atCenter * 0.1, "far-above leaked: " + farAbove + " vs " + atCenter);
            Assert.True(farBelow < atCenter * 0.1, "far-below leaked: " + farBelow + " vs " + atCenter);
        }

        [Fact]
        public void HigherQIsNarrower() {
            // An off-center (but not far) tone passes less as Q rises and the band tightens.
            double offCenterLowQ = FilteredTailRms(1300.0, 1000.0, 5.0);
            double offCenterHighQ = FilteredTailRms(1300.0, 1000.0, 30.0);
            Assert.True(offCenterHighQ < offCenterLowQ,
                "highQ " + offCenterHighQ + " should be < lowQ " + offCenterLowQ);
        }
    }
}
