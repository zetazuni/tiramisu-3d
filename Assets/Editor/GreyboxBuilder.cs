using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
        const string MatDir = "Assets/Art/Materials/Greybox";

        static Material oak, oakDark, wallWhite, cap, slab, tile, bathTile, garageFloor, gym, lawn, lawnDark,
            deck, water, poolTile, stone, glass, steel, roof, trunk, leaf, mailbox;

        // ---------- render pipeline ----------

        [MenuItem("Tiramisu/Set up render pipeline")]
        public static void SetupPipeline()
        {
            System.IO.Directory.CreateDirectory("Assets/Settings");
            const string rdPath = "Assets/Settings/Tiramisu_Renderer.asset";
            const string rpPath = "Assets/Settings/Tiramisu_URP.asset";

            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(rpPath);
            if (!asset)
            {
                var rd = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rd, rdPath);
                asset = UniversalRenderPipelineAsset.Create(rd);
                AssetDatabase.CreateAsset(asset, rpPath);
            }
            asset.shadowDistance = 70f;
            asset.shadowCascadeCount = 2;
            asset.msaaSampleCount = 4;
            asset.supportsHDR = true;
            EditorUtility.SetDirty(asset);

            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            AssetDatabase.SaveAssets();
            Debug.Log("Tiramisu: URP is set up and the project now uses linear colour.");
        }

        // ---------- materials ----------

        static Material Mat(string name, Color c, float smooth = 0.15f, bool transparent = false, float metal = 0f)
        {
            System.IO.Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (transparent)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static void MakeMaterials()
        {
            oak = Mat("Oak", new Color(0.83f, 0.69f, 0.51f), 0.25f);
            oakDark = Mat("OakDark", new Color(0.62f, 0.45f, 0.30f), 0.25f);
            wallWhite = Mat("WallWhite", new Color(0.96f, 0.94f, 0.91f), 0.05f);
            cap = Mat("DarkCap", new Color(0.17f, 0.17f, 0.19f), 0.2f);
            slab = Mat("Slab", new Color(0.78f, 0.77f, 0.76f), 0.1f);
            tile = Mat("KitchenTile", new Color(0.88f, 0.87f, 0.85f), 0.45f);
            bathTile = Mat("BathTile", new Color(0.74f, 0.88f, 0.87f), 0.5f);
            garageFloor = Mat("GarageFloor", new Color(0.58f, 0.59f, 0.61f), 0.3f);
            gym = Mat("GymFloor", new Color(0.36f, 0.38f, 0.42f), 0.15f);
            lawn = Mat("Lawn", new Color(0.56f, 0.76f, 0.47f), 0.05f);
            lawnDark = Mat("LawnEdge", new Color(0.47f, 0.66f, 0.40f), 0.05f);
            deck = Mat("Deck", new Color(0.66f, 0.49f, 0.35f), 0.2f);
            water = Mat("PoolWater", new Color(0.36f, 0.74f, 0.90f, 0.75f), 0.9f, true);
            poolTile = Mat("PoolTile", new Color(0.55f, 0.82f, 0.90f), 0.4f);
            stone = Mat("Stone", new Color(0.84f, 0.82f, 0.78f), 0.1f);
            glass = Mat("Glass", new Color(0.72f, 0.85f, 0.92f, 0.22f), 0.95f, true);
            steel = Mat("BlackSteel", new Color(0.12f, 0.12f, 0.13f), 0.5f, false, 0.6f);
            roof = Mat("Roof", new Color(0.33f, 0.35f, 0.39f), 0.3f);
            trunk = Mat("Trunk", new Color(0.45f, 0.33f, 0.24f), 0.1f);
            leaf = Mat("Leaves", new Color(0.49f, 0.72f, 0.46f), 0.05f);
            mailbox = Mat("Mailbox", new Color(0.15f, 0.15f, 0.16f), 0.3f);
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
        }

        // ---------- the house ----------

        [MenuItem("Tiramisu/Build greybox house")]
        public static void Build()
        {
            if (GraphicsSettings.defaultRenderPipeline == null) SetupPipeline();
            MakeMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var house = Group("House", null);

            BuildGround(Group("Ground floor", house));
            var upper = Group("Upper floor", house);
            BuildUpper(upper);
            var roofGroup = Group("Roof", house);
            BuildRoof(roofGroup);
            BuildGarden(Group("Garden", null));
            var hv = BuildRig(upper.gameObject, roofGroup.gameObject);

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            const string scenePath = "Assets/Scenes/Main.unity";
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
            }
            Box("Stair spine", stairs, new Vector3(14.9f, 0f, 2f), new Vector3(15.1f, UPY, 6f), steel);
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
            float y = UPY + H;
            Box("Roof slab", g, new Vector3(-0.9f, y, -0.9f), new Vector3(WX + 0.7f, y + 0.28f, WD + 0.9f), roof);
            Box("Roof fascia", g, new Vector3(-0.92f, y + 0.28f, WD + 0.88f), new Vector3(WX + 0.72f, y + 0.4f, WD + 0.94f), wallWhite);
        }

        static void BuildGarden(Transform g)
        {
            const float gy = -SLAB;
            // outer ground and lawn both leave a hole for the pool (x 18 to 26, z 12 to 16)
            float px0 = 18f, px1 = 26f, pz0 = 12f, pz1 = 16f;
            float gb = gy - 0.4f, gt = gy - 0.02f;
            Box("Ground front", g, new Vector3(-14f, gb, -12f), new Vector3(44f, gt, pz0), lawnDark);
            Box("Ground back", g, new Vector3(-14f, gb, pz1), new Vector3(44f, gt, 32f), lawnDark);
            Box("Ground left", g, new Vector3(-14f, gb, pz0), new Vector3(px0, gt, pz1), lawnDark);
            Box("Ground right", g, new Vector3(px1, gb, pz0), new Vector3(44f, gt, pz1), lawnDark);

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
            Box("Pool water", pool, new Vector3(px0, gy - 1.3f, pz0), new Vector3(px1, gy - 0.12f, pz1), water, false);
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

            Tree(g, new Vector3(1.5f, gy, 20f), 1.3f);
            Tree(g, new Vector3(11f, gy, 20.5f), 1.6f);
            Tree(g, new Vector3(30.5f, gy, 19.5f), 1.4f);
            Tree(g, new Vector3(32.5f, gy, 12f), 1.1f);
            Tree(g, new Vector3(-1.5f, gy, 13f), 1.2f);

            Room(g, "Garden & pool", 0, 5, 0, WX, 10.5f, 22f, gy, null);
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

        // ---------- camera, light and view controller ----------

        static HouseView BuildRig(GameObject upper, GameObject roofGo)
        {
            var sun = new GameObject("Sun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.86f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.75f;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.sun = light;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.82f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.74f, 0.72f, 0.70f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.40f, 0.38f);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.80f, 0.87f, 0.95f);
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 300f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<UniversalAdditionalCameraData>();
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
            return hv;
        }
    }
}
