using System;

namespace TangledeepAccess.Audio {
    /// <summary>
    /// An attack/decay/sustain/release amplitude envelope that wraps another grain. The
    /// envelope's own length is <c>attack + decay + sustain + release</c>; the wrapped
    /// grain has its own length; the resulting grain lasts the lesser of the two. Output
    /// is the envelope gain times the inner grain's sample, so this both shapes amplitude
    /// and bounds duration (e.g. a 100 ms enveloped tone = a 100 ms envelope over the
    /// 1 s <see cref="SineGrain"/>).
    ///
    /// Segment times are in seconds; <see cref="SustainLevel"/> is the gain (typically
    /// 0..1) held during the sustain segment and reached at the end of decay.
    /// </summary>
    public sealed class AdsrGrain : Grain {
        public Grain Inner { get; }
        public double Attack { get; }
        public double Decay { get; }
        public double Sustain { get; }
        public double Release { get; }
        public double SustainLevel { get; }

        public AdsrGrain(
            Grain inner,
            double attack,
            double decay,
            double sustain,
            double release,
            double sustainLevel = 1.0) {
            Inner = inner;
            Attack = attack;
            Decay = decay;
            Sustain = sustain;
            Release = release;
            SustainLevel = sustainLevel;
        }

        /// <summary>Total length of the envelope itself, ignoring the wrapped grain.</summary>
        public double EnvelopeDuration => Attack + Decay + Sustain + Release;

        public override double Duration => Math.Min(Inner.Duration, EnvelopeDuration);

        /// <summary>
        /// The envelope gain at time <paramref name="t"/> seconds, in [0, 1] (scaled by
        /// <see cref="SustainLevel"/> across the held segment). 0 outside the envelope.
        /// Zero-length segments are skipped without dividing by zero.
        /// </summary>
        public double EnvelopeGain(double t) {
            if (t < 0.0) {
                return 0.0;
            }
            double decayEnd = Attack + Decay;
            double sustainEnd = decayEnd + Sustain;
            double releaseEnd = sustainEnd + Release;

            if (t < Attack) {
                return t / Attack; // ramp 0 -> 1
            }
            if (t < decayEnd) {
                double f = (t - Attack) / Decay; // 0..1 across decay
                return 1.0 + (SustainLevel - 1.0) * f; // 1 -> SustainLevel
            }
            if (t < sustainEnd) {
                return SustainLevel;
            }
            if (t < releaseEnd) {
                double f = (t - sustainEnd) / Release; // 0..1 across release
                return SustainLevel * (1.0 - f); // SustainLevel -> 0
            }
            return 0.0;
        }

        public override float Evaluate(double t) {
            if (t < 0.0 || t >= Duration) {
                return 0f;
            }
            return (float)(EnvelopeGain(t) * Inner.Evaluate(t));
        }
    }
}
