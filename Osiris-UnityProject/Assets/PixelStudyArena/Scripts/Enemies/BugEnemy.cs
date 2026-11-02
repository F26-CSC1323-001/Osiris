using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Mockup 2 bug: placed in the level by the builder and just idles there. It doesn't move,
    /// attack or take damage (there's no combat in this mockup); the knight walks straight past it.
    /// </summary>
    public abstract class BugEnemy : MonoBehaviour
    {
        public Transform visual;
        public SpriteRenderer body;
        public PixelAnimator anim;
        [Tooltip("Clip played on loop.")]
        public string idleClip = "idle";
        [Tooltip("1 faces right, -1 faces left.")]
        public int facing = -1;

        protected virtual void Start()
        {
            if (anim != null) anim.Play(idleClip);
            if (visual != null) visual.localScale = new Vector3(facing >= 0 ? 1f : -1f, 1f, 1f);
        }
    }
}
