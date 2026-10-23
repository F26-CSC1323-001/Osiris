using System.Linq;
using UnityEngine;

namespace PixelStudy
{
    /// <summary>
    /// The knight's skills. Mockup 2 has no combat, so there are none: nothing shows on the HUD and a
    /// perfect quiz never unlocks one. (The full game in Main_Dev lists them here.)
    /// </summary>
    public static class SkillCatalog
    {
        public class Entry
        {
            public string Id;
            public string Name;
            public string Key;
            public string Description;
            public bool StartsUnlocked;
        }

        /// <summary>Chance that a perfect (3/3) quiz unlocks a random locked skill.</summary>
        public const float PerfectUnlockChance = 0f;

        public static readonly Entry[] All = new Entry[0];

        public static Entry Get(string id) => All.FirstOrDefault(e => e.Id == id);

        public static bool IsUnlocked(RunData run, string id)
        {
            var e = Get(id);
            if (e == null) return false;
            return e.StartsUnlocked || (run != null && run.unlockedSkills != null && run.unlockedSkills.Contains(id));
        }

        /// <summary>Unlocks a random locked skill. Returns it, or null if everything is already unlocked.</summary>
        public static Entry UnlockRandom(RunData run)
        {
            if (run == null) return null;
            var locked = All.Where(e => !IsUnlocked(run, e.Id)).ToList();
            if (locked.Count == 0) return null;
            var pick = locked[Random.Range(0, locked.Count)];
            run.unlockedSkills.Add(pick.Id);
            return pick;
        }

        public static void UnlockAll(RunData run)
        {
            if (run == null) return;
            foreach (var e in All)
                if (!IsUnlocked(run, e.Id)) run.unlockedSkills.Add(e.Id);
        }
    }
}
