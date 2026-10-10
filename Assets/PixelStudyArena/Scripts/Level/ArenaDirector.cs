using PixelStudy.Game;
using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Mockup 2: one level on repeat. The bugs are already placed in the level, the exit door is open
    /// from the start, and walking into it finishes the floor (quiz, Floor Cleared), after which the
    /// same level loads again.
    /// </summary>
    public class ArenaDirector : MonoBehaviour
    {
        public static ArenaDirector Instance { get; private set; }

        public ArenaPlayer player;
        public ExitDoor exitDoor;

        bool exitTriggered;

        void Awake()
        {
            Instance = this;
            ArenaLayers.ConfigureCollisions();
            GameSession.EnsureRun();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            if (exitDoor != null) exitDoor.OpenInstantly();
            var ui = GameUIController.Instance;
            if (ui != null && ui.hud != null) ui.hud.SetObjective("EXIT OPEN  →");
        }

        void Update()
        {
            if (Time.timeScale < 0.01f || exitTriggered) return;
            if (exitDoor != null && exitDoor.IsOpen && player != null && exitDoor.Contains(player.transform.position))
            {
                exitTriggered = true;
                var ui = GameUIController.Instance;
                if (ui == null) return;
                if (ui.hud != null) ui.hud.SetObjective("");
                ui.CompleteFloor();
            }
        }
    }
}
