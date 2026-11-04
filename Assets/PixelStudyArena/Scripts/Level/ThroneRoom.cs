using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace PixelStudy.Arena
{
    /// <summary>
    /// The final arena, the throne room at the top of the tower. When the Parasite tears out of the
    /// Infested Knight the room crumbles: the ceiling falls in, the back wall and pillars collapse and
    /// the fight carries on under an open storm sky.
    /// </summary>
    public class ThroneRoom : MonoBehaviour
    {
        public Tilemap ground;
        public Tilemap backWall;
        public Tilemap pillars;
        [Tooltip("Ceiling rows to bring down (cell y, inclusive).")]
        public int ceilingFrom = 20, ceilingTo = 23;
        public int width = 56;
        [Tooltip("Banners, windows, the throne... toppled and faded out.")]
        public Transform[] fallingDecor;
        public SpriteRenderer stormSky;

        static ThroneRoom instance;

        bool crumbled;
        float lightningTimer;

        static readonly Color Stone = new Color(0.55f, 0.55f, 0.6f);

        void Awake() => instance = this;

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void Start()
        {
            if (stormSky != null) stormSky.color = new Color(1f, 1f, 1f, 0f);
        }

        /// <summary>Brings the room down (the Infested Knight calls it when the Parasite emerges).</summary>
        public static void CrumbleNow()
        {
            if (instance != null && !instance.crumbled) instance.StartCoroutine(instance.Crumble());
        }

        IEnumerator Crumble()
        {
            crumbled = true;
            ArenaFX.Shake(1f);

            // the ceiling caves in, from the middle outward
            var cells = new List<Vector3Int>();
            for (int y = ceilingFrom; y <= ceilingTo; y++)
            for (int x = 2; x < width - 2; x++)
                cells.Add(new Vector3Int(x, y, 0));
            int mid = width / 2;
            cells.Sort((a, b) => Mathf.Abs(a.x - mid).CompareTo(Mathf.Abs(b.x - mid)));

            var decor = new List<(Transform t, Vector3 start, float spin, SpriteRenderer sr)>();
            if (fallingDecor != null)
                foreach (var t in fallingDecor)
                    if (t != null)
                    {
                        foreach (var d in t.GetComponentsInChildren<Drift>()) d.enabled = false;
                        decor.Add((t, t.position, Random.Range(-90f, 90f), t.GetComponent<SpriteRenderer>()));
                    }

            const float duration = 1.6f;
            int removed = 0;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                int target = Mathf.RoundToInt(cells.Count * Mathf.Clamp01(k * 1.4f));
                for (; removed < target; removed++)
                {
                    var c = cells[removed];
                    if (ground != null) ground.SetTile(c, null);
                    if (removed % 3 == 0) ArenaFX.Burst(new Vector2(c.x + 0.5f, c.y + 0.5f), Stone, 4, 5f, 0.16f, 0.9f, 0f);
                }
                if (backWall != null) backWall.color = new Color(backWall.color.r, backWall.color.g, backWall.color.b, 1f - k);
                if (pillars != null) pillars.color = new Color(pillars.color.r, pillars.color.g, pillars.color.b, 1f - k);
                if (stormSky != null) stormSky.color = new Color(1f, 1f, 1f, k);
                foreach (var (tr, start, spin, sr) in decor)
                {
                    tr.position = start + Vector3.down * (k * k * 8f);
                    tr.rotation = Quaternion.Euler(0f, 0f, spin * k);
                    if (sr != null) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f - k);
                }
                if (Random.value < 0.2f) ArenaFX.Shake(0.3f);
                yield return null;
            }
            for (; removed < cells.Count; removed++)
                if (ground != null) ground.SetTile(cells[removed], null);
            if (backWall != null) backWall.gameObject.SetActive(false);
            if (pillars != null) pillars.gameObject.SetActive(false);
            foreach (var (tr, _, _, _) in decor) tr.gameObject.SetActive(false);
            lightningTimer = 1.5f;
        }

        void Update()
        {
            if (!crumbled || Time.timeScale < 0.01f) return;
            // lightning in the storm overhead
            if ((lightningTimer -= Time.deltaTime) > 0f) return;
            lightningTimer = Random.Range(2.5f, 5.5f);
            ArenaFX.ScreenFlash(new Color(0.75f, 0.6f, 1f, 0.22f), 0.12f);
        }
    }
}
