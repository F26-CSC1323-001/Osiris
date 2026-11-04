using UnityEngine;

namespace PixelStudy.Arena
{
    /// <summary>
    /// Anime impact frames on the main camera: for a few frames the whole picture flips to flat
    /// ink-and-paper silhouettes with speed lines bursting from the hit, then snaps back.
    /// Call ImpactFrame.Play(worldPoint) on a big hit (Flame Slash, criticals).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ImpactFrame : MonoBehaviour
    {
        public Material material;

        public enum Style { Fire, Steel, Lightning, Gold, Void, Jade }

        struct Step
        {
            public float time;
            public bool invert;
            public Color ink, paper;
            public float threshold;
        }

        static ImpactFrame instance;

        static readonly int InkId = Shader.PropertyToID("_Ink");
        static readonly int PaperId = Shader.PropertyToID("_Paper");
        static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
        static readonly int InvertId = Shader.PropertyToID("_Invert");
        static readonly int FocusId = Shader.PropertyToID("_Focus");
        static readonly int SeedId = Shader.PropertyToID("_Seed");
        static readonly int LinesId = Shader.PropertyToID("_Lines");

        Camera cam;
        Step[] steps;
        int step = -1;
        float stepTimer;
        Vector3 focusWorld;

        /// <summary>True while an impact frame is on screen.</summary>
        public static bool Active => instance != null && instance.step >= 0;

        void Awake()
        {
            instance = this;
            cam = GetComponent<Camera>();
            if (material != null) material = new Material(material); // a copy, so play mode doesn't edit the asset
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static void Play(Vector2 worldFocus, Style style = Style.Fire)
        {
            if (instance == null || instance.material == null) return;
            instance.Begin(worldFocus, style);
        }

        void Begin(Vector2 worldFocus, Style style)
        {
            focusWorld = worldFocus;
            if (style == Style.Fire)
            {
                steps = new[]
                {
                    // black silhouettes on white, then white-hot shapes on blood-red ink, then a last flicker
                    new Step { time = 0.05f, invert = true, ink = new Color(0.07f, 0.02f, 0.02f), paper = new Color(1f, 0.97f, 0.9f), threshold = 0.3f },
                    new Step { time = 0.05f, invert = false, ink = new Color(0.32f, 0.03f, 0.02f), paper = new Color(1f, 0.82f, 0.45f), threshold = 0.3f },
                    new Step { time = 0.03f, invert = true, ink = new Color(0.05f, 0.02f, 0.02f), paper = new Color(1f, 0.6f, 0.25f), threshold = 0.34f },
                };
            }
            else if (style == Style.Lightning)
            {
                steps = new[]
                {
                    // electric: white-blue flash, then navy ink with cyan shapes, then one more flicker
                    new Step { time = 0.04f, invert = true, ink = new Color(0.02f, 0.04f, 0.12f), paper = new Color(0.9f, 0.98f, 1f), threshold = 0.3f },
                    new Step { time = 0.05f, invert = false, ink = new Color(0.02f, 0.05f, 0.16f), paper = new Color(0.55f, 0.9f, 1f), threshold = 0.3f },
                    new Step { time = 0.03f, invert = true, ink = new Color(0.02f, 0.04f, 0.12f), paper = Color.white, threshold = 0.36f },
                };
            }
            else if (style == Style.Void)
            {
                steps = new[]
                {
                    // the corruption: black ink on bruised violet, then violet shapes on black
                    new Step { time = 0.05f, invert = true, ink = new Color(0.04f, 0.01f, 0.06f), paper = new Color(0.86f, 0.62f, 1f), threshold = 0.3f },
                    new Step { time = 0.05f, invert = false, ink = new Color(0.03f, 0.01f, 0.05f), paper = new Color(0.75f, 0.35f, 1f), threshold = 0.3f },
                    new Step { time = 0.03f, invert = true, ink = new Color(0.08f, 0.01f, 0.06f), paper = new Color(1f, 0.4f, 0.6f), threshold = 0.36f },
                };
            }
            else if (style == Style.Jade)
            {
                steps = new[]
                {
                    // the Reaper Mantis: black ink on moonlit jade, then pale shapes on deep green
                    new Step { time = 0.05f, invert = true, ink = new Color(0.02f, 0.06f, 0.03f), paper = new Color(0.85f, 1f, 0.88f), threshold = 0.3f },
                    new Step { time = 0.05f, invert = false, ink = new Color(0.02f, 0.1f, 0.05f), paper = new Color(0.55f, 1f, 0.65f), threshold = 0.3f },
                    new Step { time = 0.03f, invert = true, ink = new Color(0.1f, 0.02f, 0.02f), paper = new Color(1f, 0.85f, 0.85f), threshold = 0.36f },
                };
            }
            else if (style == Style.Gold)
            {
                steps = new[]
                {
                    new Step { time = 0.045f, invert = true, ink = new Color(0.1f, 0.06f, 0.01f), paper = new Color(1f, 0.95f, 0.78f), threshold = 0.3f },
                    new Step { time = 0.05f, invert = false, ink = new Color(0.14f, 0.08f, 0.02f), paper = new Color(1f, 0.85f, 0.4f), threshold = 0.3f },
                };
            }
            else
            {
                steps = new[]
                {
                    new Step { time = 0.045f, invert = true, ink = new Color(0.04f, 0.04f, 0.06f), paper = Color.white, threshold = 0.3f },
                    new Step { time = 0.04f, invert = false, ink = new Color(0.04f, 0.04f, 0.06f), paper = new Color(1f, 0.95f, 0.8f), threshold = 0.3f },
                };
            }
            step = 0;
            stepTimer = steps[0].time;
            material.SetFloat(SeedId, Random.Range(0f, 100f));
        }

        void LateUpdate()
        {
            if (step < 0) return;
            stepTimer -= Time.unscaledDeltaTime;
            if (stepTimer > 0f) return;
            step++;
            if (step >= steps.Length) { step = -1; return; }
            stepTimer = steps[step].time;
            material.SetFloat(SeedId, Random.Range(0f, 100f)); // the speed lines change every frame
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (step < 0 || material == null)
            {
                Graphics.Blit(src, dst);
                return;
            }
            var s = steps[step];
            var vp = cam.WorldToViewportPoint(focusWorld);
            material.SetColor(InkId, s.ink);
            material.SetColor(PaperId, s.paper);
            material.SetFloat(ThresholdId, s.threshold);
            material.SetFloat(InvertId, s.invert ? 1f : 0f);
            material.SetFloat(LinesId, 1f);
            material.SetVector(FocusId, new Vector4(vp.x, vp.y, 0f, 0f));
            Graphics.Blit(src, dst, material);
        }
    }
}
