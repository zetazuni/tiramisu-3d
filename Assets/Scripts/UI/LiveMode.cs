using System.Collections.Generic;
using UnityEngine;

namespace Tiramisu
{
    /// <summary>
    /// Live mode, the way you play the house like The Sims: click a person to pick them (a green diamond floats over their head),
    /// click the floor to send them there, click furniture or another person for a round menu of what they can do,
    /// space pauses and 1, 2, 3 change the speed. Orders queue up: a person finishes what they were told before they go
    /// back to doing their own thing.
    /// </summary>
    [DefaultExecutionOrder(20)]
    public class LiveMode : MonoBehaviour
    {
        public static LiveMode Instance { get; private set; }
        public static Character Selected { get; private set; }

        [Tooltip("the green diamond over the picked person")] public Material plumbobMaterial;

        static readonly float[] Speeds = { 0f, 1f, 2.5f, 5f };
        public static int Speed { get; private set; } = 1;

        // ---- round menu ----
        struct Option { public string label; public System.Action act; public bool enabled; }
        readonly List<Option> options = new List<Option>();
        bool pieOpen;
        Vector2 pieCenter;   // GUI points (already divided by the GUI scale)
        const float PieRadius = 78f, PieDisc = 92f;
        string pieTitle;

        Rect portraits, speedPanel;
        float scale = 1f;
        Texture2D disc;
        GUIStyle discLabel, tinyLabel, btn, btnOn, title;

        Transform plumbob;
        Vector3 markerAt; float markerUntil;
        float toastUntil; string toast;

        void Awake() { Instance = this; }

        void Start()
        {
            var previous = OrbitCamera.IsOverUi;
            OrbitCamera.IsOverUi = p => (previous != null && previous(p)) || portraits.Contains(p / scale) || speedPanel.Contains(p / scale) || PieContains(p);
            MakePlumbob();
        }

        void OnDestroy() { Time.timeScale = 1f; if (Instance == this) Instance = null; }

        // ---------------------------------------------------------------- input

