using System;

namespace TangledeepAccess.Audio {
    /// <summary>
    /// A direct-form-I biquad (two-pole/two-zero IIR) filter, used offline to colour white noise.
    /// Stateful — output depends on the previous two in/out samples — so it is run sequentially over
    /// a buffer from a zeroed state, never sampled pointwise. Coefficients computed in double; the
    /// only factory here is the RBJ constant-0 dB-peak band-pass.
    /// </summary>
    public sealed class Biquad {
        private double _b0, _b1, _b2, _a1, _a2;
        private double _x1, _x2, _y1, _y2;

        /// <summary>
        /// RBJ band-pass with 0 dB peak gain at <paramref name="centerHz"/>; <paramref name="q"/> is
        /// the resonance (center ÷ bandwidth). Higher Q is a narrower, more tonal band.
        /// </summary>
        public static Biquad Bandpass(double centerHz, double q, int sampleRate) {
            double w0 = 2.0 * Math.PI * centerHz / sampleRate;
            double cosw0 = Math.Cos(w0);
            double alpha = Math.Sin(w0) / (2.0 * q);
            double a0 = 1.0 + alpha;

            var bq = new Biquad();
            bq._b0 = alpha / a0;
            bq._b1 = 0.0;
            bq._b2 = -alpha / a0;
            bq._a1 = -2.0 * cosw0 / a0;
            bq._a2 = (1.0 - alpha) / a0;
            return bq;
        }

        public void Reset() {
            _x1 = _x2 = _y1 = _y2 = 0.0;
        }

        public float Process(float x) {
            double y = _b0 * x + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
            _x2 = _x1;
            _x1 = x;
            _y2 = _y1;
            _y1 = y;
            return (float)y;
        }

        public void ProcessInPlace(float[] buffer) {
            for (int i = 0; i < buffer.Length; i++) {
                buffer[i] = Process(buffer[i]);
            }
        }
    }
}
