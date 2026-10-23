using System;
using UnityEngine;

namespace PixelStudy
{
    /// <summary>
    /// Player settings from the Settings screen. Audio code should read MusicVolume / SfxVolume
    /// (0..1) and listen to <see cref="Changed"/>.
    /// </summary>
    public static class GameSettings
    {
        const string MusicKey = "PixelStudy.MusicVolume";
        const string SfxKey = "PixelStudy.SfxVolume";

        public static event Action Changed;

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(MusicKey, 0.7f);
            set { PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value)); Changed?.Invoke(); }
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SfxKey, 0.85f);
            set { PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value)); Changed?.Invoke(); }
        }

        public static bool Fullscreen
        {
            get => Screen.fullScreen;
            set { Screen.fullScreen = value; Changed?.Invoke(); }
        }
    }
}
