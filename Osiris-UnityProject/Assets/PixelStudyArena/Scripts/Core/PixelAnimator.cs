using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelStudy.Arena
{
    [Serializable]
    public class PixelClip
    {
        public string name;
        public Sprite[] frames;
        public float fps = 12f;
        public bool loop = true;
    }

    /// <summary>
    /// Plays sprite-frame clips by name. Simpler than an Animator for combat, because code always
    /// knows exactly which frame is showing (attack hitboxes turn on during specific frames).
    /// </summary>
    public class PixelAnimator : MonoBehaviour
    {
        public SpriteRenderer target;
        public PixelClip[] clips;
        public float speed = 1f;

        Dictionary<string, PixelClip> map;
        PixelClip current;
        float time;

        public string Current => current?.name;
        public int Frame { get; private set; }
        public int FrameCount => current?.frames.Length ?? 0;
        public bool IsDone => current != null && !current.loop && time * current.fps >= current.frames.Length;

        void Awake() => BuildMap();

        void BuildMap()
        {
            if (map != null) return;
            map = new Dictionary<string, PixelClip>();
            if (clips == null) return;
            foreach (var c in clips)
                if (c != null && !string.IsNullOrEmpty(c.name) && c.frames != null && c.frames.Length > 0)
                    map[c.name] = c;
        }

        public bool Has(string clip)
        {
            BuildMap();
            return map.ContainsKey(clip);
        }

        public float Duration(string clip)
        {
            BuildMap();
            return map.TryGetValue(clip, out var c) ? c.frames.Length / Mathf.Max(0.01f, c.fps) : 0f;
        }

        public void Play(string clip, bool restart = false)
        {
            BuildMap();
            if (!restart && current != null && current.name == clip) return;
            if (!map.TryGetValue(clip, out var c)) return;
            current = c;
            time = 0f;
            Apply();
        }

        void Update()
        {
            if (current == null) return;
            time += Time.deltaTime * speed;
            Apply();
        }

        void Apply()
        {
            if (current == null || target == null) return;
            int count = current.frames.Length;
            int f = (int)(time * current.fps);
            f = current.loop ? f % count : Mathf.Min(f, count - 1);
            Frame = f;
            target.sprite = current.frames[f];
        }
    }
}
