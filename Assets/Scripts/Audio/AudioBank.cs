using System.Collections.Generic;
using UnityEngine;

namespace DivergentGenesis.Audio
{
    /// <summary>
    /// Every sound in the game is synthesised at runtime. No audio files means no
    /// download size, no import settings to get wrong, and the APK stays tiny.
    /// </summary>
    public sealed class AudioBank : MonoBehaviour
    {
        public static AudioBank Instance;

        [Range(0f, 1f)] public float Volume = 0.55f;

        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>(16);
        private AudioSource _source;
        private bool _built;

        private void Awake()
        {
            Instance = this;
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            Build();
        }

        private void Build()
        {
            _clips["swing"] = NoiseBurst("swing", 0.09f, 1800f, 0.35f, 0.6f);
            _clips["hit"] = Tone("hit", 0.10f, 220f, 90f, 0.5f, 0.3f);
            _clips["break"] = NoiseBurst("break", 0.20f, 900f, 0.25f, 0.55f);
            _clips["mine"] = NoiseBurst("mine", 0.05f, 2400f, 0.30f, 0.22f);
            _clips["place"] = Tone("place", 0.09f, 420f, 260f, 0.35f, 0.25f);
            _clips["chop"] = NoiseBurst("chop", 0.14f, 1300f, 0.28f, 0.5f);
            _clips["eat"] = NoiseBurst("eat", 0.12f, 700f, 0.18f, 0.3f);
            _clips["craft"] = Arpeggio("craft", new[] { 523f, 659f, 784f }, 0.07f, 0.28f);
            _clips["ui"] = Tone("ui", 0.05f, 880f, 880f, 0.22f, 0.2f);
            _clips["hurt"] = Tone("hurt", 0.22f, 320f, 120f, 0.5f, 0.4f);
            _clips["splash"] = NoiseBurst("splash", 0.35f, 2200f, 0.2f, 0.4f);
            _clips["levelup"] = Arpeggio("levelup", new[] { 523f, 784f, 1046f, 1318f }, 0.10f, 0.32f);
            _clips["pickup"] = Tone("pickup", 0.08f, 1200f, 1700f, 0.28f, 0.2f);
            _built = true;
        }

        public void Play(string id)
        {
            if (!_built) Build();
            AudioClip clip;
            if (!_clips.TryGetValue(id, out clip) || clip == null) return;
            _source.PlayOneShot(clip, Volume);
        }

        public void Play(string id, float volumeScale)
        {
            if (!_built) Build();
            AudioClip clip;
            if (!_clips.TryGetValue(id, out clip) || clip == null) return;
            _source.PlayOneShot(clip, Volume * volumeScale);
        }

        // ------------------------------------------------------------ synthesis
        private static AudioClip NoiseBurst(string name, float seconds, float cutoff, float decay, float amp)
        {
            const int rate = 22050;
            int count = Mathf.Max(16, (int)(rate * seconds));
            var data = new float[count];
            var rnd = new System.Random(name.GetHashCode());
            float lp = 0f;
            float alpha = Mathf.Clamp01(cutoff / rate * 6f);

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float n = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += alpha * (n - lp);
                float env = Mathf.Exp(-t * (1f / Mathf.Max(0.01f, decay)) * 5f);
                data[i] = lp * env * amp;
            }

            var clip = AudioClip.Create(name, count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Tone(string name, float seconds, float startHz, float endHz, float amp, float decay)
        {
            const int rate = 22050;
            int count = Mathf.Max(16, (int)(rate * seconds));
            var data = new float[count];
            float phase = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float hz = Mathf.Lerp(startHz, endHz, t);
                phase += hz / rate * Mathf.PI * 2f;
                float env = Mathf.Exp(-t * (1f / Mathf.Max(0.01f, decay)) * 4.5f) * (1f - t * 0.2f);
                data[i] = Mathf.Sin(phase) * env * amp + Mathf.Sin(phase * 2.02f) * env * amp * 0.22f;
            }

            var clip = AudioClip.Create(name, count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Arpeggio(string name, float[] notes, float noteSeconds, float amp)
        {
            const int rate = 22050;
            int count = Mathf.Max(16, (int)(rate * noteSeconds * notes.Length));
            var data = new float[count];
            int per = Mathf.Max(1, (int)(rate * noteSeconds));
            float phase = 0f;

            for (int i = 0; i < count; i++)
            {
                int note = Mathf.Min(notes.Length - 1, i / per);
                float local = (i % per) / (float)per;
                float hz = notes[note];
                phase += hz / rate * Mathf.PI * 2f;
                float env = Mathf.Exp(-local * 3.2f) * (1f - local * 0.3f);
                data[i] = Mathf.Sin(phase) * env * amp;
            }

            var clip = AudioClip.Create(name, count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
