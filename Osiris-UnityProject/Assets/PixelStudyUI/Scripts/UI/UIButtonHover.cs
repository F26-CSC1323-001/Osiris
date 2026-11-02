using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PixelStudy.UI
{
    /// <summary>
    /// Hover / press animation for buttons, cards and sliders: grows a little, brightens,
    /// slides in an optional cursor arrow and lifts an optional child. Works with the mouse and
    /// with keyboard/gamepad selection, and runs on unscaled time so it still animates while the
    /// game is paused.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIButtonHover : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        [Header("Scale")]
        [Tooltip("What grows on hover. Defaults to this object.")]
        public RectTransform scaleTarget;
        public float hoverScale = 1.05f;
        public float pressScale = 0.95f;

        [Header("Colors")]
        public Graphic background;
        public Color normalColor = Color.white;
        public Color hoverColor = Color.white;
        public Color pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        public Color disabledColor = new Color(0.35f, 0.33f, 0.4f, 1f);
        public Graphic label;
        [Range(0f, 1f)] public float disabledLabelAlpha = 0.45f;

        [Header("Extras (optional)")]
        [Tooltip("A cursor arrow that slides in from the left on hover. Needs a CanvasGroup.")]
        public RectTransform indicator;
        public float indicatorSlide = 14f;
        [Tooltip("A child that moves up on hover (cards).")]
        public RectTransform lift;
        public float liftAmount = 10f;
        [Tooltip("A graphic that fades in on hover (outline glow).")]
        public Graphic glow;
        [Range(0f, 1f)] public float glowAlpha = 0.6f;

        [Header("Feel")]
        public float speed = 16f;

        Selectable selectable;
        CanvasGroup indicatorGroup;
        Vector3 baseScale = Vector3.one;
        Vector2 indicatorBase, liftBase;
        Color labelBase;
        bool hovered, pressed, selected, initialized;
        float hoverT, scale = 1f;
        Color? overrideColor;

        bool Interactable => selectable == null || selectable.IsInteractable();

        void Awake() => Init();

        void Init()
        {
            if (initialized) return;
            initialized = true;
            selectable = GetComponent<Selectable>();
            if (scaleTarget == null) scaleTarget = (RectTransform)transform;
            baseScale = scaleTarget.localScale;
            if (indicator != null)
            {
                indicatorBase = indicator.anchoredPosition;
                indicatorGroup = indicator.GetComponent<CanvasGroup>();
            }
            if (lift != null) liftBase = lift.anchoredPosition;
            if (label != null) labelBase = label.color;
        }

        void OnEnable()
        {
            Init();
            hovered = pressed = selected = false;
            hoverT = 0f;
            scale = 1f;
            Apply();
        }

        void OnDisable()
        {
            hoverT = 0f;
            scale = 1f;
            Apply();
        }

        /// <summary>Forces a background color (e.g. green/red quiz answers) until cleared.</summary>
        public void SetOverrideColor(Color color) => overrideColor = color;

        public void ClearOverrideColor() => overrideColor = null;

        void Update()
        {
            float k = 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);
            bool interactable = Interactable;
            bool active = interactable && (hovered || selected);
            hoverT = Mathf.Lerp(hoverT, active ? 1f : 0f, k);
            float targetScale = !interactable ? 1f : pressed ? pressScale : active ? hoverScale : 1f;
            scale = Mathf.Lerp(scale, targetScale, k);
            Apply();
        }

        void Apply()
        {
            bool interactable = Interactable;
            if (scaleTarget != null) scaleTarget.localScale = baseScale * scale;

            if (background != null)
            {
                Color c;
                if (overrideColor.HasValue) c = overrideColor.Value;
                else if (!interactable) c = disabledColor;
                else if (pressed) c = pressedColor;
                else c = Color.Lerp(normalColor, hoverColor, hoverT);
                background.color = c;
            }

            if (label != null)
            {
                var c = labelBase;
                if (!interactable && !overrideColor.HasValue) c.a *= disabledLabelAlpha;
                label.color = c;
            }

            if (indicator != null)
            {
                float bob = Mathf.Sin(Time.unscaledTime * 9f) * 3f * hoverT;
                indicator.anchoredPosition = indicatorBase + new Vector2(-indicatorSlide * (1f - hoverT) + bob, 0f);
                if (indicatorGroup != null) indicatorGroup.alpha = hoverT;
            }

            if (lift != null) lift.anchoredPosition = liftBase + new Vector2(0f, liftAmount * hoverT);

            if (glow != null)
            {
                var g = glow.color;
                g.a = glowAlpha * hoverT;
                glow.color = g;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            // Only one thing highlighted at a time: the mouse takes over from keyboard focus.
            var es = EventSystem.current;
            if (es != null && !es.alreadySelecting && es.currentSelectedGameObject != null && es.currentSelectedGameObject != gameObject)
                es.SetSelectedGameObject(null);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            pressed = false;
            var es = EventSystem.current;
            if (es != null && !es.alreadySelecting && es.currentSelectedGameObject == gameObject)
                es.SetSelectedGameObject(null);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && Interactable) pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData) => pressed = false;

        public void OnSelect(BaseEventData eventData) => selected = true;

        public void OnDeselect(BaseEventData eventData) => selected = false;
    }
}
