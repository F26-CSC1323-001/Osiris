using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>Smooth follow with look-ahead, arena bounds and trauma-based screen shake.</summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Camera))]
    public class CameraFollow2D : MonoBehaviour
    {
        public ArenaPlayer player;
        public Vector2 offset = new Vector2(0f, 1.6f);
        public float lookAhead = 2.2f;
        public float smoothTime = 0.14f;
        public Rect bounds = new Rect(0f, 0f, 64f, 26f);
        public float maxShake = 0.45f;

        static CameraFollow2D instance;

        Camera cam;
        Vector3 velocity;
        Vector3 basePosition;
        float look;
        float trauma;
        float baseSize, zoom = 1f, zoomTarget = 1f, zoomHold;

        public static void Shake(float amount)
        {
            if (instance != null) instance.trauma = Mathf.Clamp01(instance.trauma + amount);
        }

        /// <summary>Punches the view in (factor below 1) for a moment, then eases back (criticals, big skills).</summary>
        public static void Zoom(float factor, float hold)
        {
            if (instance == null) return;
            instance.zoomTarget = factor;
            instance.zoomHold = hold;
        }

        void Awake()
        {
            instance = this;
            cam = GetComponent<Camera>();
            baseSize = cam.orthographicSize;
        }

        void Start()
        {
            if (player != null) basePosition = Clamp(Desired());
            else basePosition = transform.position;
            transform.position = basePosition;
        }

        Vector3 Desired()
        {
            var p = (Vector2)player.transform.position + offset + new Vector2(look, 0f);
            return new Vector3(p.x, p.y, transform.position.z);
        }

        Vector3 Clamp(Vector3 p)
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            p.x = bounds.width > halfW * 2f ? Mathf.Clamp(p.x, bounds.xMin + halfW, bounds.xMax - halfW) : bounds.center.x;
            p.y = bounds.height > halfH * 2f ? Mathf.Clamp(p.y, bounds.yMin + halfH, bounds.yMax - halfH) : bounds.center.y;
            return p;
        }

        void LateUpdate()
        {
            // real time: the camera keeps up with the knight while a skill slows the world down
            float dt = Time.timeScale == 0f ? 0f : Time.unscaledDeltaTime;
            zoomHold -= Time.unscaledDeltaTime;
            float wantZoom = zoomHold > 0f ? zoomTarget : 1f;
            zoom = Mathf.Lerp(zoom, wantZoom, 1f - Mathf.Exp(-(zoomHold > 0f ? 22f : 6f) * Time.unscaledDeltaTime));
            cam.orthographicSize = baseSize * zoom;

            if (player != null && dt > 0f)
            {
                look = Mathf.Lerp(look, player.Facing * lookAhead, dt * 2.5f);
                basePosition = Vector3.SmoothDamp(basePosition, Clamp(Desired()), ref velocity, smoothTime, Mathf.Infinity, dt);
            }

            trauma = Mathf.MoveTowards(trauma, 0f, Time.unscaledDeltaTime * 1.6f);
            float s = trauma * trauma * maxShake;
            float t = Time.unscaledTime * 30f;
            var shake = new Vector3((Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * s, (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * s, 0f);
            transform.position = basePosition + shake;
            transform.rotation = Quaternion.Euler(0f, 0f, (Mathf.PerlinNoise(t, t) - 0.5f) * 3f * trauma * trauma);
        }
    }
}
