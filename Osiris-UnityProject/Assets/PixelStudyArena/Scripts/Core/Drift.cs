using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>Slow side-to-side drift for fog banks and hanging things.</summary>
    public class Drift : MonoBehaviour
    {
        public Vector2 amplitude = new Vector2(1.5f, 0.2f);
        public float speed = 0.15f;
        public float swayDegrees;

        Vector3 basePosition;
        float seed;

        void Start()
        {
            basePosition = transform.localPosition;
            seed = Random.value * 10f;
        }

        void Update()
        {
            float t = Time.time * speed + seed;
            transform.localPosition = basePosition + new Vector3(Mathf.Sin(t) * amplitude.x, Mathf.Sin(t * 1.7f) * amplitude.y, 0f);
            if (swayDegrees != 0f) transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2.3f) * swayDegrees);
        }
    }
}
