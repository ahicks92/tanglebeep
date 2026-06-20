using System.Collections.Generic;
using System.IO;
using TangledeepAccess.Gameplay;
using TangledeepAccess.Util;

namespace TangledeepAccess.Audio {
    /// <summary>
    /// Loads the entity-scanner's per-category radar samples (radar_monster/container/powerup/
    /// service_shop/stairs .wav) once and serves their decoded mono PCM by <see cref="RadarCategory"/>.
    /// A category with no sample (Default, or one whose resource failed to load) returns false from
    /// <see cref="TryGet"/>, and the scanner falls back to its triangle tone — so a missing or bad asset
    /// degrades to the old behavior, audibly never silently. Mirrors <see cref="CursorSounds"/>'
    /// embedded-resource load but keeps raw samples for a <see cref="BufferGrain"/> on the timeline.
    /// </summary>
    public static class SampleBank {
        private struct Clip {
            public float[] Samples;
            public int SampleRate;
        }

        private static readonly Dictionary<RadarCategory, Clip> Clips = new Dictionary<RadarCategory, Clip>();
        private static bool _loaded;

        /// <summary>The decoded sample for a category, or false (→ triangle fallback) if none is loaded.</summary>
        public static bool TryGet(RadarCategory category, out float[] samples, out int sampleRate) {
            EnsureLoaded();
            if (Clips.TryGetValue(category, out Clip clip)) {
                samples = clip.Samples;
                sampleRate = clip.SampleRate;
                return true;
            }

            samples = null;
            sampleRate = 0;
            return false;
        }

        private static void EnsureLoaded() {
            if (_loaded) {
                return;
            }

            _loaded = true; // set first: a failed load logs and is simply absent from the map
            Load(RadarCategory.Monster, "radar_monster.wav");
            Load(RadarCategory.Container, "radar_container.wav");
            Load(RadarCategory.Powerup, "radar_powerup.wav");
            Load(RadarCategory.Shop, "radar_service_shop.wav");
            Load(RadarCategory.Stairs, "radar_stairs.wav");
        }

        private static void Load(RadarCategory category, string file) {
            string resource = "TangledeepAccess.Sounds." + file;
            using (Stream stream = typeof(SampleBank).Assembly.GetManifestResourceStream(resource)) {
                if (stream == null) {
                    Log.Warn("Radar sound resource missing: " + resource);
                    return;
                }

                var bytes = new byte[stream.Length];
                int read = 0;
                while (read < bytes.Length) {
                    int n = stream.Read(bytes, read, bytes.Length - read);
                    if (n == 0) {
                        break;
                    }
                    read += n;
                }

                WavData wav = WavData.Parse(bytes);
                Clips[category] = new Clip { Samples = ToMono(wav, file), SampleRate = wav.SampleRate };
            }
        }

        // BufferGrain reads its array as a mono stream; a stereo asset would read as interleaved garbage,
        // so collapse to the left channel (and warn) if we are handed one.
        private static float[] ToMono(WavData wav, string file) {
            if (wav.Channels == 1) {
                return wav.Samples;
            }

            Log.Warn(file + " is not mono (" + wav.Channels + " ch); using the left channel");
            int frames = wav.Samples.Length / wav.Channels;
            var mono = new float[frames];
            for (int i = 0; i < frames; i++) {
                mono[i] = wav.Samples[i * wav.Channels];
            }
            return mono;
        }
    }
}
