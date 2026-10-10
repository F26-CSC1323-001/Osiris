using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PixelStudy.EditorTools
{
    /// <summary>
    /// When the editor starts on an empty "Untitled" scene (which happens after the builders run),
    /// pressing Play shows nothing. This opens the main menu instead, once per editor session.
    /// If you had a real scene open, nothing changes.
    /// </summary>
    [InitializeOnLoad]
    static class OpenStartSceneOnLoad
    {
        const string StartScene = "Assets/Scenes/MainMenu.unity";
        const string SessionKey = "PixelStudy.StartSceneChecked";

        static OpenStartSceneOnLoad()
        {
            if (Application.isBatchMode || SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            EditorApplication.delayCall += OpenIfUntitled;
        }

        static void OpenIfUntitled()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var active = SceneManager.GetActiveScene();
            bool emptyUntitled = SceneManager.sceneCount == 1 && string.IsNullOrEmpty(active.path) && !active.isDirty;
            if (!emptyUntitled || AssetDatabase.LoadAssetAtPath<SceneAsset>(StartScene) == null) return;
            EditorSceneManager.OpenScene(StartScene);
            Debug.Log("[PixelStudy] Opened the Main Menu scene. Press Play to start the game.");
        }
    }
}
