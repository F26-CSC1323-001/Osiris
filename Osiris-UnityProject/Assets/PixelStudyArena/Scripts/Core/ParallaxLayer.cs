using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Moves a background layer with the camera. factor 1 = glued to the camera (very far away),
    /// 0 = part of the level, above 1 = foreground that slides past faster.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class ParallaxLayer : MonoBehaviour
    {
        public Vector2 factor = new Vector2(0.5f, 0.3f);
        [Tooltip("Camera position where this layer sits at its placed position (the arena center).")]
        public Vector2 anchor;

        Transform cam;
        Vector3 basePosition;

        void Start()
        {
            cam = Camera.main != null ? Camera.main.transform : null;
            basePosition = transform.position;
        }

        void LateUpdate()
        {
            if (cam == null) return;
            var delta = (Vector2)cam.position - anchor;
            transform.position = basePosition + new Vector3(delta.x * factor.x, delta.y * factor.y, 0f);
        }
    }
}
