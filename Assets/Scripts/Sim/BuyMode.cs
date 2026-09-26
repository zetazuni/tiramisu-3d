using System.Collections.Generic;
using UnityEngine;

namespace Tiramisu
{
    [System.Serializable]
    public class CatalogEntry
    {
        public string id, name, category;
        public GameObject prefab;
        public int Price => Household.PriceOf(id);
    }

    /// <summary>Seats and beds for pieces that are bought (the same places the builder gives the ones that come with the house).</summary>
    public static class SeatSpots
    {
        static void Spot(GameObject piece, string label, CharacterRig.Pose pose, Vector3 pelvis, float yaw, Vector3 approach, float recline = 6f, float shin = -8f, float footY = 0f, float raise = 0f, float legRaise = 0f, float knee = 6f)
        {
            var g = new GameObject("Use: " + label);
            g.transform.SetParent(piece.transform, false);
            g.transform.localPosition = pelvis;
            var sp = g.AddComponent<UseSpot>();
            sp.label = label; sp.pose = pose; sp.yaw = yaw; sp.approachLocal = approach;
            sp.recline = recline; sp.shinAngle = shin; sp.footY = footY; sp.raise = raise; sp.legRaise = legRaise; sp.kneeBend = knee;
            sp.seconds = pose == CharacterRig.Pose.Lie ? new Vector2(40f, 90f) : new Vector2(20f, 60f);
        }

        public static void Add(GameObject go, string id)
        {
            var sit = CharacterRig.Pose.Sit; var lie = CharacterRig.Pose.Lie;
            switch (id)
            {
                case "sofa": foreach (float x in new[] { -0.65f, 0f, 0.65f }) Spot(go, "sofa", sit, new Vector3(x, 0.62f, 0.02f), 0f, new Vector3(x, 0f, 1.1f), 16f, 12f); break;
                case "modern_arm_chair_01": Spot(go, "armchair", sit, new Vector3(0f, 0.52f, 0.05f), 0f, new Vector3(0f, 0f, 0.95f), 22f, 10f); break;
                case "diningchair": Spot(go, "dining chair", sit, new Vector3(0f, 0.52f, 0f), 0f, new Vector3(0.75f, 0f, 0f), 3f); break;
                case "barstool": Spot(go, "bar stool", sit, new Vector3(0f, 0.75f, 0f), 180f, new Vector3(0f, 0f, 0.8f), 0f, -5f, 0.27f); break;
                case "officechair": Spot(go, "office chair", sit, new Vector3(0f, 0.53f, 0.02f), 0f, new Vector3(0.8f, 0f, 0.1f), 8f); break;
                case "platformbed": case "platformbed_e": Spot(go, "bed", lie, new Vector3(0f, 0.7f, -0.25f), 0f, new Vector3(1.3f, 0f, 0f), knee: 5f); break;
                case "beanbag": Spot(go, "beanbag", sit, new Vector3(0f, 0.33f, 0f), 0f, new Vector3(0f, 0f, 0.95f), 38f, 22f); break;
                case "lounger": Spot(go, "lounger", lie, new Vector3(0f, 0.55f, -0.16f), 0f, new Vector3(0.9f, 0f, 0f), raise: 58f, knee: 4f); break;
                case "outdoorsectional": foreach (float x in new[] { -0.8f, 0f, 0.8f }) Spot(go, "outdoor sofa", sit, new Vector3(x, 0.56f, 0.08f), 0f, new Vector3(x, 0f, 1.0f), 12f, 10f); break;
                case "gardenbench": foreach (float x in new[] { -0.4f, 0.4f }) Spot(go, "bench", sit, new Vector3(x, 0.53f, 0f), 0f, new Vector3(x, 0f, 0.8f), 10f); break;
                case "hammock": Spot(go, "hammock", lie, new Vector3(0f, 0.85f, 0.2f), 0f, new Vector3(1.3f, 0f, 0f), raise: 14f, legRaise: 12f, knee: -14f); break;
            }
        }
    }

    /// <summary>Makes a piece from a catalogue model while the game runs: colliders, a body, furniture, seats, things to do.</summary>
    public static class FurnitureFactory
    {
        static int counter;
        static readonly HashSet<string> Small = new HashSet<string> { "fruitbowl", "candles", "mug", "globe", "bedlamp", "basket" };

