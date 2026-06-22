using System;
using Tanglebeep.Audio;
using Xunit;

namespace Tanglebeep.Tests.Audio {
    public class PanLawTests {
        [Fact]
        public void HardLeft() {
            PanLaw.Compute(-1.0, out float l, out float r);
            Assert.Equal(1f, l, 5);
            Assert.Equal(0f, r, 5);
        }

        [Fact]
        public void HardRight() {
            PanLaw.Compute(1.0, out float l, out float r);
            Assert.Equal(0f, l, 5);
            Assert.Equal(1f, r, 5);
        }

        [Fact]
        public void CenterIsEqualPowerNotEqualAmplitude() {
            PanLaw.Compute(0.0, out float l, out float r);
            float expected = (float)Math.Sqrt(0.5);
            Assert.Equal(expected, l, 5);
            Assert.Equal(expected, r, 5);
        }

        [Theory]
        [InlineData(-1.0)]
        [InlineData(-0.4)]
        [InlineData(0.0)]
        [InlineData(0.6)]
        [InlineData(1.0)]
        public void ConstantPower(double pan) {
            PanLaw.Compute(pan, out float l, out float r);
            Assert.Equal(1.0, l * l + r * r, 5);
        }

        [Fact]
        public void ClampsOutOfRange() {
            PanLaw.Compute(-5.0, out float l1, out float r1);
            Assert.Equal(1f, l1, 5);
            Assert.Equal(0f, r1, 5);

            PanLaw.Compute(5.0, out float l2, out float r2);
            Assert.Equal(0f, l2, 5);
            Assert.Equal(1f, r2, 5);
        }
    }
}
