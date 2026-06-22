using System;

namespace Tanglebeep.Audio {
    /// <summary>
    /// A fixed-frequency sine wave. Its duration is capped at 1 second: a sine of any
    /// integral frequency completes a whole number of cycles in exactly one second, so
    /// 1 s is the natural periodic boundary. Shorter sounds are made not by shrinking the
    /// sine but by wrapping it in an <see cref="AdsrGrain"/> (whose shorter duration wins),
    /// which also removes the click a raw truncation would produce.
    /// </summary>
    public sealed class SineGrain : Grain {
        public const double CappedDuration = 1.0;

        public double Frequency { get; }
        public double Amplitude { get; }

        public SineGrain(double frequency, double amplitude = 1.0) {
            Frequency = frequency;
            Amplitude = amplitude;
        }

        public override double Duration => CappedDuration;

        public override float Evaluate(double t) {
            if (t < 0.0 || t >= CappedDuration) {
                return 0f;
            }
            return (float)(Amplitude * Math.Sin(2.0 * Math.PI * Frequency * t));
        }
    }
}
