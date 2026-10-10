using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>Physics layers used by the arena (named in Project Settings > Tags and Layers by the builder).</summary>
    public static class ArenaLayers
    {
        public const int Ground = 8;
        public const int Platform = 9;
        public const int Player = 10;
        public const int Enemy = 11;
        public const int Pickup = 12;

        public const int GroundMask = (1 << Ground) | (1 << Platform);
        public const int SolidMask = 1 << Ground;
        public const int PlatformMask = 1 << Platform;
        public const int PlayerMask = 1 << Player;
        public const int EnemyMask = 1 << Enemy;

        /// <summary>Player and bugs pass through each other (like Dead Cells); only attacks hurt.</summary>
        public static void ConfigureCollisions()
        {
            Physics2D.IgnoreLayerCollision(Player, Enemy, true);
            Physics2D.IgnoreLayerCollision(Enemy, Enemy, true);
            Physics2D.IgnoreLayerCollision(Pickup, Player, true);
            Physics2D.IgnoreLayerCollision(Pickup, Enemy, true);
            Physics2D.IgnoreLayerCollision(Pickup, Pickup, true);
        }
    }
}
