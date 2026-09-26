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
        public Material petalMaterial, leafMaterial, snowMaterial, fireflyMaterial;

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
        ParticleSystem petals, fallLeaves, snow, fireflies;
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
                    if (n == "Lawn" || n == "LawnEdge" || n == "Hedge") { shared[i] = Copy(m); changed = true; }
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
            foreach (var m in leafMats) Tint(m, l.leaf);
            foreach (var m in flowerMats) Tint(m, l.flower);
            bool leavesShown = l.leafOn > 0.35f;
            foreach (var r in leaves) if (r && r.enabled != leavesShown) r.enabled = leavesShown;
            bool flowersShown = l.snow < 0.5f;
            foreach (var r in flowers) if (r && r.enabled != flowersShown) r.enabled = flowersShown;
            SunPeak = l.sunPeak; Sunrise = l.sunrise; Sunset = l.sunset; SunPower = l.sunPower; Warm = l.warm; CloudScale = l.cloud; HazeScale = l.haze;
        }

        // ------------------------------------------------------------ falling things

        void MakeParticles()
        {
            var root = new GameObject("Season weather").transform;
            root.SetParent(transform, false);
            ParticleSystem Make(string name, Material mat, float size, float life, float rate, float gravity, Vector2 startSize, Color color, float spinDeg)
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
                if (spinDeg > 0f) { main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f); }
                var em = ps.emission; em.rateOverTime = rate; em.enabled = false;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(52f, 0.5f, 42f);
                var noise = ps.noise; noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.15f; noise.scrollSpeed = 0.3f;
                var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World; vel.x = new ParticleSystem.MinMaxCurve(-0.4f, 0.8f); vel.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
                var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limitY = new ParticleSystem.MinMaxCurve(size); lim.dampen = 0.5f;
                if (spinDeg > 0f) { var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-spinDeg * Mathf.Deg2Rad, spinDeg * Mathf.Deg2Rad); }
                var col = ps.collision; col.enabled = false;
                ps.Play();
                return ps;
            }
            petals = Make("Petals", petalMaterial, 1.2f, 18f, 60f, 0.04f, new Vector2(0.06f, 0.11f), new Color(1f, 0.78f, 0.86f, 0.95f), 220f);
            fallLeaves = Make("Falling leaves", leafMaterial, 1.5f, 18f, 55f, 0.05f, new Vector2(0.1f, 0.2f), new Color(1f, 0.62f, 0.2f, 1f), 260f);
            snow = Make("Snow", snowMaterial, 1.8f, 12f, 1100f, 0.05f, new Vector2(0.035f, 0.08f), new Color(1f, 1f, 1f, 0.9f), 0f);
            fireflies = Make("Fireflies", fireflyMaterial, 0.35f, 8f, 25f, 0f, new Vector2(0.06f, 0.09f), new Color(1f, 0.95f, 0.5f, 1f), 0f);
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
            Rate(petals, 70f * petalStrength);
            Rate(fallLeaves, 60f * leafStrength);
            Rate(snow, 1100f * snowStrength);
            Rate(fireflies, 26f * fireflyStrength);
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
