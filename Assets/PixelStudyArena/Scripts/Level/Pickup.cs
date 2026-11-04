using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>Coins and hearts dropped by bugs. They pop out, bounce, then fly to the player.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class Pickup : MonoBehaviour
    {
        public enum Kind { Coin, Heart }

        /// <summary>Set when the arena is cleared: every pickup flies to the player.</summary>
        public static bool MagnetAll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => MagnetAll = false;

        public Kind kind;
        public int amount = 1;
        public float magnetDelay = 0.5f;
        public float magnetRange = 7f;

        Rigidbody2D rb;
        float age, speed;
        bool magnet;

        void Awake() => rb = GetComponent<Rigidbody2D>();

        public void Pop(Vector2 velocity) => rb.linearVelocity = velocity;

        void Update()
        {
            if (Time.timeScale < 0.01f) return;
            age += Time.deltaTime;
            var p = ArenaPlayer.Instance;
            if (p == null || p.IsDead) return;

            var to = p.Center - (Vector2)transform.position;
            if (!magnet && age > magnetDelay && (MagnetAll || to.magnitude < magnetRange))
            {
                magnet = true;
                rb.simulated = false;
            }
            if (!magnet) return;

            speed = Mathf.Min(speed + 40f * Time.deltaTime, 22f);
            transform.position += (Vector3)(to.normalized * speed * Time.deltaTime);
            if (to.magnitude < 0.45f) Collect(p);
        }

        void Collect(ArenaPlayer p)
        {
            if (kind == Kind.Coin)
            {
                GameSession.AddCoins(amount);
                Sfx.Play("coin", 0.4f);
                ArenaFX.Burst(transform.position, new Color(1f, 0.85f, 0.3f), 4, 2f, 0.08f, 0.25f, 0.5f);
            }
            else
            {
                GameSession.Heal(amount);
                Sfx.Play("heart", 0.6f, 1f, 0f);
                ArenaFX.DamageNumber(p.Center + Vector2.up * 0.8f, "+" + amount, new Color(0.45f, 1f, 0.45f));
                ArenaFX.Ring(p.Center, new Color(0.45f, 1f, 0.45f, 0.8f), 0.5f, 2.5f, 0.3f);
            }
            Destroy(gameObject);
        }
    }
}
