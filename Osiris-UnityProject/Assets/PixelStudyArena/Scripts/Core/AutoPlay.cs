using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>Starts a PixelAnimator clip when the scene loads (torch flames, ambient props).</summary>
    [RequireComponent(typeof(PixelAnimator))]
    public class AutoPlay : MonoBehaviour
    {
        public string clip;

        void Start() => GetComponent<PixelAnimator>().Play(clip);
    }
}
