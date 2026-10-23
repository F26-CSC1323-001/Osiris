using UnityEngine;
using UnityEngine.UI;

namespace PixelStudy.UI
{
    /// <summary>Plays a list of sprites on a UI Image (the knight idling on the main menu).</summary>
    [RequireComponent(typeof(Image))]
    public class UISpriteAnimator : MonoBehaviour
    {
        public Sprite[] frames;
        public float framesPerSecond = 6f;

        Image image;

        void Awake() => image = GetComponent<Image>();

        void Update()
        {
            if (frames == null || frames.Length == 0) return;
            int i = (int)(Time.unscaledTime * framesPerSecond) % frames.Length;
            image.sprite = frames[i];
        }
    }
}
