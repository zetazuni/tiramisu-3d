using System.Collections.Generic;
using UnityEngine;

namespace Tiramisu
{
    /// <summary>
    /// The Sims style panels: household funds and the date at the top, the needs and mood of the person you are playing at the bottom left,
    /// the status window (key C: mood, feelings, traits, skills, wishes, friends, money) and the messages at the bottom.
    /// The little icons are drawn in code.
    /// </summary>
    public class SimUi : MonoBehaviour
    {
        public static SimUi Instance { get; private set; }
        public static bool StatusOpen { get; private set; }

        Rect topBar, needsPanel, statusPanel;
        float scale = 1f;
        Texture2D white;
        readonly Texture2D[] icons = new Texture2D[6];
        GUIStyle label, bold, small, big, btn;
        static readonly Color[] NeedColours =
        {
            new Color(0.95f, 0.65f, 0.3f), new Color(0.4f, 0.75f, 0.95f), new Color(0.6f, 0.55f, 0.95f),
            new Color(0.98f, 0.8f, 0.3f), new Color(0.95f, 0.5f, 0.7f), new Color(0.4f, 0.9f, 0.8f),
        };

        public static void OpenStatus() { StatusOpen = true; GameAudio.Play(GameAudio.Sfx.Click); }

        public bool OverPanels(Vector2 p) => topBar.Contains(p / scale) || needsPanel.Contains(p / scale) || (StatusOpen && statusPanel.Contains(p / scale));

        void Awake() { Instance = this; }

