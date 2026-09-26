using UnityEngine;

namespace Tiramisu
{
    /// <summary>Name tags over the people and pets, and a speech bubble while somebody is talking.</summary>
    public class CharacterHud : MonoBehaviour
    {
        GUIStyle tag, bubble;

        void Styles()
        {
            if (tag != null) return;
            tag = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            tag.normal.textColor = new Color(1f, 1f, 1f, 0.92f);
            bubble = new GUIStyle(GUI.skin.box) { fontSize = 13, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            bubble.normal.textColor = Color.white;
            bubble.fontStyle = FontStyle.Bold;
            var bg = new Texture2D(1, 1); bg.SetPixel(0, 0, new Color(0.08f, 0.06f, 0.09f, 0.82f)); bg.Apply();
            bubble.normal.background = bg;
            tag.normal.textColor = Color.white;
        }

        void OnGUI()
        {
            var cam = Camera.main;
            if (!cam) return;
            Styles();
            float scale = Mathf.Max(1f, Screen.height / 900f);
            foreach (var c in Character.All)
            {
                if (string.IsNullOrEmpty(c.displayName)) continue;
                var r = c.GetComponentInChildren<Renderer>();
                if (r && !r.enabled) continue;
                var world = c.transform.position + Vector3.up * ((c.isPet ? 0.7f : 2.0f) * c.scale + 0.15f);
                var s = cam.WorldToScreenPoint(world);
                if (s.z < 0f) continue;
                float y = Screen.height - s.y;
                GUI.Label(new Rect(s.x - 60 * scale, y - 12 * scale, 120 * scale, 22 * scale), c.displayName, tag);
                if (c.Speaking && !string.IsNullOrEmpty(c.Bubble))
                {
                    var rect = new Rect(s.x - 90 * scale, y - 64 * scale, 180 * scale, 44 * scale);
                    GUI.Box(rect, c.Bubble, bubble);
                }
            }
        }
    }
}
