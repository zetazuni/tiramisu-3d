using UnityEngine;

namespace Tiramisu
{
    /// <summary>A few warm hints for the first minutes (F1 shows them again). Skipping is remembered.</summary>
    public class Tutorial : MonoBehaviour
    {
        const string Key = "tiramisu.tutorial";

        static readonly string[] Steps =
        {
            "Welcome home! Click Amir or Athirah in the panel at the bottom (or in the house) to play as them. The little atom over their head shows who you are playing.",
            "Click the floor and they walk there. Click furniture, a person or the pet (or right click) for a round menu of things to do: sit, cook, swim, work out, watch TV, chat.",
            "The bars at the bottom left are their needs: hunger, bathroom, energy, fun, friends and hygiene. Keep them happy. Press C for the full status: skills, wishes, friends.",
            "The money at the top pays for things and comes from work (the desks), wishes that come true and selling. Bills come every few days, and nothing bad ever happens if you cannot pay them yet.",
            "B opens the shop, V lets you build walls, rooms, doors and windows and paint everything, M moves furniture. Space pauses, 1 2 3 change the speed, Page Up and Page Down change floors.",
            "The seasons change as time passes (or press Season). F2 mutes the sound, N turns the music off. F1 shows these hints again. Have a lovely time together!",
        };

        int step;
        bool open;
        GUIStyle text, btn;
        Rect box;

        void Start()
        {
            open = PlayerPrefs.GetInt(Key, 0) == 0;
            if (open) Invoke(nameof(Ready), 2f); else enabled = true;
        }

        void Ready() { }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) { open = true; step = 0; }
        }

        void OnGUI()
        {
            if (!open) { box = Rect.zero; return; }
            if (text == null)
            {
                text = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
                text.normal.textColor = Color.white;
                btn = new GUIStyle(GUI.skin.button) { fontSize = 13, padding = new RectOffset(10, 10, 4, 4) };
            }
            float scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale;
            box = new Rect(w * 0.5f - 320, 54, 640, 96);
            var c = GUI.color; GUI.color = new Color(0.1f, 0.08f, 0.12f, 0.92f); GUI.DrawTexture(box, Texture2D.whiteTexture); GUI.color = c;
            GUI.Label(new Rect(box.x + 14, box.y + 8, box.width - 28, 60), Steps[step], text);
            GUI.Label(new Rect(box.x + 14, box.yMax - 26, 200, 20), $"{step + 1} of {Steps.Length}", text);
            if (GUI.Button(new Rect(box.xMax - 240, box.yMax - 30, 110, 24), "Skip hints", btn)) { open = false; PlayerPrefs.SetInt(Key, 1); GameAudio.Play(GameAudio.Sfx.Click); }
            if (GUI.Button(new Rect(box.xMax - 120, box.yMax - 30, 106, 24), step < Steps.Length - 1 ? "Next" : "Got it", btn))
            {
                GameAudio.Play(GameAudio.Sfx.Click);
                if (step < Steps.Length - 1) step++; else { open = false; PlayerPrefs.SetInt(Key, 1); }
            }
        }

        public bool OverBox(Vector2 p) => open && box.Contains(p / Mathf.Max(1f, Screen.height / 900f));
    }
}
