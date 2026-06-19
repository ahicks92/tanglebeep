using System;

namespace TangledeepAccess.Audio {
    /// <summary>
    /// A reservoir of band-pass-filtered white noise for one frequency band. Pre-filters a buffer
    /// once (amortizing the cost), then hands out successive non-overlapping slices via
    /// <see cref="Take"/>; when the buffer runs short it regenerates from fresh noise. Each slice is
    /// a different draw, so two slices taken for the same band (the left and right walls) are
    /// decorrelated and stay perceptually separate when panned apart.
    ///
    /// <para>Generation: fill <c>length + warmup</c> samples of white noise, band-pass it from a
    /// zeroed filter, discard the leading <c>warmup</c> samples (the filter's settling transient),
    /// then RMS-normalize so the perceived loudness is independent of Q.</para>
    /// </summary>
    public sealed class NoisePool {
        private readonly double _centerHz;
        private readonly double _q;
        private readonly int _sampleRate;
        private readonly int _lengthSamples;
        private readonly int _warmupSamples;
        private readonly double _targetRms;
        private readonly Random _rng;

        private float[] _data;
        private int _cursor;

        public int SampleRate => _sampleRate;

        public NoisePool(double centerHz, double q, int sampleRate, int lengthSamples, int warmupSamples, double targetRms, int seed) {
            _centerHz = centerHz;
            _q = q;
            _sampleRate = sampleRate;
            _lengthSamples = lengthSamples;
            _warmupSamples = warmupSamples;
            _targetRms = targetRms;
            _rng = new Random(seed);
            Refill();
        }

        /// <summary>A grain over the next <paramref name="seconds"/> of pool, advancing the cursor.</summary>
        public BufferGrain Take(double seconds) {
            int count = (int)Math.Ceiling(seconds * _sampleRate);
            if (count > _data.Length) {
                count = _data.Length; // a single tone longer than the whole pool: clamp (shouldn't happen)
            }
            if (_cursor + count > _data.Length) {
                Refill(); // not enough left; regenerate (old slices keep referencing the old array)
            }

            var grain = new BufferGrain(_data, _cursor, count, _sampleRate);
            _cursor += count;
            return grain;
        }

        private void Refill() {
            var raw = new float[_lengthSamples + _warmupSamples];
            WhiteNoise.Fill(raw, _rng);
            Biquad.Bandpass(_centerHz, _q, _sampleRate).ProcessInPlace(raw);

            var data = new float[_lengthSamples];
            Array.Copy(raw, _warmupSamples, data, 0, _lengthSamples);
            NormalizeRms(data, _targetRms);

            _data = data;
            _cursor = 0;
        }

        private static void NormalizeRms(float[] buffer, double target) {
            double sumSquares = 0.0;
            for (int i = 0; i < buffer.Length; i++) {
                sumSquares += (double)buffer[i] * buffer[i];
            }
            double rms = Math.Sqrt(sumSquares / buffer.Length);
            if (rms < 1e-9) {
                return;
            }

            double scale = target / rms;
            for (int i = 0; i < buffer.Length; i++) {
                buffer[i] = (float)(buffer[i] * scale);
            }
        }
    }
}