        void Start()
        {
            white = Texture2D.whiteTexture;
            for (int i = 0; i < 6; i++) icons[i] = MakeIcon(i);
            var previous = OrbitCamera.IsOverUi;
            OrbitCamera.IsOverUi = p => (previous != null && previous(p)) || OverPanels(p);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.C)) { StatusOpen = !StatusOpen; GameAudio.Play(GameAudio.Sfx.Click); }
            if (StatusOpen && Input.GetKeyDown(KeyCode.Escape)) StatusOpen = false;
        }

        // ------------------------------------------------------------ icons

        static Texture2D MakeIcon(int kind)
        {
            const int N = 32;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                    float a = 0f;
                    switch (kind)
                    {
                        case 0: // hunger: an apple
                            a = Mathf.Max(Circle(u + 0.3f, v + 0.1f, 0.55f), Circle(u - 0.3f, v + 0.1f, 0.55f));
                            a = Mathf.Max(a, Circle(u - 0.35f, v - 0.75f, 0.24f) * (v > 0.5f ? 1f : 0f)) * (Mathf.Abs(u) < 0.06f && v > 0.5f ? 0.4f : 1f);
                            break;
                        case 1: // bladder: a drop
                            a = Mathf.Max(Circle(u, v + 0.25f, 0.55f), Mathf.Clamp01(1f - (Mathf.Abs(u) * 2.4f + (0.75f - v) * 0.5f)) * (v > -0.2f ? 1f : 0f) * ((0.85f - v) > Mathf.Abs(u) * 2.6f ? 1f : 0f));
                            break;
                        case 2: // energy: a bolt
                            a = Bolt(u, v);
                            break;
                        case 3: // fun: a star
                            a = Star(u, v);
                            break;
                        case 4: // social: two heads and shoulders
                            a = Mathf.Max(Mathf.Max(Circle(u + 0.42f, v - 0.35f, 0.3f), Circle(u - 0.42f, v - 0.35f, 0.3f)), Mathf.Max(Circle(u + 0.42f, v + 0.75f, 0.6f) * (v < 0.05f ? 1f : 0f), Circle(u - 0.42f, v + 0.75f, 0.6f) * (v < 0.05f ? 1f : 0f)));
                            break;
                        default: // hygiene: bubbles
                            a = Mathf.Max(Ring(u + 0.35f, v + 0.25f, 0.42f), Mathf.Max(Ring(u - 0.42f, v - 0.2f, 0.32f), Ring(u + 0.05f, v - 0.62f, 0.22f)));
                            break;
                    }
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
                }
            t.Apply();
            return t;
        }

        static float Circle(float x, float y, float r) => Mathf.Clamp01((r - Mathf.Sqrt(x * x + y * y)) * 14f);
        static float Ring(float x, float y, float r) => Mathf.Clamp01((0.12f - Mathf.Abs(Mathf.Sqrt(x * x + y * y) - r)) * 14f);
        static float Bolt(float x, float y)
        {
            // a zig zag: two triangles
            float a = 0f;
            if (y > -0.05f && y < 0.95f && x > -0.45f + (0.95f - y) * 0.15f - 0.3f && x < 0.15f + (0.95f - y) * 0.15f) a = 1f;
            if (y < 0.1f && y > -0.95f && x > -0.15f - (y + 0.95f) * 0.1f && x < 0.45f - (y + 0.95f) * 0.1f + 0.05f) a = 1f;
            return a;
        }
        static float Star(float x, float y)
        {
            float ang = Mathf.Atan2(y, x), r = Mathf.Sqrt(x * x + y * y);
            float lim = 0.42f + 0.5f * Mathf.Pow(Mathf.Abs(Mathf.Cos(2.5f * (ang + Mathf.PI / 2f))), 1.6f);
            return Mathf.Clamp01((lim - r) * 14f);
        }

        // ------------------------------------------------------------ drawing helpers

        void Styles()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 12 }; label.normal.textColor = Color.white;
            bold = new GUIStyle(label) { fontStyle = FontStyle.Bold, fontSize = 13 };
            small = new GUIStyle(label) { fontSize = 11 }; small.normal.textColor = new Color(1f, 1f, 1f, 0.8f);
            big = new GUIStyle(label) { fontSize = 20, fontStyle = FontStyle.Bold };
            btn = new GUIStyle(GUI.skin.button) { fontSize = 12, padding = new RectOffset(8, 8, 3, 3) };
        }

        void Fill(Rect r, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = o; }

        void Panel(Rect r) { Fill(r, new Color(0.08f, 0.07f, 0.1f, 0.82f)); }

        void Bar(Rect r, float value01, Color c)
        {
            Fill(r, new Color(1f, 1f, 1f, 0.14f));
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(value01), r.height), value01 < 0.22f ? Color.Lerp(c, new Color(0.95f, 0.3f, 0.3f), 0.7f) : c);
        }

        // ------------------------------------------------------------ the screen

        void OnGUI()
        {
            Styles();
            scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale, h = Screen.height / scale;

            // ---- top bar: money, date, bills
            topBar = new Rect(w * 0.5f - 250, 8, 500, 34);
            Panel(topBar);
            var hh = Household.Instance;
            var dn = DayNightCycle.Instance; var sea = SeasonCycle.Instance;
            GUI.Label(new Rect(topBar.x + 12, topBar.y + 6, 150, 24), $"{Household.Currency} {(hh ? hh.Funds : 0):N0}", bold);
            string date = dn ? $"Day {dn.DayCount + 1} · {(sea ? sea.Label : "")} · {dn.Clock}" : "";
            GUI.Label(new Rect(topBar.x + 150, topBar.y + 6, 220, 24), date, label);
            if (hh) GUI.Label(new Rect(topBar.x + 340, topBar.y + 6, 150, 24), $"Bills day {hh.NextBillDay + 1}", small);

            // ---- needs of the person you play
            var who = LiveMode.Selected;
            if (who != null && who.sim != null && !DecorateMode.Active)
            {
                var sim = who.sim;
                needsPanel = new Rect(214, h - 122 - 226, 240, 220);
                Panel(needsPanel);
                float y = needsPanel.y + 6;
                Fill(new Rect(needsPanel.x + 8, y + 2, 18, 18), sim.MoodColour);
                GUI.Label(new Rect(needsPanel.x + 32, y, 130, 22), $"{who.displayName} · {sim.MoodName}", bold);
                if (GUI.Button(new Rect(needsPanel.xMax - 70, y, 62, 20), "Status", btn)) { StatusOpen = true; GameAudio.Play(GameAudio.Sfx.Click); }
                y += 26;
                for (int i = 0; i < 6; i++)
                {
                    var o = GUI.color; GUI.color = NeedColours[i]; GUI.DrawTexture(new Rect(needsPanel.x + 8, y - 1, 16, 16), icons[i]); GUI.color = o;
                    GUI.Label(new Rect(needsPanel.x + 28, y - 3, 70, 18), ((Need)i).ToString(), small);
                    Bar(new Rect(needsPanel.x + 96, y + 2, 132, 8), sim.needs[i] / 100f, NeedColours[i]);
                    y += 19;
                }
                y += 4;
                GUI.Label(new Rect(needsPanel.x + 8, y, 224, 16), "Wishes", bold);
                y += 18;
                foreach (var wish in sim.wishes) { GUI.Label(new Rect(needsPanel.x + 8, y, 226, 16), "· " + wish.text, small); y += 15; }
            }
            else needsPanel = Rect.zero;

            if (StatusOpen && who != null && who.sim != null) DrawStatus(who, w, h);
            else statusPanel = Rect.zero;

            // ---- messages
            string msg = Household.CurrentToast;
            if (!string.IsNullOrEmpty(msg))
            {
                var r = new Rect(w * 0.5f - 300, h - 132, 600, 30);
                Panel(r);
                var st = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
                GUI.Label(r, msg, st);
            }
        }

        // ------------------------------------------------------------ the status window

        void DrawStatus(Character who, float w, float h)
        {
            var sim = who.sim;
            statusPanel = new Rect(w * 0.5f - 340, h * 0.5f - 250, 680, 500);
            Panel(statusPanel);
            var x0 = statusPanel.x + 16; float y = statusPanel.y + 12;
            GUI.Label(new Rect(x0, y, 400, 30), $"{who.displayName}", big);
            var st = new GUIStyle(label) { alignment = TextAnchor.MiddleRight };
            GUI.Label(new Rect(statusPanel.xMax - 300, y + 4, 240, 22), $"Mood: {sim.MoodName} ({Mathf.RoundToInt(sim.Mood)})", st);
            if (GUI.Button(new Rect(statusPanel.xMax - 44, y + 2, 30, 24), "X", btn)) StatusOpen = false;
            Fill(new Rect(x0, y + 34, 648, 6), sim.MoodColour);
            y += 46;

            // people you can look at
            float px = x0;
            foreach (var c in Character.All)
                if (!c.isPet)
                {
                    if (GUI.Button(new Rect(px, y, 90, 22), c.displayName, btn)) LiveMode.Select(c);
                    px += 96;
                }
            y += 30;

            // left column: needs, feelings, traits
            float colW = 310;
            GUI.Label(new Rect(x0, y, colW, 18), "Needs", bold);
            float yy = y + 20;
            for (int i = 0; i < 6; i++)
            {
                if (sim.isPet && (i == (int)Need.Bladder || i == (int)Need.Hygiene || i == (int)Need.Social)) continue;
                var o = GUI.color; GUI.color = NeedColours[i]; GUI.DrawTexture(new Rect(x0, yy - 1, 16, 16), icons[i]); GUI.color = o;
                GUI.Label(new Rect(x0 + 22, yy - 2, 70, 18), ((Need)i).ToString(), small);
                Bar(new Rect(x0 + 92, yy + 3, 170, 8), sim.needs[i] / 100f, NeedColours[i]);
                GUI.Label(new Rect(x0 + 268, yy - 2, 40, 18), Mathf.RoundToInt(sim.needs[i]).ToString(), small);
                yy += 20;
            }
            yy += 6;
            GUI.Label(new Rect(x0, yy, colW, 18), "Feelings", bold); yy += 20;
            foreach (var f in sim.Feelings()) { GUI.Label(new Rect(x0, yy, colW, 16), $"{(f.value >= 0 ? "+" : "")}{Mathf.RoundToInt(f.value)}  {f.text}", small); yy += 15; if (yy > statusPanel.yMax - 90) break; }
            yy = Mathf.Max(yy, y + 20 + 6 * 20 + 6 + 20 + 15 * 3) + 6;
            GUI.Label(new Rect(x0, statusPanel.yMax - 76, colW, 18), "Traits", bold);
            GUI.Label(new Rect(x0, statusPanel.yMax - 58, colW, 32), string.Join(" · ", sim.traits), label);
            if (sim.job != "")
                GUI.Label(new Rect(x0, statusPanel.yMax - 36, colW, 22), $"Job: {sim.job} (level {1 + Mathf.FloorToInt(sim.jobXp / 300f)}), pays {Household.Currency} {Household.PayPerSecond(sim)} a second", small);

            // right column: skills, wishes, friends
            float rx = x0 + colW + 24; float ry = y;
            GUI.Label(new Rect(rx, ry, 300, 18), "Skills", bold); ry += 20;
            foreach (Skill s in System.Enum.GetValues(typeof(Skill)))
            {
                GUI.Label(new Rect(rx, ry - 2, 90, 18), s.ToString(), small);
                Bar(new Rect(rx + 92, ry + 3, 150, 8), sim.LevelProgress(s), new Color(0.55f, 0.85f, 0.6f));
                GUI.Label(new Rect(rx + 248, ry - 2, 60, 18), $"Level {sim.Level(s)}", small);
                ry += 20;
            }
            ry += 8;
            GUI.Label(new Rect(rx, ry, 300, 18), $"Wishes ({sim.wishesDone} come true)", bold); ry += 20;
            foreach (var wish in sim.wishes) { GUI.Label(new Rect(rx, ry, 310, 16), $"· {wish.text}  (+{Household.Currency} {wish.reward})", small); ry += 16; }
            ry += 8;
            GUI.Label(new Rect(rx, ry, 300, 18), "Friends", bold); ry += 20;
            foreach (var kv in sim.friendship)
            {
                if (kv.Key == who.displayName) continue;
                GUI.Label(new Rect(rx, ry - 2, 90, 18), kv.Key, small);
                Bar(new Rect(rx + 92, ry + 3, 150, 8), kv.Value / 100f, new Color(0.95f, 0.5f, 0.7f));
                GUI.Label(new Rect(rx + 248, ry - 2, 60, 18), Mathf.RoundToInt(kv.Value).ToString(), small);
                ry += 20;
            }
            ry += 8;
            var hh = Household.Instance;
            if (hh != null)
            {
                GUI.Label(new Rect(rx, ry, 300, 18), "Recent money", bold); ry += 20;
                int n = 0;
                foreach (var l in hh.Ledger) { GUI.Label(new Rect(rx, ry, 310, 16), $"{(l.amount >= 0 ? "+" : "")}{l.amount}  {l.text}", small); ry += 15; if (++n >= 5) break; }
            }
            GUI.Label(new Rect(statusPanel.x + 16, statusPanel.yMax - 20, 640, 16), "C or Esc closes this window", small);
        }
    }
}
