using System.Collections.Generic;
using UnityEngine;

namespace PixelStudy
{
    /// <summary>
    /// Simple sound effects: Sfx.Play("hit"). The clips are made by the arena builder (ArenaSounds) into a
    /// Resources/Sfx folder. Volume follows Settings > SFX volume; a small random pitch change keeps
    /// repeated sounds from sounding robotic.
    /// </summary>
    public static class Sfx
    {
        const int Voices = 12;
        /// <summary>The same sound again within this many seconds is skipped (a burst of coins, a skill hitting ten bugs).</summary>
        const float MinGap = 0.03f;

        static AudioSource[] sources;
        static int next;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            sources = null;
            next = 0;
            clips.Clear();
            lastPlayed.Clear();
        }

        public static void Play(string name, float volume = 1f, float pitch = 1f, float pitchJitter = 0.06f)
        {
            if (!Application.isPlaying) return;
            var clip = Clip(name);
            if (clip == null) return;
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(name, out var last) && now - last < MinGap) return;
            lastPlayed[name] = now;

            var source = NextSource();
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume) * GameSettings.SfxVolume;
            source.pitch = pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
            source.Play();
        }

        static AudioClip Clip(string name)
        {
            if (clips.TryGetValue(name, out var clip)) return clip;
            clip = Resources.Load<AudioClip>("Sfx/" + name);
            if (clip == null) Debug.LogWarning($"[Sfx] No sound called \"{name}\" (Tools > Pixel Study > Build Arena makes them).");
            clips[name] = clip;
            return clip;
        }

        /// <summary>A free voice, or the one that started longest ago.</summary>
        static AudioSource NextSource()
        {
            if (sources == null || sources[0] == null)
            {
                var go = new GameObject("Sfx");
                Object.DontDestroyOnLoad(go);
                sources = new AudioSource[Voices];
                for (int i = 0; i < Voices; i++)
                {
                    sources[i] = go.AddComponent<AudioSource>();
                    sources[i].playOnAwake = false;
                }
            }
            for (int i = 0; i < Voices; i++)
            {
                var s = sources[(next + i) % Voices];
                if (s.isPlaying) continue;
                next = (next + i + 1) % Voices;
                return s;
            }
            var oldest = sources[next];
            next = (next + 1) % Voices;
            return oldest;
        }
    }
}
