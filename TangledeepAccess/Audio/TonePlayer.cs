using UnityEngine;

namespace TangledeepAccess.Audio {
    /// <summary>
    /// Plays a mod-synthesized interleaved-stereo PCM buffer through a single mod-owned
    /// <see cref="AudioSource"/>, bypassing the game's mixer and all DSP so the buffer reaches the
    /// output sample-for-sample (the "no effects" path: 2D, centered, mixer-group-less, with the
    /// effect/listener/reverb bypasses on). The buffer must be rendered at the same sample rate the
    /// clip is created with, so the caller passes the rate it used. Engine-side glue (touches Unity),
    /// so it lives outside Core; the buffer math lives in Core.
    /// </summary>
    public static class TonePlayer {
        private static AudioSource _source;

        private static AudioSource Source() {
            if (_source == null) {
                var go = new GameObject("TangledeepAccess.TonePlayer");
                Object.DontDestroyOnLoad(go);
                _source = go.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.loop = false;
                _source.spatialBlend = 0f;          // 2D: no distance attenuation / 3D panning
                _source.panStereo = 0f;             // leave the buffer's own L/R intact
                _source.pitch = 1f;                 // no resample-by-pitch
                _source.volume = 1f;
                _source.outputAudioMixerGroup = null; // straight to the listener, not the game mixer
                _source.bypassEffects = true;
                _source.bypassListenerEffects = true;
                _source.bypassReverbZones = true;
            }
            return _source;
        }

        /// <summary>Play an interleaved stereo buffer (L, R, L, R, …) rendered at <paramref name="sampleRate"/> Hz.</summary>
        public static void PlayStereo(float[] pcm, int sampleRate) {
            if (pcm == null || pcm.Length == 0) {
                return;
            }

            AudioSource source = Source();
            var clip = AudioClip.Create("wallEcho", pcm.Length / 2, 2, sampleRate, false);
            clip.SetData(pcm, 0);
            source.Stop();
            source.clip = clip;
            source.Play();
        }
    }
}
