using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PixelStudy.UI
{
    /// <summary>
    /// One full screen or popup. Show() fades it in (and pops <see cref="popTarget"/>),
    /// Hide() fades it out and deactivates it. Uses unscaled time so it works while paused.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIScreen : MonoBehaviour
    {
        [Tooltip("Gets keyboard focus when the player presses an arrow key or Tab.")]
        public Selectable firstSelected;
        [Tooltip("Optional panel that pops in when the screen opens.")]
        public RectTransform popTarget;
        public float fadeDuration = 0.18f;

        CanvasGroup group;
        Coroutine routine;
        bool hiding;

        public bool IsOpen => gameObject.activeSelf && !hiding;

        CanvasGroup Group => group != null ? group : group = GetComponent<CanvasGroup>();

        public void Show()
        {
            hiding = false;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Fade(0f, 1f, 0.94f, 1f, false));
        }

        public void Hide()
        {
            if (!gameObject.activeSelf || hiding) return;
            hiding = true;
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null && es.currentSelectedGameObject.transform.IsChildOf(transform))
                es.SetSelectedGameObject(null);
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Fade(Group.alpha, 0f, 1f, 0.97f, true));
        }

        public void HideImmediate()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            hiding = false;
            Group.alpha = 0f;
            gameObject.SetActive(false);
        }

        IEnumerator Fade(float fromAlpha, float toAlpha, float fromScale, float toScale, bool deactivate)
        {
            var g = Group;
            g.interactable = !deactivate;
            g.blocksRaycasts = !deactivate;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeDuration);
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
                g.alpha = Mathf.Lerp(fromAlpha, toAlpha, e);
                if (popTarget != null) popTarget.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, e);
                yield return null;
            }
            if (popTarget != null) popTarget.localScale = Vector3.one;
            routine = null;
            if (deactivate)
            {
                hiding = false;
                gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if (firstSelected == null || hiding) return;
            var es = EventSystem.current;
            if (es == null) return;
            var current = es.currentSelectedGameObject;
            if (current != null && current.activeInHierarchy) return;
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) ||
                Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow) ||
                Input.GetKeyDown(KeyCode.Tab))
            {
                es.SetSelectedGameObject(firstSelected.gameObject);
            }
        }
    }
}
