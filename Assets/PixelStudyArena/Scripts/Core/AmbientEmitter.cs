using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>Builds a simple looping particle system at runtime: torch embers, dust motes, portal sparks.</summary>
    public class AmbientEmitter : MonoBehaviour
    {
        public Material material;
        public Color colorA = new Color(1f, 0.7f, 0.3f, 1f);
        public Color colorB = new Color(1f, 0.4f, 0.15f, 1f);
        public Vector2 area = new Vector2(0.3f, 0.1f);
        public float rate = 6f;
        public Vector2 lifetime = new Vector2(0.8f, 1.6f);
        public Vector2 size = new Vector2(0.04f, 0.09f);
        public Vector2 velocity = new Vector2(0f, 1.2f);
        public float velocityRandom = 0.4f;
        public float noise = 0.6f;
        public int sortingOrder = 15;
        public bool fadeIn;

        void Start()
        {
            var ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            main.prewarm = true;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(area.x, area.y, 0.01f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(velocity.x - velocityRandom, velocity.x + velocityRandom);
            vel.y = new ParticleSystem.MinMaxCurve(velocity.y - velocityRandom, velocity.y + velocityRandom);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            if (noise > 0f)
            {
                var n = ps.noise;
                n.enabled = true;
                n.strength = noise;
                n.frequency = 0.6f;
                n.scrollSpeed = 0.4f;
            }

            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(fadeIn ? 0f : 1f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            colorOverLife.color = g;

            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.material = material;
            renderer.sortingOrder = sortingOrder;

            ps.Play();
        }
    }
}
