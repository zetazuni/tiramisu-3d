using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tiramisu.EditorTools
{
    /// <summary>
    /// Builds the greybox house from the 2D game's floor plan (1 tile = 1 m, garden toward +Z).
    /// Menu: Tiramisu > Build greybox house. It rebuilds Assets/Scenes/Main.unity from scratch,
    /// so tweak the numbers here and run it again rather than editing the scene by hand.
    /// </summary>
    public static class GreyboxBuilder
    {
        const float WX = 30f, WD = 8f, H = 3f, SLAB = 0.3f, UPY = 3.3f;
        const float OUT = 0.28f, PART = 0.14f, GLASS = 0.08f, DOOR_H = 2.3f;
        const string MatDir = "Assets/Art/Materials/Architecture";
        const float FLOOR_TOP = 0.02f; // room floor plates are 2 cm thick, furniture stands on top

        static Material oak, oakDark, wallWhite, cap, slab, tile, bathTile, garageFloor, gym, lawn, lawnDark,
            deck, water, poolTile, stone, glass, steel, roof, trunk, leaf, mailbox, lampGlow, poolGlow;

        // ---------- render pipeline ----------

        [MenuItem("Tiramisu/Set up render pipeline")]
        public static void SetupPipeline()
        {
            CinematicSetup.Pipeline();
            PhysicsSetup.ProjectSettings();
            Debug.Log("Tiramisu: HDRP is set up with the film look settings, DirectX 12 and linear colour.");
        }

        // ---------- materials ----------

        static Material Tx(string name, string tex, MaterialLibrary.Mapping map, Vector2 smooth, float normal = 1f, float metal = 0f, Color? tint = null, float scale = 1f)
            => MaterialLibrary.Textured($"{MatDir}/{name}.mat", tex, map, tint ?? Color.white, smooth, metal, normal, scale);

        /// <summary>Neutral (greyscale detail) texture with the colour set here.</summary>
        static Material Tn(string name, string tex, MaterialLibrary.Mapping map, Color tint, Vector2 smooth, float normal = 1f, float metal = 0f, float scale = 1f)
            => MaterialLibrary.Textured($"{MatDir}/{name}.mat", tex, map, tint, smooth, metal, normal, scale, true);

        static Material Pl(string name, Color c, float smooth, float metal = 0f)
            => MaterialLibrary.Plain($"{MatDir}/{name}.mat", c, smooth, metal);

        static Material Glowing(string name, Color c, Color emit)
        {
            var m = Pl(name, c, 0.6f);
            m.SetFloat("_UseEmissiveIntensity", 0f);
            m.SetColor("_EmissiveColor", emit);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void MakeMaterials()
        {
            var P = MaterialLibrary.Mapping.Planar;
            var T = MaterialLibrary.Mapping.Triplanar;
            oak = Tx("Oak", "herringbone_parquet", P, new Vector2(0.2f, 0.48f));
            oakDark = Tx("OakDark", "dark_wooden_planks", P, new Vector2(0.2f, 0.45f));
            wallWhite = Tn("WallWhite", "white_plaster_02", T, new Color(0.95f, 0.94f, 0.92f), new Vector2(0.02f, 0.22f), 0.3f);
            cap = Pl("DarkCap", new Color(0.05f, 0.05f, 0.055f), 0.5f);
            slab = Tn("Slab", "brushed_concrete", T, new Color(0.72f, 0.71f, 0.69f), new Vector2(0.1f, 0.4f));
            tile = Tx("KitchenTile", "marble_tiles", P, new Vector2(0.35f, 0.65f));
            bathTile = Tx("BathTile", "large_grey_tiles", P, new Vector2(0.3f, 0.6f));
            garageFloor = Tn("GarageFloor", "concrete_floor", P, new Color(0.62f, 0.62f, 0.63f), new Vector2(0.2f, 0.5f));
            gym = Tx("GymFloor", "rubber_tiles", P, new Vector2(0.05f, 0.35f));
            lawn = Tn("Lawn", "leafy_grass", P, new Color(0.40f, 0.58f, 0.24f), new Vector2(0f, 0.3f));
            lawnDark = Tn("LawnEdge", "leafy_grass", P, new Color(0.33f, 0.48f, 0.21f), new Vector2(0f, 0.25f));
            deck = Tn("Deck", "wood_floor_deck", P, new Color(0.64f, 0.47f, 0.33f), new Vector2(0.15f, 0.5f));
            water = MaterialLibrary.Water($"{MatDir}/PoolWater.mat");
            poolTile = Tn("PoolTile", "blue_floor_tiles_01", T, new Color(0.55f, 0.80f, 0.88f), new Vector2(0.5f, 0.9f));
            stone = Tn("Stone", "precast_stone_paving", T, new Color(0.80f, 0.78f, 0.74f), new Vector2(0.05f, 0.4f));
            glass = MaterialLibrary.Glass($"{MatDir}/Glass.mat", new Color(0.88f, 0.93f, 0.95f, 0.06f));
            steel = Pl("BlackSteel", new Color(0.04f, 0.04f, 0.045f), 0.78f, 1f);
            roof = Tn("Roof", "box_profile_metal_sheet", T, new Color(0.30f, 0.32f, 0.35f), new Vector2(0.35f, 0.7f), 1f, 1f);
            trunk = Tx("Trunk", "bark_brown_02", T, new Vector2(0f, 0.3f));
            leaf = Pl("Leaves", new Color(0.25f, 0.42f, 0.2f), 0.3f);
            mailbox = Pl("Mailbox", new Color(0.04f, 0.04f, 0.045f), 0.6f, 0.8f);
            lampGlow = Glowing("LampGlow", new Color(1f, 0.82f, 0.55f), new Color(1f, 0.7f, 0.35f) * 2.2f);
            poolGlow = Glowing("PoolGlow", new Color(0.5f, 0.9f, 1f), new Color(0.3f, 0.85f, 1f) * 2.5f);
        }

        // ---------- geometry helpers ----------

        /// <summary>A box from min corner to max corner in world metres.</summary>
        static GameObject Box(string name, Transform parent, Vector3 min, Vector3 max, Material m, bool shadows = true)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.position = (min + max) * 0.5f;
            g.transform.localScale = max - min;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            if (!shadows) r.shadowCastingMode = ShadowCastingMode.Off;
            return g;
        }

        static Transform Group(string name, Transform parent)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            return g.transform;
        }

        /// <summary>
        /// A wall line with optional door gaps. axis X means the wall sits at x = at and runs along Z.
        /// Door gaps are [from, to] ranges along the run; each gets a lintel above DOOR_H.
        /// </summary>
        static void Wall(Transform parent, string name, WallCutaway.Axis axis, float at, float thick,
            float from, float to, float floorY, Material m, bool isGlass, params Vector2[] doors)
        {
            var line = Group(name, parent);
            var cut = line.gameObject.AddComponent<WallCutaway>();
            cut.axis = axis;
            cut.plane = at + thick * 0.5f;
            cut.floor = floorY > 0.1f ? 1 : 0;
            cut.floorY = floorY;
            cut.fullHeight = H;

            float cursor = from;
            int i = 0;
            System.Array.Sort(doors, (a, b) => a.x.CompareTo(b.x));
            foreach (var d in doors)
            {
                if (d.x > cursor) Segment(line, $"{name} {i++}", axis, at, thick, cursor, d.x, floorY, floorY + H, m, isGlass);
                Segment(line, $"{name} lintel {i++}", axis, at, thick, d.x, d.y, floorY + DOOR_H, floorY + H, m, isGlass);
                cursor = d.y;
            }
            if (to > cursor) Segment(line, $"{name} {i}", axis, at, thick, cursor, to, floorY, floorY + H, m, isGlass);
        }

        static void Segment(Transform line, string name, WallCutaway.Axis axis, float at, float thick,
            float a, float b, float y0, float y1, Material m, bool isGlass)
        {
            Vector3 min = axis == WallCutaway.Axis.X ? new Vector3(at, y0, a) : new Vector3(a, y0, at);
            Vector3 max = axis == WallCutaway.Axis.X ? new Vector3(at + thick, y1, b) : new Vector3(b, y1, at + thick);
            Box(name, line, min, max, m, !isGlass);
            if (isGlass) Frames(line, name, axis, at, thick, a, b, y0, y1);
        }

        /// <summary>Slim black steel mullions (at most 1.5 m apart) and top and bottom rails on a glass panel.</summary>
        static void Frames(Transform line, string name, WallCutaway.Axis axis, float at, float thick, float a, float b, float y0, float y1)
        {
            const float w = 0.05f;
            float d0 = at - 0.015f, d1 = at + thick + 0.015f;
            Vector3 P(float along, float y, float depth) => axis == WallCutaway.Axis.X ? new Vector3(depth, y, along) : new Vector3(along, y, depth);
            int n = Mathf.Max(1, Mathf.CeilToInt((b - a) / 1.5f));
            for (int i = 0; i <= n; i++)
            {
                float u = Mathf.Clamp(a + (b - a) * i / n, a + w * 0.5f, b - w * 0.5f);
                Box($"{name} mullion {i}", line, P(u - w * 0.5f, y0, d0), P(u + w * 0.5f, y1, d1), steel);
            }
            Box($"{name} bottom rail", line, P(a, y0, d0), P(b, y0 + w, d1), steel);
            Box($"{name} top rail", line, P(a, y1 - w, d0), P(b, y1, d1), steel);
        }

        static void Room(Transform parent, string name, int floor, float order, float x0, float x1, float z0, float z1, float y, Material m)
        {
            if (m) Box($"{name} floor", parent, new Vector3(x0, y, z0), new Vector3(x1, y + 0.02f, z1), m);
            var mark = new GameObject($"Room: {name}");
            mark.transform.SetParent(parent, false);
            mark.transform.position = new Vector3((x0 + x1) * 0.5f, y, (z0 + z1) * 0.5f);
            var rm = mark.AddComponent<RoomMarker>();
            rm.displayName = name;
            rm.floor = floor;
            rm.order = Mathf.RoundToInt(order);
            rm.size = new Vector2(x1 - x0, z1 - z0);

            if (m) // real rooms get a warm ceiling light and a reflection probe
            {
                bool cool = name == "Garage" || name == "Gym";
                CinematicSetup.RoomLightAndProbe(parent, name, mark.transform.position, rm.size, H, cool ? 4000f : 2700f, true);
            }
        }

        // ---------- the house ----------

        [MenuItem("Tiramisu/Build greybox house")]
        public static void Build()
        {
            SetupPipeline();
            FurnitureImport.ImportAll();
            MakeMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var house = Group("House", null);

            BuildGround(Group("Ground floor", house));
            var upper = Group("Upper floor", house);
            BuildUpper(upper);
            var roofGroup = Group("Roof", house);
            BuildRoof(roofGroup);
            BuildGarden(Group("Garden", null));
            Furnish(Group("Furniture", house), upper);
            var hv = BuildRig(upper.gameObject, roofGroup.gameObject);
            PhysicsSetup.AssignSurfaces(house);
            PhysicsSetup.AssignSurfaces(GameObject.Find("Garden").transform);

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            const string scenePath = "Assets/Scenes/Main.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            CinematicSetup.Bake();
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Tiramisu: greybox house built and saved to " + scenePath);
        }

        static void BuildGround(Transform g)
        {
            Box("Slab", g, new Vector3(-OUT, -SLAB, -OUT), new Vector3(WX + GLASS, 0f, WD + GLASS), slab);
            Box("Slab oak band", g, new Vector3(-OUT - 0.02f, -0.22f, WD + GLASS), new Vector3(WX + GLASS + 0.02f, -0.08f, WD + GLASS + 0.02f), oakDark);

            Room(g, "Living room", 0, 0, 0, 8, 0, WD, 0f, oak);
            Room(g, "Kitchen", 0, 1, 8, 14, 0, WD, 0f, tile);
            Room(g, "Stair hall", 0, 2, 14, 16, 0, WD, 0f, oak);
            Room(g, "Bathroom", 0, 3, 16, 22, 0, WD, 0f, bathTile);
            Room(g, "Garage", 0, 4, 22, 30, 0, WD, 0f, garageFloor);

            var walls = Group("Walls", g);
            Wall(walls, "Back wall", WallCutaway.Axis.Z, -OUT, OUT, -OUT, WX + GLASS, 0f, wallWhite, false);
            Wall(walls, "Left wall", WallCutaway.Axis.X, -OUT, OUT, 0f, WD + GLASS, 0f, wallWhite, false);
            Wall(walls, "Garage end glass", WallCutaway.Axis.X, WX, GLASS, 0f, WD, 0f, glass, true);
            Wall(walls, "Front glass", WallCutaway.Axis.Z, WD, GLASS, 0f, WX + GLASS, 0f, glass, true,
                new Vector2(3, 5), new Vector2(10, 12), new Vector2(18, 20), new Vector2(24, 28));
            Wall(walls, "Living and kitchen glass", WallCutaway.Axis.X, 8f - PART / 2, PART, 0f, WD, 0f, glass, true, new Vector2(3, 5));
            Wall(walls, "Kitchen and hall wall", WallCutaway.Axis.X, 14f - PART / 2, PART, 0f, WD, 0f, wallWhite, false, new Vector2(6.2f, 7.8f));
            Wall(walls, "Hall and bathroom wall", WallCutaway.Axis.X, 16f - PART / 2, PART, 0f, WD, 0f, wallWhite, false, new Vector2(6.2f, 7.8f));
            Wall(walls, "Bathroom and garage glass", WallCutaway.Axis.X, 22f - PART / 2, PART, 0f, WD, 0f, glass, true, new Vector2(3, 5));

            // floating oak stairs climbing from the front of the hall (z 6) up to the landing (z 2)
            var stairs = Group("Stairs", g);
            const int steps = 14;
            float rise = UPY / (steps + 1), run = 4f / steps;
            for (int i = 0; i < steps; i++)
            {
                float top = (i + 1) * rise;
                float z1 = 6f - i * run, z0 = z1 - run;
                Box($"Step {i + 1}", stairs, new Vector3(14.2f, top - 0.07f, z0), new Vector3(15.8f, top, z1), oak);
                // slim steel support under each step, so the stairs float without a wall in the middle of the hall
                Box($"Step support {i + 1}", stairs, new Vector3(14.92f, top - 0.32f, z0), new Vector3(15.08f, top - 0.07f, z1), steel);
            }
        }

        static void BuildUpper(Transform g)
        {
            Box("Slab left", g, new Vector3(-OUT, UPY - SLAB, -OUT), new Vector3(14f, UPY, WD + GLASS), slab);
            Box("Slab right", g, new Vector3(16f, UPY - SLAB, -OUT), new Vector3(WX + GLASS, UPY, WD + GLASS), slab);
            Box("Slab landing", g, new Vector3(14f, UPY - SLAB, -OUT), new Vector3(16f, UPY, 2f), slab);

            Room(g, "Teacher's Room", 1, 10, 0, 8, 0, WD, UPY, oak);
            Room(g, "Office & Library", 1, 11, 8, 14, 0, WD, UPY, oakDark);
            Room(g, "Landing", 1, 12, 14, 16, 0, 2, UPY, oak);
            Room(g, "Engineer's Room", 1, 13, 16, 22, 0, WD, UPY, oak);
            Room(g, "Gym", 1, 14, 22, 30, 0, WD, UPY, gym);

            var walls = Group("Walls", g);
            Wall(walls, "Back wall", WallCutaway.Axis.Z, -OUT, OUT, -OUT, WX + GLASS, UPY, wallWhite, false);
            Wall(walls, "Left wall", WallCutaway.Axis.X, -OUT, OUT, 0f, WD + GLASS, UPY, wallWhite, false);
            Wall(walls, "Gym end glass", WallCutaway.Axis.X, WX, GLASS, 0f, WD, UPY, glass, true);
            Wall(walls, "Front glass", WallCutaway.Axis.Z, WD, GLASS, 0f, WX + GLASS, UPY, glass, true);
            Wall(walls, "Teacher and office wall", WallCutaway.Axis.X, 8f - PART / 2, PART, 0f, WD, UPY, wallWhite, false, new Vector2(3, 5));
            Wall(walls, "Office and landing wall", WallCutaway.Axis.X, 14f - PART / 2, PART, 0f, WD, UPY, wallWhite, false, new Vector2(0.4f, 1.8f));
            Wall(walls, "Landing and engineer wall", WallCutaway.Axis.X, 16f - PART / 2, PART, 0f, WD, UPY, wallWhite, false, new Vector2(0.4f, 1.8f));
            Wall(walls, "Engineer and gym glass", WallCutaway.Axis.X, 22f - PART / 2, PART, 0f, WD, UPY, glass, true, new Vector2(3, 5));

            // glass balustrade along the top of the stairwell
            Box("Stairwell rail", g, new Vector3(14f, UPY, 2f), new Vector3(16f, UPY + 1f, 2.06f), glass, false);
            Box("Stairwell rail cap", g, new Vector3(14f, UPY + 1f, 1.99f), new Vector3(16f, UPY + 1.04f, 2.07f), steel);
        }

        static void BuildRoof(Transform g)
        {
            // flat modern roof: deep overhang over the deck, oak soffit underneath, black steel fascia
            float y = UPY + H;
            Vector3 lo = new Vector3(-0.7f, y, -0.7f), hi = new Vector3(WX + 0.7f, y + 0.32f, WD + 1.6f);
            Box("Roof", g, new Vector3(lo.x, y + 0.03f, lo.z), hi, roof);
            Box("Roof soffit (oak)", g, lo, new Vector3(hi.x, y + 0.03f, hi.z), oakDark);
            const float f = 0.06f;
            Box("Fascia front", g, new Vector3(lo.x - f, y - 0.02f, hi.z), new Vector3(hi.x + f, hi.y + 0.08f, hi.z + f), steel);
            Box("Fascia back", g, new Vector3(lo.x - f, y - 0.02f, lo.z - f), new Vector3(hi.x + f, hi.y + 0.08f, lo.z), steel);
            Box("Fascia left", g, new Vector3(lo.x - f, y - 0.02f, lo.z), new Vector3(lo.x, hi.y + 0.08f, hi.z), steel);
            Box("Fascia right", g, new Vector3(hi.x, y - 0.02f, lo.z), new Vector3(hi.x + f, hi.y + 0.08f, hi.z), steel);
        }

        static void BuildGarden(Transform g)
        {
            const float gy = -SLAB;
            // outer ground and lawn both leave a hole for the pool (x 18 to 26, z 12 to 16)
            float px0 = 18f, px1 = 26f, pz0 = 12f, pz1 = 16f;
            float gb = gy - 0.4f, gt = gy - 0.02f;
            Box("Ground front", g, new Vector3(-200f, gb, -200f), new Vector3(230f, gt, pz0), lawnDark);
            Box("Ground back", g, new Vector3(-200f, gb, pz1), new Vector3(230f, gt, 230f), lawnDark);
            Box("Ground left", g, new Vector3(-200f, gb, pz0), new Vector3(px0, gt, pz1), lawnDark);
            Box("Ground right", g, new Vector3(px1, gb, pz0), new Vector3(230f, gt, pz1), lawnDark);

            Box("Lawn front", g, new Vector3(-2f, gy - 0.02f, 10.5f), new Vector3(34f, gy, pz0), lawn);
            Box("Lawn back", g, new Vector3(-2f, gy - 0.02f, pz1), new Vector3(34f, gy, 22f), lawn);
            Box("Lawn left", g, new Vector3(-2f, gy - 0.02f, pz0), new Vector3(px0, gy, pz1), lawn);
            Box("Lawn right", g, new Vector3(px1, gy - 0.02f, pz0), new Vector3(34f, gy, pz1), lawn);

            Box("Deck", g, new Vector3(0f, gy, WD + GLASS), new Vector3(WX + GLASS, -0.06f, 10.5f), deck);

            var pool = Group("Pool", g);
            Box("Pool basin", pool, new Vector3(px0, gy - 1.4f, pz0), new Vector3(px1, gy - 1.3f, pz1), poolTile);
            Box("Pool wall near", pool, new Vector3(px0, gy - 1.4f, pz0), new Vector3(px1, gy, pz0 + 0.05f), poolTile);
            Box("Pool wall far", pool, new Vector3(px0, gy - 1.4f, pz1 - 0.05f), new Vector3(px1, gy, pz1), poolTile);
            Box("Pool wall left", pool, new Vector3(px0, gy - 1.4f, pz0), new Vector3(px0 + 0.05f, gy, pz1), poolTile);
            Box("Pool wall right", pool, new Vector3(px1 - 0.05f, gy - 1.4f, pz0), new Vector3(px1, gy, pz1), poolTile);
            // the surface is a live wave mesh (ripples, splashes, buoyancy), see PoolRipples
            var ripples = pool.gameObject.AddComponent<PoolRipples>();
            ripples.min = new Vector2(px0 + 0.05f, pz0 + 0.05f);
            ripples.max = new Vector2(px1 - 0.05f, pz1 - 0.05f);
            ripples.surfaceY = gy - 0.12f;
            ripples.floorY = gy - 1.3f;
            ripples.material = water;
            Box("Coping near", pool, new Vector3(px0 - 0.3f, gy, pz0 - 0.3f), new Vector3(px1 + 0.3f, gy + 0.05f, pz0), stone);
            Box("Coping far", pool, new Vector3(px0 - 0.3f, gy, pz1), new Vector3(px1 + 0.3f, gy + 0.05f, pz1 + 0.3f), stone);
            Box("Coping left", pool, new Vector3(px0 - 0.3f, gy, pz0), new Vector3(px0, gy + 0.05f, pz1), stone);
            Box("Coping right", pool, new Vector3(px1, gy, pz0), new Vector3(px1 + 0.3f, gy + 0.05f, pz1), stone);

            var path = Group("Stepping stones", g);
            for (int i = 0; i < 9; i++)
            {
                float z = 11f + i * 1.2f;
                Box($"Stone {i + 1}", path, new Vector3(3.5f, gy, z), new Vector3(4.5f, gy + 0.04f, z + 0.7f), stone);
            }
            Box("Mailbox (placeholder)", g, new Vector3(5.2f, gy, 17f), new Vector3(5.8f, gy + 1.2f, 17.5f), mailbox);

            foreach (var p in GardenProps) PropPlacer.Place(p, g);
            NightLights(g, gy, px0, px1, pz0, pz1);

            Room(g, "Garden & pool", 0, 5, 0, WX, 10.5f, 22f, gy, null);
        }

        /// <summary>
        /// A light that fades on at night. With an area size it is a soft rectangle panel that faces
        /// (pitch, yaw) degrees (90 = down, -90 = up), otherwise a small point light.
        /// </summary>
        static void AddNightLight(GameObject host, Vector3 at, Color colour, float night, float range, float day = 0f,
            Vector2 area = default, float pitch = 90f, float yaw = 0f)
        {
            var lg = new GameObject("Night light");
            lg.transform.SetParent(host.transform, false);
            lg.transform.position = at;
            var l = lg.AddComponent<Light>();
            if (area != default)
            {
                l.type = LightType.Rectangle;
                l.areaSize = area;
                lg.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }
            else l.type = LightType.Point;
            lg.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
            l.lightUnit = LightUnit.Lumen;
            l.color = colour;
            l.range = range;
            l.intensity = day;
            l.shadows = LightShadows.None;
            var sw = lg.AddComponent<SwitchableLight>();
            sw.day = day;
            sw.night = night;
        }

        /// <summary>Lights that come on at night: path bollards, soffit downlights over the deck, glowing pool lamps.</summary>
        static void NightLights(Transform g, float gy, float px0, float px1, float pz0, float pz1)
        {
            var lights = Group("Night lights", g);
            var warm = new Color(1f, 0.76f, 0.45f);

            // low bollards along the edge of the deck and beside the stepping stones
            var bolls = new System.Collections.Generic.List<Vector2>();
            foreach (float x in new[] { 1.5f, 6.5f, 10.5f, 14.5f, 29f }) bolls.Add(new Vector2(x, 10.9f));
            foreach (float z in new[] { 12.5f, 15.5f, 18.5f }) bolls.Add(new Vector2(2.6f, z));
            foreach (float z in new[] { 12.5f, 15.5f }) bolls.Add(new Vector2(5.4f, z));
            foreach (var b in bolls)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = "Bollard";
                post.transform.SetParent(lights, false);
                post.transform.position = new Vector3(b.x, gy + 0.32f, b.y);
                post.transform.localScale = new Vector3(0.11f, 0.32f, 0.11f);
                post.GetComponent<Renderer>().sharedMaterial = steel;
                var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cap.name = "Bollard glow";
                cap.transform.SetParent(post.transform, false);
                cap.transform.localPosition = new Vector3(0f, 0.93f, 0f);
                cap.transform.localScale = new Vector3(0.9f, 0.08f, 0.9f);
                cap.GetComponent<Renderer>().sharedMaterial = lampGlow;
                Object.DestroyImmediate(cap.GetComponent<Collider>());
                AddNightLight(post, new Vector3(b.x, gy + 0.72f, b.y), warm, 140f, 4f, 0f, new Vector2(0.3f, 0.3f), 90f);
            }

            // downlights in the roof overhang above the deck
            float ry = UPY + H - 0.02f;
            for (int i = 0; i < 6; i++)
            {
                float x = 2.5f + i * 5.2f;
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = "Soffit light";
                disc.transform.SetParent(lights, false);
                disc.transform.position = new Vector3(x, ry, WD + 1.1f);
                disc.transform.localScale = new Vector3(0.22f, 0.005f, 0.22f);
                disc.GetComponent<Renderer>().sharedMaterial = lampGlow;
                Object.DestroyImmediate(disc.GetComponent<Collider>());
                AddNightLight(disc, new Vector3(x, ry - 0.05f, WD + 1.1f), warm, 380f, 6f, 0f, new Vector2(0.7f, 0.7f), 90f);
            }

            // glowing lamps in the pool wall and the water lit from below
            foreach (float x in new[] { 19.7f, 22f, 24.3f })
            {
                foreach (bool near in new[] { true, false })
                {
                    float z = near ? pz0 + 0.05f : pz1 - 0.05f;
                    float dz = near ? 0.02f : -0.02f;
                    var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    lamp.name = "Pool lamp";
                    lamp.transform.SetParent(lights, false);
                    lamp.transform.position = new Vector3(x, gy - 0.85f, z + dz);
                    lamp.transform.localScale = new Vector3(0.32f, 0.12f, 0.03f);
                    lamp.GetComponent<Renderer>().sharedMaterial = poolGlow;
                    Object.DestroyImmediate(lamp.GetComponent<Collider>());
                    AddNightLight(lamp, new Vector3(x, gy - 0.85f, z + (near ? 0.12f : -0.12f)), new Color(0.3f, 0.85f, 1f), 260f, 6f, 0f, new Vector2(0.9f, 0.5f), 0f, near ? 0f : 180f);
                }
            }
        }

        static void Tree(Transform parent, Vector3 at, float size)
        {
            var t = Group("Tree", parent);
            var trunkGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunkGo.name = "Trunk";
            trunkGo.transform.SetParent(t, false);
            trunkGo.transform.position = at + Vector3.up * size;
            trunkGo.transform.localScale = new Vector3(0.25f * size, size, 0.25f * size);
            trunkGo.GetComponent<Renderer>().sharedMaterial = trunk;
            var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "Leaves";
            crown.transform.SetParent(t, false);
            crown.transform.position = at + Vector3.up * size * 2.3f;
            crown.transform.localScale = Vector3.one * size * 2f;
            crown.GetComponent<Renderer>().sharedMaterial = leaf;
        }

        // ---------- furniture ----------

        /// <summary>
        /// Default furniture: model id (file name in Assets/Art/Models), centre x and z in metres,
        /// rotation in degrees (0 = the front faces the garden, +Z; 180 = faces the back wall) and floor.
        /// Blender models face -Y, which arrives in Unity facing +Z.
        /// </summary>
        static readonly (string id, float x, float z, float rot, int floor)[] Layout =
        {
            ("geomrug", 4f, 4.3f, 0f, 0),
            ("sofa", 4f, 3.1f, 0f, 0),
            ("marbletable", 4f, 4.6f, 0f, 0),
            ("cushion", 3.35f, 3.05f, 12f, 0),
            ("cushion", 4.7f, 3.05f, -8f, 0),
            // kitchen (x 8 to 14): counter run on the back wall, fridge beside it, island, stools, dining set
            ("kitchenrun", 11.62f, 0.34f, 0f, 0),
            ("fridge", 8.98f, 0.4f, 0f, 0),
            ("kitchenisland", 11.6f, 3.7f, 0f, 0),
            ("barstool", 10.6f, 4.55f, 20f, 0),
            ("barstool", 11.6f, 4.55f, -10f, 0),
            ("barstool", 12.6f, 4.55f, 5f, 0),
            ("diningtable", 10.9f, 6.5f, 0f, 0),
            ("diningchair", 10.3f, 5.75f, 0f, 0),
            ("diningchair", 11.5f, 5.75f, 0f, 0),
            ("diningchair", 10.3f, 7.25f, 180f, 0),
            ("diningchair", 11.5f, 7.25f, 180f, 0),
            ("pendant", 10.5f, 3.7f, 0f, 0),
            ("pendant", 11.6f, 3.7f, 0f, 0),
            ("pendant", 12.7f, 3.7f, 0f, 0),
            // bathroom (x 16 to 22): shower, vanity and toilet along the back wall, tub in the middle, laundry on the right
            ("shower", 16.72f, 0.62f, 0f, 0),
            ("vanity", 18.75f, 0.27f, 0f, 0),
            ("bathmirror", 18.75f, 0.04f, 0f, 0),
            ("toilet", 20.1f, 0.33f, 0f, 0),
            ("towelrack", 21.35f, 0.08f, 0f, 0),
            ("bathmat", 18.75f, 1.05f, 0f, 0),
            ("bathtub", 20.75f, 3.3f, 0f, 0),
            ("washer", 21.6f, 5.4f, 270f, 0),
            ("dryer", 21.6f, 6.1f, 270f, 0),
            ("basket", 20.8f, 6.9f, 0f, 0),
            // garage (x 22 to 30): two cars nose to the garden, tools along the back wall
            ("sedan", 24.4f, 4.5f, 0f, 0),
            ("mpv", 27.4f, 4.3f, 0f, 0),
            ("evcharger", 25.9f, 0.12f, 0f, 0),
            ("workbench", 28.9f, 0.34f, 0f, 0),
            ("garageshelf", 23.1f, 0.24f, 0f, 0),
            ("toolchest", 29.5f, 1.4f, 270f, 0),
            ("bicycle", 22.65f, 3.9f, 0f, 0),
            // planters under the money trees (the trees stand on their soil, 0.38 m up)
            ("planter", 0.75f, 0.8f, 0f, 0),
            ("planter", 16.75f, 5f, 0f, 0),
            ("planter", 7.3f, 7.2f, 0f, 1),
            ("planter", 21.3f, 2.6f, 0f, 1),
            ("planter", 15f, 0.7f, 0f, 1),
            // upper floor, Teacher's Room (x 0 to 8)
            ("platformbed", 5f, 1.17f, 0f, 1),
            ("nightstand", 3.9f, 0.22f, 0f, 1),
            ("nightstand", 6.1f, 0.22f, 0f, 1),
            ("wardrobe", 1f, 0.32f, 0f, 1),
            ("bookcase", 0.22f, 3.3f, 90f, 1),
            ("geomrug", 5f, 3.6f, 0f, 1),
            ("teacherdesk", 0.4f, 6f, 90f, 1),
            ("officechair", 1.2f, 6f, 270f, 1),
            // Office & Library (x 8 to 14)
            ("bookcase", 8.75f, 0.22f, 0f, 1),
            ("bookcase", 10f, 0.22f, 0f, 1),
            ("officedesk", 10.4f, 4.2f, 0f, 1),
            ("officechair", 10.4f, 5.05f, 180f, 1),
            ("officechair", 11.6f, 5f, 170f, 1),
            ("filecabinet", 13.55f, 2.2f, 270f, 1),
            ("uplight", 13.3f, 7.3f, 0f, 1),
            // Engineer's Room (x 16 to 22)
            ("platformbed_e", 20.2f, 1.17f, 0f, 1),
            ("nightstand", 18.9f, 0.22f, 0f, 1),
            ("wardrobe", 17.1f, 0.32f, 0f, 1),
            ("ebench", 16.45f, 5f, 90f, 1),
            ("officechair", 17.3f, 5f, 270f, 1),
            ("robotarm", 18.7f, 3.2f, 0f, 1),
            ("printer3d", 21f, 6.8f, 0f, 1),
            ("beanbag", 20.4f, 4.6f, 20f, 1),
            ("geomrug", 19.6f, 3.9f, 0f, 1),
            // Gym (x 22 to 30)
            ("treadmill", 23.3f, 0.93f, 0f, 1),
            ("treadmill", 25.2f, 0.93f, 0f, 1),
            ("dumbbells", 27.7f, 0.24f, 0f, 1),
            ("weightbench", 24f, 4.2f, 0f, 1),
            ("spinbike", 27.4f, 3.3f, 0f, 1),
            ("spinbike", 29f, 3.3f, 0f, 1),
            ("punchbag", 26.4f, 5.8f, 0f, 1),
            ("yogamat", 23.3f, 6.5f, 0f, 1),
            ("yogamat", 24.2f, 6.5f, 0f, 1),
            ("waterdispenser", 28.4f, 7.3f, 180f, 1),
        };

        /// <summary>Small things standing on surfaces: model id, x, y (height of the surface in metres), z and rotation.</summary>
        static readonly (string id, float x, float y, float z, float rot)[] Tabletop =
        {
            // kitchen island top is 0.94 m, counter top 0.92 m
            ("fruitbowl", 10.55f, 0.94f, 3.6f, 0f),
            ("cuttingboard", 12.4f, 0.94f, 3.55f, 12f),
            ("mug", 11.75f, 0.94f, 3.35f, 0f),
            ("mug", 11.95f, 0.94f, 3.5f, 70f),
            ("espresso", 13.35f, 0.92f, 0.33f, 0f),
            ("utensils", 11.45f, 0.92f, 0.3f, 0f),
            ("herbs", 11.8f, 0.92f, 0.32f, 20f),
            // bathroom: vanity top is 0.84 m
            ("towelstack", 21.6f, 0.87f, 6.1f, 90f),
            ("candles", 19.45f, 0.02f, 2.9f, 0f),
            // garage
            ("cardboardboxes", 29.4f, 0.02f, 4.5f, 10f),
            ("paintcans", 23.15f, 0.02f, 1.0f, 0f),
            ("sparetyres", 22.6f, 0.02f, 6.9f, 0f),
            // upper floor surfaces (floor top is 3.32 m): nightstands 0.52, desks 0.75
            ("bedlamp", 3.9f, 3.84f, 0.22f, 0f),
            ("bedlamp", 6.1f, 3.84f, 0.22f, 0f),
            ("bedlamp", 18.9f, 3.84f, 0.22f, 0f),
            ("globe", 0.5f, 4.07f, 5.6f, 0f),
            ("mug", 10.9f, 4.07f, 4.3f, 0f),
            // things hung on upper walls
            ("chalkboard", 0.04f, 4.57f, 6f, 90f),
            ("worldmap", 5f, 4.72f, 0.03f, 0f),
            ("whiteboard", 12.4f, 4.32f, 0.02f, 0f),
            ("gymmirror", 23.6f, 4.32f, 0.02f, 0f),
            ("gymmirror", 25.6f, 4.32f, 0.02f, 0f),
            ("gymmirror", 27.6f, 4.32f, 0.02f, 0f),
        };

        static PropPlacer.Prop Pr(string id, string variant, float x, float y, float z, float rot, float scale,
            PropPlacer.Body body, float mass = 0f, float colliderHeight = 0f)
            => new PropPlacer.Prop { id = id, variant = variant, at = new Vector3(x, y, z), rot = rot, scale = scale, body = body, mass = mass, colliderHeight = colliderHeight };

        const float TableTop = 0.4f + FLOOR_TOP; // marble coffee table
        const float SideTop = 0.45f + FLOOR_TOP; // side_table_01

        /// <summary>Photoscanned props from Poly Haven (tools/fetch_models.py) in the living room.</summary>
        static readonly PropPlacer.Prop[] LivingProps =
        {
            Pr("modern_arm_chair_01", null, 6.5f, FLOOR_TOP, 4.5f, 70f, 1f, PropPlacer.Body.Dynamic, 18f),
            Pr("side_table_01", null, 5.55f, FLOOR_TOP, 2.95f, 0f, 1f, PropPlacer.Body.Dynamic, 6f),
            Pr("desk_lamp_arm_01", null, 5.6f, SideTop + 0.002f, 2.9f, 200f, 1f, PropPlacer.Body.Dynamic, 2.5f),
            Pr("book_encyclopedia_set_01", null, 3.7f, TableTop + 0.004f, 4.6f, 0f, 1f, PropPlacer.Body.DynamicParts, 0.7f),
            Pr("ceramic_vase_03", null, 4.45f, TableTop + 0.002f, 4.62f, 0f, 1f, PropPlacer.Body.Dynamic, 1.2f),
            Pr("potted_plant_01", null, 7.3f, FLOOR_TOP, 0.75f, 30f, 1.35f, PropPlacer.Body.Static, 0f, 0.55f),
            Pr("pachira_aquatica_01", "_d", 0.75f, FLOOR_TOP + 0.38f, 0.8f, 0f, 1f, PropPlacer.Body.Static, 0f, 0.5f),
            Pr("pachira_aquatica_01", "_d", 16.75f, FLOOR_TOP + 0.38f, 5.0f, 30f, 0.95f, PropPlacer.Body.Static, 0f, 0.5f),
        };

        const float UpFloor = UPY + FLOOR_TOP;

        /// <summary>Photoscanned props on the upper floor.</summary>
        static readonly PropPlacer.Prop[] UpperProps =
        {
            Pr("modern_arm_chair_01", null, 6.8f, UpFloor, 5.8f, 220f, 1f, PropPlacer.Body.Dynamic, 18f),
            Pr("side_table_01", null, 7.5f, UpFloor, 6.6f, 0f, 1f, PropPlacer.Body.Dynamic, 6f),
            Pr("pachira_aquatica_01", "_d", 7.3f, UpFloor + 0.38f, 7.2f, 0f, 1f, PropPlacer.Body.Static, 0f, 0.5f),
            Pr("potted_plant_01", null, 8.6f, UpFloor, 7.3f, 30f, 1.35f, PropPlacer.Body.Static, 0f, 0.55f),
            Pr("modern_arm_chair_01", null, 12.5f, UpFloor, 6.5f, 200f, 1f, PropPlacer.Body.Dynamic, 18f),
            Pr("pachira_aquatica_01", "_c", 21.3f, UpFloor + 0.38f, 2.6f, 60f, 1.35f, PropPlacer.Body.Static, 0f, 0.5f),
            Pr("potted_plant_01", null, 29.3f, UpFloor, 7.2f, 0f, 1.35f, PropPlacer.Body.Static, 0f, 0.55f),
            Pr("pachira_aquatica_01", "_a", 15f, UpFloor + 0.38f, 0.7f, 120f, 1.3f, PropPlacer.Body.Static, 0f, 0.5f),
        };

        /// <summary>Real trees and shrubs for the garden (garden ground sits at -SLAB).</summary>
        static readonly PropPlacer.Prop[] GardenProps =
        {
            Pr("island_tree_02", null, 1.5f, -SLAB, 20f, 0f, 1.5f, PropPlacer.Body.Static, 0f, 2f),
            Pr("island_tree_02", null, 30.5f, -SLAB, 19.5f, 140f, 1.35f, PropPlacer.Body.Static, 0f, 2f),
            Pr("searsia_lucida", "_a_LOD0", 11f, -SLAB, 20.5f, 60f, 1.3f, PropPlacer.Body.Static, 0f, 1.5f),
            Pr("searsia_lucida", "_b_LOD0", 32.5f, -SLAB, 12f, 200f, 1.3f, PropPlacer.Body.Static, 0f, 1.5f),
            Pr("searsia_lucida", "_c_LOD0", -1.8f, -SLAB, 13f, 20f, 1.3f, PropPlacer.Body.Static, 0f, 1.2f),
            Pr("shrub_02", "_b", 7.5f, -SLAB, 11.6f, 0f, 0.55f, PropPlacer.Body.Static, 0f, 0.6f),
            Pr("shrub_02", "_d", 13.5f, -SLAB, 11.5f, 90f, 0.5f, PropPlacer.Body.Static, 0f, 0.6f),
            Pr("shrub_02", "_a", 28.5f, -SLAB, 11.4f, 45f, 0.55f, PropPlacer.Body.Static, 0f, 0.6f),
            Pr("shrub_02", "_c", 17f, -SLAB, 19.5f, 0f, 0.5f, PropPlacer.Body.Static, 0f, 0.6f),
            Pr("shrub_04", null, 2.6f, -SLAB, 12.8f, 0f, 1.2f, PropPlacer.Body.None),
            Pr("shrub_04", null, 5.3f, -SLAB, 15.2f, 70f, 1.2f, PropPlacer.Body.None),
            Pr("shrub_04", null, 2.8f, -SLAB, 18.4f, 140f, 1.2f, PropPlacer.Body.None),
        };

        static readonly System.Collections.Generic.Dictionary<string, int> keyCount = new System.Collections.Generic.Dictionary<string, int>();

        /// <summary>Built in fittings and things hung on walls or ceilings stay where they are.</summary>
        static readonly System.Collections.Generic.HashSet<string> Pinned = new System.Collections.Generic.HashSet<string>
        {
            "kitchenrun", "shower", "toilet", "bathtub", "vanity", "pendant", "punchbag", "evcharger",
            "bathmirror", "worldmap", "chalkboard", "whiteboard", "gymmirror",
        };

        static void MakeMovable(GameObject go, bool pinned)
        {
            if (!go) return;
            string id = go.name;
            keyCount.TryGetValue(id, out int n);
            keyCount[id] = n + 1;
            var f = go.AddComponent<Furniture>();
            f.key = $"{id}#{n}";
            f.pinned = pinned || Pinned.Contains(id);
        }

        static void AttachTo(string wallPath, GameObject item)
        {
            var wall = GameObject.Find(wallPath);
            if (wall && wall.GetComponent<WallCutaway>()) wall.GetComponent<WallCutaway>().attachments.Add(item);
        }

        static void Furnish(Transform ground, Transform upper)
        {
            foreach (var p in LivingProps) MakeMovable(PropPlacer.Place(p, ground), false);
            foreach (var p in UpperProps) MakeMovable(PropPlacer.Place(p, upper), false);

            // a picture above the sofa, hung on the back wall (hidden while that wall is cut down)
            var pic = PropPlacer.Place(Pr("hanging_picture_frame_02", null, 4f, 1.35f, 0.01f, 0f, 1.4f, PropPlacer.Body.None), ground);
            var backWall = GameObject.Find("House/Ground floor/Walls/Back wall");
            if (pic && backWall) backWall.GetComponent<WallCutaway>().attachments.Add(pic);

            keyCount.Clear();
            var all = new System.Collections.Generic.List<(string id, float x, float y, float z, float rot, int floor)>();
            foreach (var l in Layout) all.Add((l.id, l.x, -1f, l.z, l.rot, l.floor));   // y -1 = on the floor
            foreach (var t in Tabletop) all.Add((t.id, t.x, t.y, t.z, t.rot, t.y > 2f ? 1 : 0));
            foreach (var f in all)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{FurnitureImport.ModelDir}/{f.id}.fbx");
                if (!model) { Debug.LogWarning($"Tiramisu: model {f.id} not found, skipped."); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model, f.floor == 0 ? ground : upper);
                go.name = f.id;
                var spec = PhysicsSetup.Spec(f.id);
                go.transform.position = new Vector3(f.x, f.y >= 0f ? f.y + spec.dropHeight : (f.floor == 0 ? 0f : UPY) + FLOOR_TOP + spec.dropHeight, f.z);
                go.transform.rotation = Quaternion.Euler(0f, f.rot, 0f);
                PhysicsSetup.MakeSolid(go, spec);
                MakeMovable(go, false);
                if (f.id == "bathmirror" && backWall) backWall.GetComponent<WallCutaway>().attachments.Add(go); // hangs on the back wall
                if (f.id == "worldmap" || f.id == "whiteboard" || f.id == "gymmirror") AttachTo("House/Upper floor/Walls/Back wall", go);
                if (f.id == "chalkboard") AttachTo("House/Upper floor/Walls/Left wall", go);
                if (f.id == "bedlamp") AddNightLight(go, go.transform.position + Vector3.up * 0.33f, new Color(1f, 0.78f, 0.5f), 110f, 3f, 0f, new Vector2(0.35f, 0.35f), 90f);
                if (f.id == "uplight") // washes the ceiling with a wide, soft glow
                    AddNightLight(go, go.transform.position + Vector3.up * 1.68f, new Color(1f, 0.8f, 0.55f), 380f, 5f, 0f, new Vector2(0.5f, 0.5f), -90f);
                if (f.id == "sedan" || f.id == "mpv") // red glow behind the tail lights
                {
                    float rear = f.id == "sedan" ? 2.3f : 2.08f; // metres behind the centre (Blender +Y is Unity -Z)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var lg = new GameObject("Tail light glow");
                        lg.transform.SetParent(go.transform, false);
                        lg.transform.position = go.transform.position + new Vector3(side * 0.5f, 0.78f, -(rear + 0.12f));
                        var l = lg.AddComponent<Light>();
                        l.type = LightType.Point;
                        lg.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
                        l.lightUnit = LightUnit.Lumen;
                        l.intensity = 60f;
                        l.color = new Color(1f, 0.05f, 0.03f);
                        l.range = 2.5f;
                        l.shadows = LightShadows.None;
                    }
                }
                if (f.id == "pendant") // a soft downward glow from each shade
                    AddNightLight(go, go.transform.position + Vector3.up * 1.86f, new Color(1f, 0.78f, 0.5f), 300f, 4f, 60f, new Vector2(0.5f, 0.5f), 90f);
            }
        }

        // ---------- camera, light and view controller ----------

        static HouseView BuildRig(GameObject upper, GameObject roofGo)
        {
            var sunLight = CinematicSetup.Sun();
            var moonLight = CinematicSetup.Moon();
            var volume = CinematicSetup.PostVolume();
            RenderSettings.fog = false; // HDRP fog lives in the volume

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 2000f;
            camGo.AddComponent<AudioListener>();
            CinematicSetup.Camera(camGo, volume);
            camGo.AddComponent<PhysicsPoke>();
            var orbit = camGo.AddComponent<OrbitCamera>();
            // put the camera where the orbit will start, so the editor view matches play mode
            var rot = Quaternion.Euler(orbit.pitch, orbit.yaw, 0f);
            camGo.transform.SetPositionAndRotation(orbit.pivot + rot * new Vector3(0f, 0f, -orbit.distance), rot);

            var game = new GameObject("Game");
            var hv = game.AddComponent<HouseView>();
            hv.upperFloor = upper;
            hv.roof = roofGo;
            hv.upperFloorY = UPY;
            game.AddComponent<HouseHud>();
            game.AddComponent<DecorateMode>();
            var dn = game.AddComponent<DayNightCycle>();
            dn.sun = sunLight;
            dn.moon = moonLight;
            dn.volume = volume;
            dn.hour = 15f;
            game.AddComponent<GraphicsModes>().volume = volume;
            game.AddComponent<FpsBenchmark>();
            return hv;
        }
    }
}