        void Update()
        {
            if (Selected == null || !Selected) Selected = FirstPerson();

            if (Input.GetKeyDown(KeyCode.Space)) SetSpeed(Speed == 0 ? 1 : 0);
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetSpeed(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetSpeed(2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetSpeed(3);

            if (DecorateMode.Active) { pieOpen = false; UpdatePlumbob(); return; }

            if (pieOpen && (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))) pieOpen = false;
            var oc = OrbitCamera.Instance;
            if (oc && oc.ClickedThisFrame) HandleClick(Input.mousePosition);
            UpdatePlumbob();
        }

        public static void SetSpeed(int s)
        {
            Speed = Mathf.Clamp(s, 0, 3);
            Time.timeScale = Speeds[Speed];
        }

        static Character FirstPerson()
        {
            foreach (var c in Character.All) if (!c.isPet) return c;
            return null;
        }

        public static void Select(Character c) { if (c && !c.isPet) Selected = c; }

        // ---------------------------------------------------------------- clicks

        Character PickCharacter(Vector3 mouse)
        {
            var cam = Camera.main;
            if (!cam) return null;
            Character best = null;
            float bestD = 46f * scale;
            foreach (var c in Character.All)
            {
                var r = c.GetComponentInChildren<Renderer>();
                if (r && !r.enabled) continue;
                float h = (c.isPet ? 0.25f : 0.95f) * c.scale;
                var s = cam.WorldToScreenPoint(c.transform.position + Vector3.up * h);
                if (s.z < 0f) continue;
                float d = Vector2.Distance(new Vector2(s.x, s.y), new Vector2(mouse.x, mouse.y));
                if (c.isPet) d *= 0.8f;
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        void HandleClick(Vector3 mouse)
        {
            pieOpen = false;
            if (Selected == null) return;
            var cam = Camera.main;
            if (!cam) return;

            var who = PickCharacter(mouse);
            if (who != null)
            {
                if (who == Selected) { OrbitCamera.Instance.FocusOn(who.transform.position, 8f); return; }
                OpenPersonMenu(who, mouse);
                return;
            }

            var hits = Physics.RaycastAll(cam.ScreenPointToRay(mouse), 400f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                var piece = SeatOwner(h.collider.transform);
                if (piece != null) { OpenFurnitureMenu(piece, h.point, mouse); return; }
                Walk(h.point);
                return;
            }
        }

        static Furniture SeatOwner(Transform t)
        {
            foreach (var f in t.GetComponentsInParent<Furniture>())
                if (f.GetComponentInChildren<UseSpot>() != null) return f;
            return null;
        }

        void Walk(Vector3 point)
        {
            Selected.GiveOrder(new Character.Order { kind = Character.Order.Kind.Go, point = point, label = "Walking over" });
            markerAt = point; markerUntil = Time.unscaledTime + 0.9f;
        }

        void OpenMenu(Vector3 mouse, string heading)
        {
            pieOpen = true;
            pieTitle = heading;
            pieCenter = new Vector2(mouse.x, Screen.height - mouse.y) / scale;
            // keep the whole ring on screen
            float m = PieRadius + PieDisc * 0.5f + 6f;
            pieCenter.x = Mathf.Clamp(pieCenter.x, m, Screen.width / scale - m);
            pieCenter.y = Mathf.Clamp(pieCenter.y, m, Screen.height / scale - m);
        }

        void OpenPersonMenu(Character who, Vector3 mouse)
        {
            options.Clear();
            var me = Selected;
            if (who.isPet)
            {
                options.Add(new Option { label = "Pet " + who.displayName, enabled = true, act = () => me.GiveOrder(new Character.Order { kind = Character.Order.Kind.Pet, target = who, label = "Going to pet " + who.displayName }) });
            }
            else
            {
                bool free = who.CanChat;
                options.Add(new Option { label = "Talk to " + who.displayName, enabled = free, act = () => me.GiveOrder(new Character.Order { kind = Character.Order.Kind.Talk, target = who, label = "Going to talk to " + who.displayName }) });
                options.Add(new Option { label = "Play as " + who.displayName, enabled = true, act = () => Select(who) });
            }
            options.Add(new Option { label = "Go there", enabled = true, act = () => Walk(who.transform.position) });
            OpenMenu(mouse, who.displayName);
        }

        void OpenFurnitureMenu(Furniture piece, Vector3 hit, Vector3 mouse)
        {
            options.Clear();
            var me = Selected;
            UseSpot best = null; float bestD = float.MaxValue;
            foreach (var s in piece.GetComponentsInChildren<UseSpot>())
            {
                if (s.occupant != null && s.occupant != me) continue;
                float d = (s.transform.position - hit).sqrMagnitude;
                if (d < bestD) { bestD = d; best = s; }
            }
            string name = piece.name.Replace('_', ' ');
            if (best != null)
            {
                var spot = best;
                bool lie = spot.pose == CharacterRig.Pose.Lie;
                options.Add(new Option
                {
                    label = lie ? "Lie down" : "Sit down", enabled = true,
                    act = () => me.GiveOrder(new Character.Order { kind = Character.Order.Kind.Use, spot = spot, label = lie ? "Going to lie down" : "Going to sit" })
                });
            }
            else options.Add(new Option { label = "Somebody is using it", enabled = false, act = null });
            options.Add(new Option { label = "Go here", enabled = true, act = () => Walk(hit) });
            OpenMenu(mouse, name);
        }

        // ---------------------------------------------------------------- the green diamond

        void MakePlumbob()
        {
            var go = new GameObject("Plumbob");
            go.transform.SetParent(transform, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            if (plumbobMaterial) mr.sharedMaterial = plumbobMaterial;
            var mesh = new Mesh { name = "Plumbob" };
            const float w = 0.11f, h = 0.2f;
            var v = new[] { new Vector3(0, h, 0), new Vector3(0, -h, 0), new Vector3(w, 0, 0), new Vector3(0, 0, w), new Vector3(-w, 0, 0), new Vector3(0, 0, -w) };
            var t = new[] { 0, 3, 2, 0, 4, 3, 0, 5, 4, 0, 2, 5, 1, 2, 3, 1, 3, 4, 1, 4, 5, 1, 5, 2 };
            mesh.vertices = v; mesh.triangles = t; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            plumbob = go.transform;
        }

        void UpdatePlumbob()
        {
            if (!plumbob) return;
            bool show = Selected != null && Selected && !DecorateMode.Active;
            var r = plumbob.GetComponent<Renderer>();
            if (r) r.enabled = show && (Selected.GetComponentInChildren<Renderer>() == null || Selected.GetComponentInChildren<Renderer>().enabled);
            if (!show) return;
            float t = Time.unscaledTime;
            float top = Selected.transform.position.y + 1.7f * Selected.scale;
            foreach (var rr in Selected.GetComponentsInChildren<Renderer>()) if (rr.enabled) top = Mathf.Max(top, rr.bounds.max.y);
            top = Selected.Activity == "Lying down" ? Selected.transform.position.y + 1.2f : Mathf.Min(top, Selected.transform.position.y + 2.2f * Selected.scale);
            plumbob.position = new Vector3(Selected.transform.position.x, top + 0.3f + 0.06f * Mathf.Sin(t * 2.4f), Selected.transform.position.z);
            plumbob.rotation = Quaternion.Euler(0f, t * 70f, 0f);
        }

        // ---------------------------------------------------------------- drawing

        bool PieContains(Vector2 screenPoint)
        {
            if (!pieOpen) return false;
            return Vector2.Distance(screenPoint / scale, pieCenter) < PieRadius + PieDisc * 0.5f + 6f;
        }

        void Styles()
        {
            if (btn != null) return;
            disc = new Texture2D(96, 96, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 96; y++)
                for (int x = 0; x < 96; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(48f, 48f));
                    disc.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(47f - d)));
                }
            disc.Apply();
            discLabel = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            discLabel.normal.textColor = Color.white;
            tinyLabel = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleLeft };
            tinyLabel.normal.textColor = new Color(1f, 1f, 1f, 0.8f);
            title = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = new Color(1f, 1f, 1f, 0.95f);
            btn = new GUIStyle(GUI.skin.button) { fontSize = 13, fixedHeight = 44, padding = new RectOffset(10, 10, 4, 4), alignment = TextAnchor.MiddleLeft, wordWrap = true };
            btnOn = new GUIStyle(btn) { fontStyle = FontStyle.Bold };
            btnOn.normal.textColor = btnOn.hover.textColor = new Color(1f, 0.78f, 0.86f);
        }

