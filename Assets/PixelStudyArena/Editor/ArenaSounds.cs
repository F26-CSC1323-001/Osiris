using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PixelStudy.EditorTools
{
    /// <summary>
    /// Makes the game's sound effects from scratch, like ArenaArt makes the art: simple retro synth blips,
    /// whooshes and booms (tones that slide in pitch, plus filtered noise), saved as .wav files in a
    /// Resources/Sfx folder where Sfx.Play finds them by name. Run by the arena builder.
    /// </summary>
    public static class ArenaSounds
    {
        public const string Dir = "Assets/PixelStudyArena/Resources/Sfx";
        const int Rate = 22050;

        enum Wave { Square, Triangle, Sine, Saw }

        /// <summary>A mono buffer that tones and noise are mixed into.</summary>
        class Sound
        {
            public readonly float[] S;
            readonly System.Random rng;

            public Sound(float seconds, int seed = 1)
            {
                S = new float[Mathf.CeilToInt(seconds * Rate)];
                rng = new System.Random(seed);
            }

            /// <summary>A tone sliding from f0 to f1 Hz over its length, with a quick attack and a fading tail.</summary>
            public Sound Tone(Wave wave, float start, float length, float f0, float f1, float volume, float duty = 0.5f, float vibratoHz = 0f, float vibrato = 0f)
            {
                int from = (int)(start * Rate), n = (int)(length * Rate);
                double phase = 0;
                for (int i = 0; i < n && from + i < S.Length; i++)
                {
                    float f = f0 * Mathf.Pow(f1 / f0, i / (float)n);
                    if (vibrato > 0f) f *= 1f + vibrato * Mathf.Sin(2f * Mathf.PI * vibratoHz * i / Rate);
                    phase += f / Rate;
                    float p = (float)(phase - Math.Floor(phase));
                    float v = wave switch
                    {
                        Wave.Square => p < duty ? 1f : -1f,
                        Wave.Triangle => 1f - 4f * Mathf.Abs(p - 0.5f),
                        Wave.Sine => Mathf.Sin(2f * Mathf.PI * p),
                        _ => 2f * p - 1f,
                    };
                    S[from + i] += v * volume * Envelope(i, n);
                }
                return this;
            }

            /// <summary>Noise through a low-pass filter whose cutoff slides from c0 to c1 Hz (bright hiss to dull rumble, or back).</summary>
            public Sound Noise(float start, float length, float c0, float c1, float volume)
            {
                int from = (int)(start * Rate), n = (int)(length * Rate);
                float y = 0f;
                for (int i = 0; i < n && from + i < S.Length; i++)
                {
                    float cutoff = c0 * Mathf.Pow(c1 / c0, i / (float)n);
                    float k = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);
                    y += k * ((float)rng.NextDouble() * 2f - 1f - y);
                    // a low cutoff also makes the noise quieter: make up for it so a rumble still carries
                    S[from + i] += y / Mathf.Max(0.25f, Mathf.Sqrt(k)) * volume * Envelope(i, n);
                }
                return this;
            }

            /// <summary>Notes one after the other (an arpeggio or a little jingle).</summary>
            public Sound Notes(Wave wave, float start, float step, float length, float volume, float duty, params float[] hz)
            {
                for (int i = 0; i < hz.Length; i++) Tone(wave, start + i * step, i == hz.Length - 1 ? length * 2f : length, hz[i], hz[i], volume, duty);
                return this;
            }

            static float Envelope(int i, int n)
            {
                int attack = Mathf.Max(1, Mathf.Min(n / 8, Rate / 200)); // up to 5 ms
                if (i < attack) return i / (float)attack;
                float t = (i - attack) / (float)Mathf.Max(1, n - attack);
                return (1f - t) * (1f - t);
            }
        }

        public static void GenerateAll()
        {
            Directory.CreateDirectory(Dir);

            // the knight
            Save("swing", new Sound(0.15f, 1).Noise(0f, 0.13f, 6000f, 1500f, 0.55f).Tone(Wave.Saw, 0f, 0.1f, 900f, 300f, 0.08f));
            Save("hit", new Sound(0.1f, 2).Tone(Wave.Square, 0f, 0.09f, 420f, 110f, 0.35f).Noise(0f, 0.07f, 5000f, 800f, 0.5f));
            Save("crit", new Sound(0.25f, 3).Tone(Wave.Square, 0f, 0.18f, 300f, 60f, 0.4f).Noise(0f, 0.15f, 7000f, 600f, 0.6f).Tone(Wave.Triangle, 0.02f, 0.2f, 1200f, 1500f, 0.15f));
            Save("jump", new Sound(0.13f).Tone(Wave.Square, 0f, 0.12f, 280f, 620f, 0.25f, 0.25f));
            Save("land", new Sound(0.12f, 4).Noise(0f, 0.1f, 1200f, 200f, 0.6f).Tone(Wave.Sine, 0f, 0.08f, 120f, 60f, 0.3f));
            Save("hurt", new Sound(0.25f, 5).Tone(Wave.Saw, 0f, 0.22f, 500f, 140f, 0.35f).Noise(0f, 0.12f, 3000f, 500f, 0.3f));
            Save("death", new Sound(0.95f, 6).Tone(Wave.Square, 0f, 0.9f, 440f, 70f, 0.3f, 0.5f, 8f, 0.03f).Noise(0f, 0.4f, 2000f, 200f, 0.2f));

            // bugs and bosses
            Save("tell", new Sound(0.08f).Tone(Wave.Triangle, 0f, 0.07f, 1300f, 1700f, 0.25f));
            Save("bug_death", new Sound(0.24f, 7).Noise(0f, 0.22f, 4000f, 300f, 0.6f).Tone(Wave.Square, 0f, 0.18f, 700f, 90f, 0.25f, 0.3f));
            Save("roar", new Sound(0.95f, 8).Noise(0f, 0.9f, 900f, 250f, 0.7f).Tone(Wave.Saw, 0f, 0.9f, 95f, 60f, 0.35f, 0.5f, 14f, 0.12f));
            Save("boom", new Sound(0.55f, 9).Tone(Wave.Sine, 0f, 0.5f, 110f, 35f, 0.7f).Noise(0f, 0.45f, 2500f, 120f, 0.7f));
            Save("spawn", new Sound(0.36f, 10).Noise(0f, 0.35f, 400f, 3000f, 0.25f).Tone(Wave.Sine, 0f, 0.35f, 200f, 600f, 0.12f));

            // pickups and the level
            Save("coin", new Sound(0.18f).Tone(Wave.Square, 0f, 0.05f, 988f, 988f, 0.22f).Tone(Wave.Square, 0.05f, 0.12f, 1319f, 1319f, 0.22f));
            Save("heart", new Sound(0.36f).Notes(Wave.Triangle, 0f, 0.06f, 0.08f, 0.35f, 0.5f, 523f, 659f, 784f, 1047f));
            Save("door", new Sound(0.7f).Notes(Wave.Triangle, 0f, 0.08f, 0.25f, 0.3f, 0.5f, 392f, 523f, 659f, 784f));

            // menus and the quiz
            Save("click", new Sound(0.04f).Tone(Wave.Square, 0f, 0.035f, 1200f, 900f, 0.18f, 0.25f));
            Save("correct", new Sound(0.3f).Tone(Wave.Square, 0f, 0.09f, 784f, 784f, 0.2f, 0.25f).Tone(Wave.Square, 0.09f, 0.2f, 1175f, 1175f, 0.2f, 0.25f));
            Save("wrong", new Sound(0.38f).Tone(Wave.Square, 0f, 0.12f, 220f, 200f, 0.22f).Tone(Wave.Square, 0.12f, 0.25f, 160f, 140f, 0.22f));
            Save("clear", new Sound(0.75f).Notes(Wave.Square, 0f, 0.1f, 0.1f, 0.2f, 0.25f, 523f, 659f, 784f, 1047f));

            // Flame Slash
            Save("flame_ignite", new Sound(0.48f, 11).Noise(0f, 0.45f, 800f, 5000f, 0.45f).Tone(Wave.Saw, 0f, 0.45f, 80f, 160f, 0.12f));
            Save("flame_burst", new Sound(0.75f, 12).Noise(0f, 0.7f, 6000f, 300f, 0.85f).Tone(Wave.Sine, 0f, 0.5f, 140f, 40f, 0.6f).Tone(Wave.Saw, 0f, 0.3f, 300f, 90f, 0.15f));

            AssetDatabase.Refresh();
        }

        /// <summary>Normalizes the sound and writes it as a 16-bit mono .wav.</summary>
        static void Save(string name, Sound sound)
        {
            var s = sound.S;
            float peak = 0f;
            foreach (var v in s) peak = Mathf.Max(peak, Mathf.Abs(v));
            float gain = peak > 0f ? 0.8f / peak : 0f;

            var path = $"{Dir}/{name}.wav";
            using (var w = new BinaryWriter(File.Create(path)))
            {
                int dataBytes = s.Length * 2;
                w.Write(Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataBytes);
                w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16);
                w.Write((short)1);        // PCM
                w.Write((short)1);        // mono
                w.Write(Rate);
                w.Write(Rate * 2);        // bytes per second
                w.Write((short)2);        // bytes per sample
                w.Write((short)16);       // bits
                w.Write(Encoding.ASCII.GetBytes("data"));
                w.Write(dataBytes);
                foreach (var v in s) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(v * gain, -1f, 1f) * 32767f));
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
