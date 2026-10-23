using System;

namespace PixelStudy
{
    /// <summary>
    /// The upgrades offered after a floor. How strong they are depends on the quiz:
    ///   1/3 correct = Bronze, 2/3 = Silver, 3/3 = Gold (0/3 = no upgrade).
    /// An upgrade token can boost the tier by one.
    /// </summary>
    public static class Upgrades
    {
        public const int MaxTier = 3;

        public class Upgrade
        {
            public string Name;
            public Func<int, string> Describe;
            public Action<RunData, int> Apply;
            public Func<RunData, string> Current;
        }

        static readonly int[] AttackPercent = { 0, 4, 7, 10 };
        static readonly int[] MaxHealth = { 0, 5, 10, 15 };
        static readonly int[] DodgePercent = { 0, 3, 5, 8 };

        public static readonly Upgrade[] All =
        {
            new Upgrade
            {
                Name = "Sharper Blade",
                Describe = tier => $"+{AttackPercent[Clamp(tier)]}% attack damage",
                Apply = (run, tier) => run.attackBonusPercent += AttackPercent[Clamp(tier)],
                Current = run => $"Attack bonus: +{run.attackBonusPercent}%",
            },
            new Upgrade
            {
                Name = "Reinforced Armor",
                Describe = tier => $"+{MaxHealth[Clamp(tier)]} max health",
                Apply = (run, tier) => { run.maxHealth += MaxHealth[Clamp(tier)]; run.health += MaxHealth[Clamp(tier)]; },
                Current = run => $"Max health: {run.maxHealth}",
            },
            new Upgrade
            {
                Name = "Swift Boots",
                Describe = tier => $"+{DodgePercent[Clamp(tier)]}% dodge distance",
                Apply = (run, tier) => run.dodgeBonusPercent += DodgePercent[Clamp(tier)],
                Current = run => $"Dodge bonus: +{run.dodgeBonusPercent}%",
            },
        };

        /// <summary>Quiz score to upgrade tier (scaled if a quiz ever has more or fewer than 3 questions).</summary>
        public static int TierFor(int correct, int total)
        {
            if (total <= 0 || correct <= 0) return 0;
            return Clamp((int)Math.Round(3.0 * correct / total));
        }

        public static string TierName(int tier) => tier switch
        {
            1 => "Bronze",
            2 => "Silver",
            3 => "Gold",
            _ => "None",
        };

        public static string TierColor(int tier) => tier switch
        {
            1 => "#D99A5B",
            2 => "#CFD8E6",
            3 => "#F2C14E",
            _ => "#A597C4",
        };

        static int Clamp(int tier) => Math.Max(0, Math.Min(MaxTier, tier));
    }
}
