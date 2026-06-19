using System;
using System.Collections.Generic;

namespace TangledeepAccess.Audio {
    /// <summary>One grain placed on a timeline: when it starts, how fast it plays, where it sits in stereo.</summary>
    public readonly struct GrainPlacement {
        public readonly Grain Grain;

        /// <summary>Timeline time (seconds) at which the grain's sample 0 plays.</summary>
        public readonly double Start;

        /// <summary>
        /// Varispeed factor. The grain is sampled at <c>(timelineTime - Start) · Rate</c>,
        /// so rate &gt; 1 is higher-pitched and shorter; its timeline length is
        /// <c>Grain.Duration / Rate</c>. Must be &gt; 0.
        /// </summary>
        public readonly double Rate;

        /// <summary>Pan position in [-1, 1]; see <see cref="PanLaw"/>.</summary>
        public readonly double Pan;

        /// <summary>Linear amplitude scale applied to the grain's samples before panning (1.0 = unchanged).</summary>
        public readonly double Gain;

        public GrainPlacement(Grain grain, double start, double rate, double pan, double gain) {
            Grain = grain;
            Start = start;
            Rate = rate;
            Pan = pan;
            Gain = gain;
        }

        /// <summary>This grain's end time on the timeline, accounting for playback rate.</summary>
        public double End => Start + Grain.Duration / Rate;
    }

    /// <summary>
    /// An ordered collection of grain placements that renders to an interleaved stereo PCM
    /// buffer. Overlapping grains are summed; the output is not clamped (limiting, if any,
    /// is the caller's concern). The timeline's <see cref="Duration"/> is known before any
    /// sample is computed, because every grain reports a finite duration.
    /// </summary>
    public sealed class GrainTimeline {
        private readonly List<GrainPlacement> _placements = new List<GrainPlacement>();

        public IReadOnlyList<GrainPlacement> Placements => _placements;

        /// <summary>Place a grain. Returns this timeline for fluent chaining.</summary>
        public GrainTimeline Add(Grain grain, double start, double rate, double pan, double gain = 1.0) {
            _placements.Add(new GrainPlacement(grain, start, rate, pan, gain));
            return this;
        }

        /// <summary>Total length in seconds: the latest end time across all placements (0 if empty).</summary>
        public double Duration {
            get {
                double max = 0.0;
                foreach (GrainPlacement p in _placements) {
                    double end = p.End;
                    if (end > max) {
                        max = end;
                    }
                }
                return max;
            }
        }

        /// <summary>
        /// Render the whole timeline to an interleaved stereo float buffer (L, R, L, R, …)
        /// at <paramref name="sampleRate"/> Hz. Length is <c>ceil(Duration · sampleRate) · 2</c>.
        /// </summary>
        public float[] RenderStereo(int sampleRate) {
            int totalFrames = (int)Math.Ceiling(Duration * sampleRate);
            var buffer = new float[totalFrames * 2];

            foreach (GrainPlacement p in _placements) {
                float leftGain, rightGain;
                PanLaw.Compute(p.Pan, out leftGain, out rightGain);

                int firstFrame = (int)Math.Floor(p.Start * sampleRate);
                if (firstFrame < 0) {
                    firstFrame = 0;
                }
                int lastFrame = (int)Math.Ceiling(p.End * sampleRate);
                if (lastFrame > totalFrames) {
                    lastFrame = totalFrames;
                }

                for (int i = firstFrame; i < lastFrame; i++) {
                    double timelineTime = (double)i / sampleRate;
                    double grainTime = (timelineTime - p.Start) * p.Rate;
                    if (grainTime < 0.0 || grainTime >= p.Grain.Duration) {
                        continue;
                    }
                    float sample = (float)(p.Grain.Evaluate(grainTime) * p.Gain);
                    buffer[2 * i] += sample * leftGain;
                    buffer[2 * i + 1] += sample * rightGain;
                }
            }

            return buffer;
        }
    }
}
