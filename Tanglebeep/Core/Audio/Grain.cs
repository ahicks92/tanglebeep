namespace Tanglebeep.Audio {
    /// <summary>
    /// A grain is a short (almost always sub-second) mono source of audio that can
    /// evaluate itself at a time <c>t</c> (in seconds) relative to its own start, where
    /// <c>t == 0.0</c> is the first sample. Every grain has a finite, known
    /// <see cref="Duration"/> so a <see cref="GrainTimeline"/> can determine its full
    /// length before rendering a single sample.
    ///
    /// Grains are sample-rate-agnostic: they are pure functions of continuous time. The
    /// sample rate only enters when a timeline renders them to PCM. Pan is not a grain
    /// concept — a grain is mono; stereo placement happens on the timeline.
    /// </summary>
    public abstract class Grain {
        /// <summary>Length of the grain in seconds. Evaluation outside [0, Duration) is silence.</summary>
        public abstract double Duration { get; }

        /// <summary>
        /// The mono sample at time <paramref name="t"/> seconds from the grain's start.
        /// Returns 0 for <paramref name="t"/> outside [0, <see cref="Duration"/>) so grains
        /// compose safely.
        /// </summary>
        public abstract float Evaluate(double t);
    }
}
