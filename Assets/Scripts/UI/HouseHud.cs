using UnityEngine;

namespace Tiramisu
{
    /// <summary>
    /// Temporary on-screen controls for the greybox stage: floor switch, room jumps,
    /// wall mode and fit. Will be replaced by the proper UI later.
    /// </summary>
    public class HouseHud : MonoBehaviour
    {
        Rect panel, timePanel;
        GUIStyle btn, btnOn, label, hint, version;
        float scale = 1f;

        void Awake()
        {
            OrbitCamera.IsOverUi = p => panel.Contains(p / scale) || timePanel.Contains(p / scale);
        }

        void Styles()
        {
            if (btn != null) return;
            btn = new GUIStyle(GUI.skin.button) { fontSize = 15, fixedHeight = 36, padding = new RectOffset(12, 12, 6, 6) };
            btnOn = new GUIStyle(btn) { fontStyle = FontStyle.Bold };
            btnOn.normal.textColor = btnOn.hover.textColor = new Color(1f, 0.78f, 0.86f);
            label = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            label.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
            hint = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.LowerCenter };
            hint.normal.textColor = new Color(1f, 1f, 1f, 0.8f);
            version = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.LowerRight };
            version.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
        }

        bool Button(string text, bool on) => GUILayout.Button(text, on ? btnOn : btn);

        void OnGUI()
        {
            Styles();
            scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale, h = Screen.height / scale;

            var view = HouseView.Instance;
            var cam = OrbitCamera.Instance;
            if (!view || !cam) return;

            panel = new Rect(12, 12, 190, h - 24);
            GUILayout.BeginArea(panel);

            GUILayout.Label("Floors", label);
            if (Button("Ground floor", view.view == HouseView.View.Ground)) view.SetView(HouseView.View.Ground);
            if (Button("Upper floor", view.view == HouseView.View.Upper)) view.SetView(HouseView.View.Upper);
            if (Button("Whole house", view.view == HouseView.View.Whole)) view.SetView(HouseView.View.Whole);

            GUILayout.Space(10);
            GUILayout.Label("Rooms", label);
            foreach (var r in RoomMarker.All)
            {
                if (r.floor != view.ActiveFloor) continue;
                if (Button(r.displayName, false))
                {
                    if (view.view == HouseView.View.Whole) view.SetView(HouseView.View.Upper, false);
                    cam.FocusOn(r.transform.position, r.ViewDistance);
                }
            }

            GUILayout.Space(10);
            GUILayout.Label("View", label);
            string walls = view.wallMode == HouseView.WallMode.Auto ? "Walls: automatic"
                         : view.wallMode == HouseView.WallMode.Up ? "Walls: always up" : "Walls: always down";
            if (Button(walls, false)) view.CycleWallMode();
            if (Button("See the whole house", false)) cam.FitHouse(view.ActiveFloorY);
            var gfx = GraphicsModes.Instance;
            if (gfx && Button("Graphics: " + gfx.Label, false)) gfx.Apply((GraphicsModes.Mode)(((int)gfx.mode + 1) % 3));

            var dec = DecorateMode.Instance;
            if (dec)
            {
                GUILayout.Space(10);
                GUILayout.Label("Furniture", label);
                if (Button(DecorateMode.Active ? "Decorate mode: on (M)" : "Move furniture (M)", DecorateMode.Active)) dec.Toggle();
                if (DecorateMode.Active && Button("Put everything back", false)) dec.ResetLayout();
            }

            GUILayout.EndArea();

            var day = DayNightCycle.Instance;
            if (day)
            {
                timePanel = new Rect(w - 262, 12, 250, 150);
                GUILayout.BeginArea(timePanel);
                GUILayout.Label($"{day.Phase} · {day.Clock}", label);
                float nh = GUILayout.HorizontalSlider(day.hour, 0f, 24f);
                if (Mathf.Abs(nh - day.hour) > 0.001f) day.SetHour(nh);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Morning", btn)) day.SetHour(8f);
                if (GUILayout.Button("Noon", btn)) day.SetHour(13f);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Sunset", btn)) day.SetHour(18.5f);
                if (GUILayout.Button("Night", btn)) day.SetHour(22.5f);
                GUILayout.EndHorizontal();
                if (Button(day.auto ? "Time is running" : "Let time run", day.auto)) day.auto = !day.auto;
                GUILayout.EndArea();
            }

            GUI.Label(new Rect(0, h - 58, w, 30),
                "Drag to spin around · Right drag to move · Scroll to zoom · Q/E spin · 1 2 3 floors · G graphics · M move furniture · Click things to nudge them", hint);
            GUI.Label(new Rect(w - 220, h - 26, 210, 22), $"Tiramisu 3D v{GameInfo.Version} · {GameInfo.BuildDate}", version);
        }
    }
}
