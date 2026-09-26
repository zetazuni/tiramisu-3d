using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Tiramisu
{
    public enum Season { Spring, Summer, Autumn, Winter }

    /// <summary>
    /// The four seasons. Each one changes the lawn, hedges, tree leaves and flowers, how high the sun climbs, how long the day is,
    /// the clouds and the haze, and lets something fall from the sky: pink petals in spring, nothing in summer (fireflies at night),
    /// golden leaves in autumn and snow in winter. When time is running a season lasts a few days, or press the season button.
    /// Everything eases over from one season to the next.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class SeasonCycle : MonoBehaviour
    {
        public static SeasonCycle Instance { get; private set; }

        public Season season = Season.Spring;
        public int daysPerSeason = 3;
        [Tooltip("soft round texture for the falling things")] public Texture2D soft;
        public Material petalMaterial, snowMaterial, fireflyMaterial;
        [Tooltip("three colours of falling leaf (each a leaf shaped sprite, HDRP unlit has no vertex colours)")] public Material[] leafMaterials;
        [Tooltip("two shades of blossom petal")] public Material[] petalMaterials;

        const string PrefKey = "tiramisu.season";

        /// <summary>One season's look, blended between neighbours.</summary>
        struct Look
        {
            public Color lawn, hedge, leaf, flower;
            public float leafOn;        // 0 bare branches, 1 full leaves
            public float snow;          // 0 no snow, 1 white world
            public float sunPeak, sunrise, sunset, sunPower, warm, cloud, haze;
        }

        static readonly Look[] Looks =
        {
            // spring: fresh light green, blossom, mild sun
            new Look { lawn = new Color(0.46f, 0.66f, 0.26f), hedge = new Color(0.24f, 0.44f, 0.16f), leaf = new Color(0.85f, 1.05f, 0.7f), flower = Color.white, leafOn = 1f, snow = 0f,
                       sunPeak = 58f, sunrise = 6f, sunset = 18f, sunPower = 0.9f, warm = 200f, cloud = 1.1f, haze = 1.0f },
            // summer: deep green, high hot sun, long days
            new Look { lawn = new Color(0.38f, 0.56f, 0.2f), hedge = new Color(0.16f, 0.34f, 0.12f), leaf = new Color(0.92f, 1f, 0.85f), flower = Color.white, leafOn = 1f, snow = 0f,
                       sunPeak = 74f, sunrise = 5.5f, sunset = 19f, sunPower = 1.05f, warm = 0f, cloud = 0.7f, haze = 0.9f },
            // autumn: gold and rust, lower sun, warm light
            new Look { lawn = new Color(0.5f, 0.46f, 0.2f), hedge = new Color(0.42f, 0.32f, 0.12f), leaf = new Color(1.5f, 0.75f, 0.28f), flower = new Color(1f, 0.75f, 0.5f), leafOn = 1f, snow = 0f,
                       sunPeak = 46f, sunrise = 6.4f, sunset = 17.6f, sunPower = 0.95f, warm = -300f, cloud = 1.3f, haze = 1.05f },
            // winter: bare trees, snow, low cool sun, short days, thick clouds and haze
            new Look { lawn = new Color(2.4f, 2.5f, 2.7f), hedge = new Color(1.6f, 1.8f, 1.9f), leaf = new Color(0.7f, 0.8f, 0.7f), flower = new Color(0.9f, 0.95f, 1f), leafOn = 0f, snow = 1f,
                       sunPeak = 32f, sunrise = 7f, sunset = 17.2f, sunPower = 0.7f, warm = 900f, cloud = 2.0f, haze = 1.9f },
        };

        // ---- what the rest of the game reads (DayNightCycle) ----
        public static float SunPeak = 68f, Sunrise = 6f, Sunset = 18f, SunPower = 1f, Warm = 0f, CloudScale = 1f, HazeScale = 1f;

        Look now;
        readonly Dictionary<Material, Material> copies = new Dictionary<Material, Material>();
        readonly List<(Renderer r, string key)> lawns = new List<(Renderer, string)>();
        readonly List<Renderer> leaves = new List<Renderer>();
        readonly List<Renderer> flowers = new List<Renderer>();
        readonly List<Material> leafMats = new List<Material>(), flowerMats = new List<Material>();
        Material lawnM, lawnEdgeM, hedgeM;
        int lastDay = -1;
        ParticleSystem snow, fireflies;
        readonly List<ParticleSystem> petalSystems = new List<ParticleSystem>(), leafSystems = new List<ParticleSystem>();
        readonly List<Material> cityLeaf = new List<Material>();
        static ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[16000];
        static Mesh quad;
        float snowStrength, leafStrength, petalStrength, fireflyStrength;

        public string Label => season.ToString();
        public float DayInSeason { get; private set; }

        void Awake()
        {
            Instance = this;
            if (PlayerPrefs.HasKey(PrefKey)) season = (Season)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey), 0, 3);
        }

        void Start()
        {
            Collect();
            now = Looks[(int)season];
            ApplyLook(now, true);
            MakeParticles();
            SetWeather(season, true);
            var dn = DayNightCycle.Instance;
            if (dn) lastDay = dn.DayCount;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SunPeak = 68f; Sunrise = 6f; Sunset = 18f; SunPower = 1f; Warm = 0f; CloudScale = 1f; HazeScale = 1f;
        }

        // ------------------------------------------------------------ finding the things to recolour

        Material Copy(Material m)
        {
            if (!copies.TryGetValue(m, out var c)) { c = new Material(m) { name = m.name + " (season)" }; copies[m] = c; }
            return c;
        }

        void Collect()
        {
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            {
                var shared = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < shared.Length; i++)
                {
                    var m = shared[i];
                    if (!m) continue;
                    string n = m.name;
                    if (n == "Lawn" || n == "LawnEdge" || n == "Hedge" || n == "CityLeaf") { shared[i] = Copy(m); changed = true; }
                    else if (n.StartsWith("HedgeFlower")) { shared[i] = Copy(m); changed = true; if (!flowers.Contains(r)) flowers.Add(r); }
                    else if (IsOutdoorLeaf(r, n))
                    {
                        shared[i] = Copy(m); changed = true;
                        if (!leaves.Contains(r)) leaves.Add(r);
                    }
                }
                if (changed) r.sharedMaterials = shared;
            }
            foreach (var kv in copies)
            {
                string n = kv.Key.name;
                if (n == "Lawn") lawnM = kv.Value;
                else if (n == "LawnEdge") lawnEdgeM = kv.Value;
                else if (n == "Hedge") hedgeM = kv.Value;
                else if (n == "CityLeaf") cityLeaf.Add(kv.Value);
                else if (n.StartsWith("HedgeFlower")) flowerMats.Add(kv.Value);
                else leafMats.Add(kv.Value);
            }
        }

        /// <summary>Trees and shrubs in the garden (indoor plants stay green all year).</summary>
        static bool IsOutdoorLeaf(Renderer r, string material)
        {
            if (!(material.EndsWith("_leaves") || material == "shrub_02")) return false;
            var t = r.transform;
            while (t != null)
            {
                string n = t.name;
                if (n.StartsWith("island_tree") || n.StartsWith("searsia") || n.StartsWith("shrub_02")) return true;
                if (n.StartsWith("potted_plant") || n.StartsWith("pachira") || n.StartsWith("calathea")) return false;
                t = t.parent;
            }
            return false;
        }

        // ------------------------------------------------------------ the changing look

        void Update()
        {
            var dn = DayNightCycle.Instance;
            if (dn && dn.auto && dn.DayCount != lastDay)
            {
                int steps = Mathf.Max(1, dn.DayCount - lastDay);
                lastDay = dn.DayCount;
                DayInSeason += steps;
                if (DayInSeason >= daysPerSeason) { DayInSeason = 0f; Choose((Season)(((int)season + 1) % 4)); }
            }
            else if (dn) lastDay = dn.DayCount;

            var target = Looks[(int)season];
            float k = 1f - Mathf.Exp(-0.9f * Time.unscaledDeltaTime);     // eases over in a few seconds
            now = Mix(now, target, k);
            ApplyLook(now, false);
            UpdateWeather(target);
        }

        public void Next() => Choose((Season)(((int)season + 1) % 4));

        public void Choose(Season s)
        {
            season = s;
            DayInSeason = 0f;
            PlayerPrefs.SetInt(PrefKey, (int)s);
            SetWeather(s, false);
        }

        static Look Mix(Look a, Look b, float k) => new Look
        {
            lawn = Color.Lerp(a.lawn, b.lawn, k), hedge = Color.Lerp(a.hedge, b.hedge, k), leaf = Color.Lerp(a.leaf, b.leaf, k), flower = Color.Lerp(a.flower, b.flower, k),
            leafOn = Mathf.Lerp(a.leafOn, b.leafOn, k), snow = Mathf.Lerp(a.snow, b.snow, k), sunPeak = Mathf.Lerp(a.sunPeak, b.sunPeak, k),
            sunrise = Mathf.Lerp(a.sunrise, b.sunrise, k), sunset = Mathf.Lerp(a.sunset, b.sunset, k), sunPower = Mathf.Lerp(a.sunPower, b.sunPower, k),
            warm = Mathf.Lerp(a.warm, b.warm, k), cloud = Mathf.Lerp(a.cloud, b.cloud, k), haze = Mathf.Lerp(a.haze, b.haze, k),
        };

        static void Tint(Material m, Color c)
        {
            if (!m) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("baseColorFactor")) m.SetColor("baseColorFactor", c);
        }

        void ApplyLook(Look l, bool force)
        {
            Tint(lawnM, l.lawn);
            Tint(lawnEdgeM, Color.Lerp(l.lawn, Color.black, 0.15f));
            Tint(hedgeM, l.hedge);
            foreach (var m in cityLeaf) Tint(m, Color.Lerp(l.hedge, l.leaf * 0.45f, 0.5f));
            foreach (var m in leafMats) Tint(m, l.leaf);
            foreach (var m in flowerMats) Tint(m, l.flower);
            bool leavesShown = l.leafOn > 0.35f;
            foreach (var r in leaves) if (r && r.enabled != leavesShown) r.enabled = leavesShown;
            bool flowersShown = l.snow < 0.5f;
            foreach (var r in flowers) if (r && r.enabled != flowersShown) r.enabled = flowersShown;
            SunPeak = l.sunPeak; Sunrise = l.sunrise; Sunset = l.sunset; SunPower = l.sunPower; Warm = l.warm; CloudScale = l.cloud; HazeScale = l.haze;
        }

        // ------------------------------------------------------------ falling things

        static Mesh Quad()
        {
            if (quad) return quad;
            quad = new Mesh { name = "Leaf quad" };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            quad.RecalculateNormals(); quad.RecalculateBounds();
            return quad;
        }

        void MakeParticles()
        {
            var root = new GameObject("Season weather").transform;
            root.SetParent(transform, false);
            ParticleSystem Make(string name, Material mat, float life, float gravity, Vector2 startSize, Color color, bool tumble, float sway, float fallSpeed)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(15f, 14f, 9f);
                var ps = go.AddComponent<ParticleSystem>();
                var rr = go.GetComponent<ParticleSystemRenderer>();
                rr.sharedMaterial = mat;
                rr.shadowCastingMode = ShadowCastingMode.Off;
                var main = ps.main;
                main.loop = true; main.playOnAwake = false;
                main.startLifetime = life;
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(startSize.x, startSize.y);
                main.startColor = color;
                main.gravityModifier = gravity;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 12000;
                if (tumble)
                {
                    rr.renderMode = ParticleSystemRenderMode.Mesh;
                    rr.mesh = Quad();
                    rr.alignment = ParticleSystemRenderSpace.World;
                    main.startRotation3D = true;
                    main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 6.28f);
                    main.startRotationY = new ParticleSystem.MinMaxCurve(0f, 6.28f);
                    main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, 6.28f);
                    var rot = ps.rotationOverLifetime; rot.enabled = true; rot.separateAxes = true;
                    rot.x = new ParticleSystem.MinMaxCurve(-2.2f, 2.2f); rot.y = new ParticleSystem.MinMaxCurve(-2.6f, 2.6f); rot.z = new ParticleSystem.MinMaxCurve(-1.6f, 1.6f);
                }
                var em = ps.emission; em.rateOverTime = 0f; em.enabled = false;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(52f, 0.5f, 42f);
                var noise = ps.noise; noise.enabled = true; noise.strength = sway; noise.frequency = 0.22f; noise.scrollSpeed = 0.35f;
                var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World; vel.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.9f); vel.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f); vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
                var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limitY = new ParticleSystem.MinMaxCurve(fallSpeed); lim.dampen = 0.6f;
                ps.Play();
                return ps;
            }
            var pm = petalMaterials != null && petalMaterials.Length > 0 ? petalMaterials : new[] { petalMaterial };
            foreach (var m in pm) petalSystems.Add(Make("Petals", m, 18f, 0.04f, new Vector2(0.06f, 0.11f), Color.white, false, 0.6f, 1.2f));
            var lm = leafMaterials != null && leafMaterials.Length > 0 ? leafMaterials : new[] { petalMaterial };
            foreach (var m in lm) leafSystems.Add(Make("Falling leaves", m, 16f, 0.06f, new Vector2(0.24f, 0.4f), Color.white, true, 1.3f, 1.05f));
            snow = Make("Snow", snowMaterial, 12f, 0.05f, new Vector2(0.035f, 0.08f), Color.white, false, 0.6f, 1.8f);
            fireflies = Make("Fireflies", fireflyMaterial, 8f, 0f, new Vector2(0.06f, 0.09f), Color.white, false, 0.6f, 0.35f);
            var fs = fireflies.shape; fs.scale = new Vector3(40f, 1f, 30f); fireflies.transform.position = new Vector3(15f, 1.2f, 9f);
        }

        void SetWeather(Season s, bool instant)
        {
            // rates are eased in UpdateWeather
            if (instant) { petalStrength = s == Season.Spring ? 1f : 0f; leafStrength = s == Season.Autumn ? 1f : 0f; snowStrength = s == Season.Winter ? 1f : 0f; fireflyStrength = s == Season.Summer ? 1f : 0f; }
        }

        void UpdateWeather(Look target)
        {
            float k = 1f - Mathf.Exp(-0.7f * Time.unscaledDeltaTime);
            petalStrength = Mathf.Lerp(petalStrength, season == Season.Spring ? 1f : 0f, k);
            leafStrength = Mathf.Lerp(leafStrength, season == Season.Autumn ? 1f : 0f, k);
            snowStrength = Mathf.Lerp(snowStrength, season == Season.Winter ? 1f : 0f, k);
            float night = DayNightCycle.Instance ? DayNightCycle.Instance.Night01 : 0f;
            fireflyStrength = Mathf.Lerp(fireflyStrength, season == Season.Summer ? night : 0f, k);
            foreach (var ps in petalSystems) { Rate(ps, 36f * petalStrength); Fade(ps, season == Season.Spring); }
            foreach (var ps in leafSystems) { Rate(ps, 70f * leafStrength); Fade(ps, season == Season.Autumn); }
            Rate(snow, 1100f * snowStrength); Fade(snow, season == Season.Winter);
            Rate(fireflies, 26f * fireflyStrength); Fade(fireflies, season == Season.Summer);
        }

        /// <summary>When a season ends, whatever is still in the air fades away within about a second instead of drifting on for ten.</summary>
        static void Fade(ParticleSystem ps, bool active)
        {
            if (!ps || active) return;
            int n = ps.particleCount;
            if (n == 0) return;
            n = ps.GetParticles(buffer);
            float f = Mathf.Exp(-4f * Time.unscaledDeltaTime);
            for (int i = 0; i < n; i++)
            {
                var c = buffer[i].startColor;
                c.a = (byte)(c.a * f);
                buffer[i].startColor = c;
                buffer[i].startSize *= 1f - (1f - f) * 0.5f;
                if (c.a < 8) buffer[i].remainingLifetime = 0f;
            }
            ps.SetParticles(buffer, n);
        }

        static void Rate(ParticleSystem ps, float r)
        {
            if (!ps) return;
            var em = ps.emission;
            em.enabled = r > 0.5f;
            em.rateOverTime = r;
        }
    }
}