        public static Furniture Create(GameObject prefab, string id, Vector3 pos, float yaw, string key = null)
        {
            var go = Object.Instantiate(prefab);
            go.name = id;
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            Bounds all = new Bounds(pos, Vector3.zero); bool first = true;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.sharedMesh) continue;
                var b = mf.sharedMesh.bounds;
                if (b.size.x < 0.03f && b.size.y < 0.03f && b.size.z < 0.03f) continue;
                var bc = mf.gameObject.AddComponent<BoxCollider>();
                bc.center = b.center; bc.size = b.size;
                var rend = mf.GetComponent<Renderer>();
                if (rend) { if (first) { all = rend.bounds; first = false; } else all.Encapsulate(rend.bounds); }
            }
            float volume = Mathf.Max(0.02f, all.size.x * all.size.y * all.size.z);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = Mathf.Clamp(volume * 90f, 2f, 140f);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = rb.mass < 8f ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.Discrete;
            rb.linearDamping = 0.6f; rb.angularDamping = 3f;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;   // stays upright
            var f = go.AddComponent<Furniture>();
            f.key = key ?? $"{id}#b{++counter}_{Random.Range(1000, 9999)}";
            f.small = Small.Contains(id);
            f.bought = true;
            SeatSpots.Add(go, id);
            Interactable.Attach(go, id);
            if (id == "tvunit")
            {
                var lg = new GameObject("TV glow");
                lg.transform.SetParent(go.transform, false);
                lg.transform.localPosition = new Vector3(0f, 1.0f, 0.7f);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point; lg.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
                l.lightUnit = UnityEngine.Rendering.LightUnit.Lumen; l.color = new Color(0.6f, 0.75f, 1f); l.range = 6f; l.intensity = 700f;
                l.shadows = LightShadows.None; l.enabled = false;
                go.AddComponent<TvScreen>();
            }
            return f;
        }
    }

    /// <summary>What was bought and what was sold, kept between sessions.</summary>
    public static class PurchaseSave
    {
        const string Key = "tiramisu.bought", SoldKey = "tiramisu.sold";
        [System.Serializable] class Item { public string key, id; public Vector3 pos; public float yaw; }
        [System.Serializable] class Data { public List<Item> items = new List<Item>(); public List<string> sold = new List<string>(); }
        static Data data;

        static Data Load()
        {
            if (data != null) return data;
            data = new Data();
            if (PlayerPrefs.HasKey(Key)) { try { data = JsonUtility.FromJson<Data>(PlayerPrefs.GetString(Key)) ?? new Data(); } catch { data = new Data(); } }
            return data;
        }

        static void Save() { PlayerPrefs.SetString(Key, JsonUtility.ToJson(Load())); PlayerPrefs.Save(); }

        public static void Record(Furniture f)
        {
            var d = Load();
            d.items.RemoveAll(i => i.key == f.key);
            d.items.Add(new Item { key = f.key, id = InteractionTable.BaseId(f.name), pos = f.transform.position, yaw = f.transform.eulerAngles.y });
            Save();
        }

        public static void Forget(Furniture f)
        {
            var d = Load();
            if (f.bought) d.items.RemoveAll(i => i.key == f.key);
            else if (!d.sold.Contains(f.key)) d.sold.Add(f.key);
            Save();
        }

        public static void Restore(Catalog cat)
        {
            var d = Load();
            foreach (var i in d.items)
            {
                var e = cat.Find(i.id);
                if (e != null) FurnitureFactory.Create(e.prefab, i.id, i.pos, i.yaw, i.key);
            }
            foreach (var f in Furniture.All.ToArray())
                if (!f.bought && d.sold.Contains(f.key)) Object.Destroy(f.gameObject);
        }

        public static void Clear() { data = new Data(); PlayerPrefs.DeleteKey(Key); }
    }

    /// <summary>The list of everything that can be bought (filled in by the builder).</summary>
    public class Catalog : MonoBehaviour
    {
        public static Catalog Instance { get; private set; }
        public List<CatalogEntry> entries = new List<CatalogEntry>();

        void Awake() { Instance = this; }

        public CatalogEntry Find(string id) { foreach (var e in entries) if (e.id == id) return e; return null; }
        public IEnumerable<string> Categories()
        {
            var seen = new List<string>();
            foreach (var e in entries) if (!seen.Contains(e.category)) seen.Add(e.category);
            return seen;
        }

        void Start() { PurchaseSave.Restore(this); }
    }

    /// <summary>The catalogue panel (key B): categories, what things cost, click to buy and place. Selling is the Delete key while a piece is held.</summary>
    public class BuyMode : MonoBehaviour
    {
        public static BuyMode Instance { get; private set; }
        public static bool Active { get; private set; }

        string category;
        Vector2 scroll;
        GUIStyle btn, btnOn, small, title;
        float scale = 1f;
        public Rect panel;

        void Awake() { Instance = this; }

        public static void Toggle()
        {
            Active = !Active;
            GameAudio.Play(GameAudio.Sfx.Click);
            if (Active && BuildMode.Active) BuildMode.Toggle();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.B) && !Input.GetKey(KeyCode.LeftControl)) Toggle();
            if (Active && Input.GetKeyDown(KeyCode.Escape) && !DecorateMode.Instance.Holding) Active = false;
        }

        void Styles()
        {
            if (btn != null) return;
            btn = new GUIStyle(GUI.skin.button) { fontSize = 12, alignment = TextAnchor.MiddleLeft, wordWrap = true, padding = new RectOffset(8, 8, 4, 4), fixedHeight = 46 };
            btnOn = new GUIStyle(btn) { fontStyle = FontStyle.Bold };
            btnOn.normal.textColor = btnOn.hover.textColor = new Color(1f, 0.78f, 0.86f);
            small = new GUIStyle(GUI.skin.label) { fontSize = 11 }; small.normal.textColor = new Color(1f, 1f, 1f, 0.8f);
            title = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold }; title.normal.textColor = Color.white;
        }

        void OnGUI()
        {
            if (!Active || Catalog.Instance == null) { panel = Rect.zero; return; }
            Styles();
            scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale, h = Screen.height / scale;
            panel = new Rect(230, h - 262, Mathf.Min(w - 470, 900), 196);
            GUI.Box(panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(panel.x + 8, panel.y + 6, panel.width - 16, panel.height - 12));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Buy mode", title, GUILayout.Width(90));
            if (category == null) category = "Living";
            foreach (var c in Catalog.Instance.Categories())
                if (GUILayout.Button(c, c == category ? btnOn : btn, GUILayout.Width(Mathf.Max(70, c.Length * 9 + 24)), GUILayout.Height(26))) { category = c; GameAudio.Play(GameAudio.Sfx.Click); }
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Funds: {Household.Currency} {(Household.Instance ? Household.Instance.Funds : 0):N0}", title);
            GUILayout.EndHorizontal();
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(122));
            GUILayout.BeginHorizontal();
            foreach (var e in Catalog.Instance.entries)
            {
                if (e.category != category) continue;
                bool afford = Household.CanAfford(e.Price);
                var old = GUI.color; if (!afford) GUI.color = new Color(1f, 1f, 1f, 0.5f);
                if (GUILayout.Button($"{e.name}\n{Household.Currency} {e.Price:N0}", btn, GUILayout.Width(128)))
                {
                    if (!afford) { Household.Toast($"You need {Household.Currency} {e.Price:N0} for the {e.name.ToLower()}."); GameAudio.Play(GameAudio.Sfx.No); }
                    else Buy(e);
                }
                GUI.color = old;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.Label(new Rect(panel.x + 10, panel.yMax + 2, 700, 20), "Click something to buy it, then move it into place and click. R turns it, Esc puts it back, Delete sells it.", small);
        }

        void Buy(CatalogEntry e)
        {
            var cam = Camera.main;
            Vector3 at = OrbitCamera.Instance ? OrbitCamera.Instance.pivot : new Vector3(10f, 0f, 8f);
            if (cam && Physics.Raycast(cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.55f, 0f)), out var hit, 300f, ~0, QueryTriggerInteraction.Ignore)) at = hit.point;
            at.y = Mathf.Max(at.y, -0.3f);
            var f = FurnitureFactory.Create(e.prefab, e.id, at + Vector3.up * 0.05f, 0f);
            f.pendingPrice = e.Price;
            GameAudio.Play(GameAudio.Sfx.Click);
            DecorateMode.Instance.BeginPlace(f);
        }
    }
}
