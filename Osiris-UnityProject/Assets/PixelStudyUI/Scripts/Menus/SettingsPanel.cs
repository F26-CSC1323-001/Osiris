using System;
using PixelStudy.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelStudy.Menus
{
    /// <summary>Settings screen (+ the Controls / key bindings screen). Used by the main menu and the pause menu.</summary>
    public class SettingsPanel : MonoBehaviour
    {
        public UIScreen screen;
        public UIScreen keyBindingsScreen;
        public Slider musicSlider;
        public Slider sfxSlider;
        public TMP_Text musicValue;
        public TMP_Text sfxValue;
        public Toggle fullscreenToggle;
        public Button keyBindingsButton;
        public Button backButton;
        public Button keyBindingsBackButton;

        Action onClosed;

        public bool IsOpen => screen.IsOpen || (keyBindingsScreen != null && keyBindingsScreen.IsOpen);

        void Awake()
        {
            musicSlider.onValueChanged.AddListener(v => { GameSettings.MusicVolume = v; musicValue.text = Percent(v); });
            sfxSlider.onValueChanged.AddListener(v => { GameSettings.SfxVolume = v; sfxValue.text = Percent(v); });
            fullscreenToggle.onValueChanged.AddListener(on => GameSettings.Fullscreen = on);
            keyBindingsButton.onClick.AddListener(() => { screen.Hide(); keyBindingsScreen.Show(); });
            keyBindingsBackButton.onClick.AddListener(() => { keyBindingsScreen.Hide(); screen.Show(); });
            backButton.onClick.AddListener(Close);
        }

        public void Open(Action closed)
        {
            onClosed = closed;
            musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
            sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
            musicValue.text = Percent(GameSettings.MusicVolume);
            sfxValue.text = Percent(GameSettings.SfxVolume);
            fullscreenToggle.SetIsOnWithoutNotify(GameSettings.Fullscreen);
            screen.Show();
        }

        public void Close()
        {
            PlayerPrefs.Save();
            if (keyBindingsScreen != null && keyBindingsScreen.IsOpen) keyBindingsScreen.Hide();
            screen.Hide();
            var callback = onClosed;
            onClosed = null;
            callback?.Invoke();
        }

        /// <summary>Escape key: steps back one level. Returns false if settings weren't open.</summary>
        public bool HandleBack()
        {
            if (!IsOpen) return false;
            if (keyBindingsScreen != null && keyBindingsScreen.IsOpen)
            {
                keyBindingsScreen.Hide();
                screen.Show();
            }
            else
            {
                Close();
            }
            return true;
        }

        static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";
    }
}
