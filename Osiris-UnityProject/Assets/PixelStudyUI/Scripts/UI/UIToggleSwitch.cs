using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelStudy.UI
{
    /// <summary>Slides the knob of an on/off switch and recolors its track.</summary>
    [RequireComponent(typeof(Toggle))]
    public class UIToggleSwitch : MonoBehaviour
    {
        public RectTransform knob;
        public Image track;
        public TMP_Text stateLabel;
        public Color onColor = new Color(0.42f, 0.82f, 0.42f);
        public Color offColor = new Color(0.25f, 0.21f, 0.33f);
        public float knobTravel = 26f;

        Toggle toggle;
        float t;

        void Awake()
        {
            toggle = GetComponent<Toggle>();
            t = toggle.isOn ? 1f : 0f;
            Apply();
        }

        void Update()
        {
            t = Mathf.MoveTowards(t, toggle.isOn ? 1f : 0f, Time.unscaledDeltaTime * 7f);
            Apply();
        }

        void Apply()
        {
            float e = t * t * (3f - 2f * t);
            if (knob != null) knob.anchoredPosition = new Vector2(Mathf.Lerp(-knobTravel, knobTravel, e), knob.anchoredPosition.y);
            if (track != null) track.color = Color.Lerp(offColor, onColor, e);
            if (stateLabel != null) stateLabel.text = toggle.isOn ? "ON" : "OFF";
        }
    }
}
