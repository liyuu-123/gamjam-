using System.Collections.Generic;
using UnityEngine;

namespace WrongTurn
{
    public sealed class ProceduralAudio
    {
        private const int SampleRate = 22050;
        private readonly AudioSource sfxSource;
        private readonly AudioSource ambienceSource;
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

        public ProceduralAudio(GameObject host)
        {
            sfxSource = host.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.volume = 0.64f;

            ambienceSource = host.AddComponent<AudioSource>();
            ambienceSource.playOnAwake = false;
            ambienceSource.loop = true;
            ambienceSource.volume = 0.12f;
            ambienceSource.clip = MakeAmbience();
            ambienceSource.Play();
        }

        public void Play(GameEventKind kind)
        {
            string key;
            switch (kind)
            {
                case GameEventKind.Move: key = "step"; break;
                case GameEventKind.Wall: key = "wall"; break;
                case GameEventKind.Record: key = "record"; break;
                case GameEventKind.Ghost: key = "ghost"; break;
                case GameEventKind.GhostHit: key = "ghost-hit"; break;
                case GameEventKind.Scout: key = "scout"; break;
                case GameEventKind.AttackHit: key = "hit"; break;
                case GameEventKind.AttackMiss: key = "miss"; break;
                case GameEventKind.Pickup: key = "pickup"; break;
                case GameEventKind.Hurt: key = "hurt"; break;
                case GameEventKind.Death: key = "death"; break;
                case GameEventKind.Respawn: key = "respawn"; break;
                case GameEventKind.Memory: key = "memory"; break;
                case GameEventKind.Boss: key = "boss"; break;
                case GameEventKind.Victory: key = "victory"; break;
                case GameEventKind.ExitLocked: key = "locked"; break;
                default: key = "invalid"; break;
            }
            PlayClip(key);
        }

        public void SetAmbienceVolume(float volume)
        {
            if (ambienceSource != null)
                ambienceSource.volume = Mathf.Clamp01(volume);
        }

        public void PlayClip(string key)
        {
            AudioClip clip;
            if (!clips.TryGetValue(key, out clip))
            {
                clip = BuildClip(key);
                clips[key] = clip;
            }
            if (clip != null)
                sfxSource.PlayOneShot(clip);
        }

        private static AudioClip BuildClip(string key)
        {
            switch (key)
            {
                case "step": return Sweep("Step", 0.055f, 105f, 72f, 0.11f, 0.01f);
                case "wall": return Noise("Wall", 0.08f, 0.16f, 72, 18);
                case "record": return Chord("Record", 0.18f, new[] { 220f, 330f }, 0.08f);
                case "ghost": return Sweep("Ghost", 0.35f, 330f, 660f, 0.12f, 0.02f);
                case "ghost-hit": return Sweep("GhostHit", 0.22f, 180f, 65f, 0.2f, 0.01f);
                case "scout": return Chord("Scout", 0.3f, new[] { 440f, 660f, 880f }, 0.08f);
                case "hit": return Noise("Hit", 0.12f, 0.23f, 380, 40);
                case "miss": return Sweep("Miss", 0.14f, 500f, 260f, 0.07f, 0.01f);
                case "pickup": return Chord("Pickup", 0.32f, new[] { 523f, 659f, 784f }, 0.1f);
                case "hurt": return Noise("Hurt", 0.22f, 0.24f, 160, 48);
                case "death": return Sweep("Death", 1.2f, 180f, 40f, 0.23f, 0.12f);
                case "respawn": return Chord("Respawn", 0.65f, new[] { 196f, 294f, 392f, 588f }, 0.11f);
                case "memory": return Chord("Memory", 0.7f, new[] { 330f, 440f, 554f, 660f }, 0.09f);
                case "boss": return Sweep("Boss", 0.8f, 55f, 35f, 0.25f, 0.1f);
                case "victory": return Chord("Victory", 1.6f, new[] { 262f, 330f, 392f, 523f }, 0.14f);
                case "shot": return Sweep("Shot", 0.07f, 980f, 320f, 0.12f, 0.015f);
                case "dash": return Sweep("Dash", 0.16f, 170f, 620f, 0.13f, 0.04f);
                case "ricochet": return Chord("Ricochet", 0.11f, new[] { 880f, 1320f }, 0.08f);
                case "slash": return Noise("Slash", 0.13f, 0.18f, 540, 70);
                case "aim": return Sweep("Aim", 0.16f, 260f, 440f, 0.06f, 0.005f);
                case "enemy-shot": return Sweep("EnemyShot", 0.12f, 380f, 170f, 0.11f, 0.015f);
                case "boss-hit": return Noise("BossHit", 0.36f, 0.25f, 90, 28);
                case "ui": return Sweep("UI", 0.045f, 640f, 820f, 0.035f, 0f);
                case "locked": return Sweep("Locked", 0.18f, 120f, 90f, 0.1f, 0.01f);
                default: return Sweep("Invalid", 0.11f, 140f, 118f, 0.06f, 0.02f);
            }
        }

        private static AudioClip Sweep(string name, float duration, float from, float to,
            float amplitude, float noise)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            var data = new float[samples];
            var random = new System.Random(name.GetHashCode());
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float freq = Mathf.Lerp(from, to, t);
                phase += freq / SampleRate * Mathf.PI * 2f;
                float envelope = Mathf.Pow(1f - t, 2f) * Mathf.Min(1f, i / 80f);
                float tone = Mathf.Sin(phase) * 0.78f + Mathf.Sin(phase * 2.01f) * 0.22f;
                float grit = ((float)random.NextDouble() * 2f - 1f) * noise;
                data[i] = (tone + grit) * amplitude * envelope;
            }
            var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Chord(string name, float duration, float[] frequencies,
            float amplitude)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float envelope = Mathf.Min(1f, i / 140f) * Mathf.Pow(1f - t, 1.8f);
                float sample = 0f;
                for (int n = 0; n < frequencies.Length; n++)
                {
                    float arpeggio = Mathf.Clamp01(t * 8f - n * 0.9f);
                    sample += Mathf.Sin(2f * Mathf.PI * frequencies[n] * i / SampleRate) *
                              arpeggio * (1f - n * 0.08f);
                }
                data[i] = sample / Mathf.Max(1, frequencies.Length) * amplitude * envelope;
            }
            var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Noise(string name, float duration, float amplitude,
            float lowTone, float highTone)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            var data = new float[samples];
            var random = new System.Random(name.GetHashCode());
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float envelope = Mathf.Pow(1f - t, 2.2f);
                float n = (float)random.NextDouble() * 2f - 1f;
                float tone = Mathf.Sin(2f * Mathf.PI *
                    Mathf.Lerp(lowTone, highTone, t) * i / SampleRate);
                data[i] = (n * 0.45f + tone * 0.55f) * amplitude * envelope;
            }
            var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip MakeAmbience()
        {
            const float duration = 12f;
            int samples = Mathf.CeilToInt(SampleRate * duration);
            var data = new float[samples];
            var random = new System.Random(913);
            float low = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float noise = (float)random.NextDouble() * 2f - 1f;
                low = Mathf.Lerp(low, noise, 0.0012f);
                float drone = Mathf.Sin(2f * Mathf.PI * 43f * t) * 0.16f +
                              Mathf.Sin(2f * Mathf.PI * 64.5f * t) * 0.08f;
                float tide = 0.55f + Mathf.Sin(2f * Mathf.PI * t / duration) * 0.18f;
                data[i] = (low * 0.32f + drone) * tide;
            }
            var clip = AudioClip.Create("AmbientDark", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
