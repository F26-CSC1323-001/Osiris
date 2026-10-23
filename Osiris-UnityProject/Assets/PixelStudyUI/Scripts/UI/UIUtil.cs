using UnityEngine;
using UnityEngine.EventSystems;

namespace PixelStudy.UI
{
    public static class UIUtil
    {
        /// <summary>Buttons need an EventSystem in the scene; make one if nobody added it.</summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null || Object.FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }
}