        void OnGUI()
        {
            Styles();
            scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale, h = Screen.height / scale;

            // speed buttons, bottom right
            speedPanel = new Rect(w - 258, h - 98, 246, 44);
            GUILayout.BeginArea(speedPanel);
            GUILayout.BeginHorizontal();
            string[] names = { "Pause", "1x", "2x", "3x" };
            for (int i = 0; i < 4; i++)
                if (GUILayout.Button(names[i], i == Speed ? btnOn : btn, GUILayout.Width(58))) SetSpeed(i);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            if (DecorateMode.Active) { portraits = Rect.zero; return; }

            // the people, bottom left next to the main panel
            var people = new List<Character>();
            foreach (var c in Character.All) if (!c.isPet) people.Add(c);
            portraits = new Rect(214, h - 122, 176 * Mathf.Max(1, people.Count) + 6, 56);
            GUILayout.BeginArea(portraits);
            GUILayout.BeginHorizontal();
            foreach (var c in people)
            {
                string line = $"{c.displayName}\n{c.Activity}" + (c.Queued > 0 ? $" (+{c.Queued})" : "");
                if (GUILayout.Button(line, c == Selected ? btnOn : btn, GUILayout.Width(170)))
                {
                    if (c == Selected) OrbitCamera.Instance.FocusOn(c.transform.position, 8f);
                    Select(c);
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            if (Selected != null && (Selected.Queued > 0 || Selected.OnOrder))
            {
                if (GUI.Button(new Rect(portraits.xMax + 6, portraits.y + 6, 64, 40), "Stop", btn)) Selected.CancelOrders();
            }

            // where the last walk order went
            if (Time.unscaledTime < markerUntil && Camera.main)
            {
                var s = Camera.main.WorldToScreenPoint(markerAt);
                if (s.z > 0f)
                {
                    float k = (markerUntil - Time.unscaledTime) / 0.9f;
                    float r = (14f + 22f * (1f - k)) ;
                    GUI.color = new Color(1f, 0.6f, 0.8f, k);
                    GUI.DrawTexture(new Rect(s.x / scale - r, (Screen.height - s.y) / scale - r, r * 2f, r * 2f), disc);
                    GUI.color = Color.white;
                }
            }

            DrawPie();
        }

        void DrawPie()
        {
            if (!pieOpen || options.Count == 0) return;
            var e = Event.current;
            var centerRect = new Rect(pieCenter.x - 24, pieCenter.y - 24, 48, 48);
            GUI.color = new Color(0.1f, 0.09f, 0.13f, 0.88f);
            GUI.DrawTexture(centerRect, disc);
            GUI.color = Color.white;
            GUI.Label(new Rect(pieCenter.x - 90, pieCenter.y - PieRadius - PieDisc * 0.5f - 24, 180, 20), pieTitle, title);
            if (GUI.Button(centerRect, GUIContent.none, GUIStyle.none)) { pieOpen = false; return; }

            int n = options.Count;
            for (int i = 0; i < n; i++)
            {
                float a = (-90f + 360f * i / n) * Mathf.Deg2Rad;
                var c = pieCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * PieRadius;
                var r = new Rect(c.x - PieDisc * 0.5f, c.y - PieDisc * 0.5f, PieDisc, PieDisc);
                var o = options[i];
                bool hover = o.enabled && r.Contains(e.mousePosition);
                GUI.color = !o.enabled ? new Color(0.25f, 0.24f, 0.27f, 0.8f) : hover ? new Color(0.98f, 0.6f, 0.75f, 0.97f) : new Color(0.16f, 0.14f, 0.2f, 0.92f);
                GUI.DrawTexture(r, disc);
                GUI.color = Color.white;
                GUI.Label(new Rect(r.x + 8, r.y + 8, r.width - 16, r.height - 16), o.label, discLabel);
                if (o.enabled && GUI.Button(r, GUIContent.none, GUIStyle.none)) { var act = o.act; pieOpen = false; act?.Invoke(); return; }
            }
        }
    }
}
