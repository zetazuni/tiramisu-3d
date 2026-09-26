using System.Collections.Generic;
using UnityEngine;

namespace Tiramisu
{
    /// <summary>
    /// Decorate mode (key M or the HUD button). Press on a piece of furniture and drag it: it follows the
    /// surface under the mouse, so a mug can be carried from the island to the floor and a lamp can be put on
    /// a table. R turns it (Shift for the other way), Esc or right click puts it back, letting go drops it.
    /// A piece cannot be dropped inside a wall or another piece; it goes back to the last free spot instead.
    /// Layouts are saved automatically and come back next time.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class DecorateMode : MonoBehaviour
    {
        public static DecorateMode Instance { get; private set; }
        public static bool Active { get; private set; }

        public float snap = 0.05f;
        public float turnStep = 15f;

        Camera cam;
        Furniture held;
        Vector2 grabOffset;
        Vector3 startPos, lastValidPos;
        Quaternion startRot, lastValidRot;
        bool valid = true;
        string toast;
        float toastUntil;
        readonly List<(Rigidbody rb, bool kinematic)> frozen = new List<(Rigidbody, bool)>();
        // things standing on the held piece (cushions on a sofa, a mug on the island) ride along
        readonly List<(Furniture f, Vector3 localPos, Quaternion localRot, Vector3 startPos, Quaternion startRot)> riders =
            new List<(Furniture, Vector3, Quaternion, Vector3, Quaternion)>();
        static Texture2D white;

        public bool Holding => held != null || winWall != null;

        // a window being moved along its wall
        WindowWall winWall;
        int winIndex;
        float winStartCenter, winStartWidth;
        bool winValid = true;

        void Awake()
        {
            Instance = this;
            cam = Camera.main;
            white = Texture2D.whiteTexture;
        }

        void Start()
        {
            StartCoroutine(SettleWhenQuiet());
            int n = Furniture.LoadAll();
            if (n > 0) Say($"Welcome back, {n} piece{(n == 1 ? "" : "s")} of furniture are where you left them.");
        }

        /// <summary>Waits until the heavy pieces have come to rest (they push out of the floor a little at the start), then fixes the small things to them.</summary>
        System.Collections.IEnumerator SettleWhenQuiet()
        {
            yield return new WaitForSeconds(1.2f);
            Furniture.SettleSmallThings();
            Furniture.AttachSmallThings();
        }

        public void Toggle()
        {
            if (Active) { Drop(false); DropWindow(false); }
            Active = !Active;
            Say(Active ? "Decorate mode: drag furniture around, R turns it, Esc puts it back." : "Decorate mode is off.");
        }

        public void ResetLayout()
        {
            Drop(false);
            DropWindow(true);
            Furniture.ResetAll();
            WindowWall.ResetAll();
            Say("Everything is back where it started, standing upright.");
        }

        void Say(string t) { toast = t; toastUntil = Time.time + 4f; }

        void OnDisable() { OrbitCamera.Blocked = false; Active = false; }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.M)) Toggle();
            if (!Active) { OrbitCamera.Blocked = false; return; }
            if (!cam) cam = Camera.main;

            if (winWall != null) { WindowUpdate(); return; }

            if (held == null)
            {
                if (Input.GetMouseButtonDown(0) && !(OrbitCamera.IsOverUi != null && OrbitCamera.IsOverUi(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y))))
                    TryGrab();
                return;
            }

            OrbitCamera.Blocked = true;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) { Drop(true); return; }
            if (Input.GetKeyDown(KeyCode.R))
            {
                float step = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -turnStep : turnStep;
                held.transform.Rotate(0f, step, 0f, Space.World);
                MoveRiders();
            }
            if (!Input.GetMouseButton(0)) { Drop(false); return; }
            Drag();
        }

        // ---------- picking ----------

        void TryGrab()
        {
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            var hits = Physics.RaycastAll(ray, 300f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                var part = h.collider.GetComponent<WallWindowPart>();
                if (part) { StartWindow(part); return; }
                if (IsArchitecture(h.collider)) continue;   // walls cut down or glass never block the pick
                var f = h.collider.GetComponentInParent<Furniture>();
                if (!f) return;
                while (f.attachedTo) f = f.attachedTo;   // small things are part of what they sit on                              // something solid that is not furniture is in front
                if (f.pinned) { Say($"The {f.Label.ToLower()} is built in, it cannot be moved."); return; }
                Grab(f, h.point);
                return;
            }
        }

        public Furniture Selected { get; private set; }

        /// <summary>Turns the last piece you picked up by the given angle (the HUD buttons). Refuses if it would hit something.</summary>
        public void RotateSelected(float deg)
        {
            if (Selected == null || held != null || winWall != null) return;
            Grab(Selected, Selected.transform.position);
            held.transform.Rotate(0f, deg, 0f, Space.World);
            MoveRiders();
            Physics.SyncTransforms();
            valid = IsFree(held.transform.position);
            lastValidPos = held.transform.position;
            lastValidRot = held.transform.rotation;
            if (!valid) Say("There is no room to turn it here.");
            Drop(!valid);
        }

        void Grab(Furniture f, Vector3 point)
        {
            held = f;
            Selected = f;
            OrbitCamera.Blocked = true;
            startPos = lastValidPos = f.transform.position;
            startRot = lastValidRot = f.transform.rotation;
            grabOffset = new Vector2(f.transform.position.x - point.x, f.transform.position.z - point.z);
            valid = true;
            frozen.Clear();
            riders.Clear();
            // everything else is fixed while this piece is carried, so nothing gets pushed when it touches other things
            foreach (var other in UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude))
            {
                if (other.transform.IsChildOf(f.transform) || other.isKinematic) continue;
                frozen.Add((other, other.isKinematic));
                other.linearVelocity = Vector3.zero;
                other.angularVelocity = Vector3.zero;
                other.isKinematic = true;
            }
            Freeze(f);
            var lb = f.LocalBounds;
            foreach (var o in Furniture.All)
            {
                if (o == f || o.pinned || o.attachedTo) continue;
                var l = f.transform.InverseTransformPoint(o.transform.position);
                bool on = Mathf.Abs(l.x - lb.center.x) < lb.extents.x && Mathf.Abs(l.z - lb.center.z) < lb.extents.z && l.y > 0.05f && l.y < lb.max.y + 0.06f;
                if (!on) continue;
                riders.Add((o, l, Quaternion.Inverse(f.transform.rotation) * o.transform.rotation, o.transform.position, o.transform.rotation));
                Freeze(o);
            }
        }

        void Freeze(Furniture f)
        {
            foreach (var rb in f.GetComponentsInChildren<Rigidbody>())
            {
                frozen.Add((rb, rb.isKinematic));
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
        }

        void MoveRiders()
        {
            foreach (var r in riders)
                r.f.transform.SetPositionAndRotation(held.transform.TransformPoint(r.localPos), held.transform.rotation * r.localRot);
        }

        static bool IsArchitecture(Collider c)
        {
            for (var t = c.transform; t != null; t = t.parent)
                if (t.name == "Walls" || t.name == "Roof") return true;
            return false;
        }

        // ---------- windows ----------

        void StartWindow(WallWindowPart part)
        {
            winWall = part.wall;
            winIndex = part.index;
            winStartCenter = winWall.windows[winIndex].center;
            winStartWidth = winWall.windows[winIndex].width;
            winValid = true;
            OrbitCamera.Blocked = true;
        }

        void WindowUpdate()
        {
            OrbitCamera.Blocked = true;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) { DropWindow(true); return; }
            if (Input.GetKeyDown(KeyCode.R))
            {
                bool back = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (!winWall.CycleWidth(winIndex, back ? -1 : 1)) Say("That size does not fit here.");
            }
            if (!Input.GetMouseButton(0)) { DropWindow(false); return; }
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (winWall.WallPlane().Raycast(ray, out float e))
            {
                float u = winWall.AlongCoordinate(ray.GetPoint(e));
                u = Mathf.Round(u / 0.1f) * 0.1f;
                winValid = winWall.TryMove(winIndex, u);
            }
        }

        void DropWindow(bool cancel)
        {
            if (winWall == null) return;
            var w = winWall.windows[winIndex];
            if (cancel)
            {
                w.width = winStartWidth;
                w.center = winStartCenter;
                winWall.Rebuild();
            }
            winWall = null;
            OrbitCamera.Blocked = false;
            WindowWall.SaveAll();
        }

        // ---------- dragging ----------

        void Drag()
        {
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            var hits = Physics.RaycastAll(ray, 300f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            bool found = false;
            Vector3 p = default;
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(held.transform)) continue;
                if (IsArchitecture(h.collider)) continue;
                if (h.normal.y < 0.6f) continue;             // only surfaces you could put something on
                p = h.point; found = true; break;
            }
            if (!found) return;

            bool free = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            float x = p.x + grabOffset.x, z = p.z + grabOffset.y;
            if (!free) { x = Mathf.Round(x / snap) * snap; z = Mathf.Round(z / snap) * snap; }
            x = Mathf.Clamp(x, -0.5f, 32f);
            z = Mathf.Clamp(z, -0.5f, 24f);
            // the piece's own origin sits on its base, so it rests exactly on the surface
            var target = new Vector3(x, p.y + 0.003f, z);
            held.transform.position = Vector3.Lerp(held.transform.position, target, 1f - Mathf.Exp(-30f * Time.unscaledDeltaTime));
            MoveRiders();
            valid = IsFree(target);
            if (valid) { lastValidPos = target; lastValidRot = held.transform.rotation; }
        }

        /// <summary>True when the piece fits at this spot: no wall or other piece inside it.</summary>
        bool IsFree(Vector3 at)
        {
            var lb = held.LocalBounds;
            var rot = held.transform.rotation;
            Vector3 centre = at + rot * lb.center;
            Vector3 half = lb.extents;
            half = new Vector3(Mathf.Max(half.x - 0.03f, 0.01f), Mathf.Max(half.y - 0.06f, 0.01f), Mathf.Max(half.z - 0.03f, 0.01f));
            centre.y += 0.02f;
            float baseY = at.y;
            foreach (var c in Physics.OverlapBox(centre, half, rot, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c.transform.IsChildOf(held.transform)) continue;
                if (c.bounds.max.y <= baseY + 0.045f) continue;   // the surface it stands on
                var other = c.GetComponentInParent<Furniture>();
                if (other != null && IsRider(other)) continue;    // things riding on this piece
                return false;
            }
            return true;
        }

        bool IsRider(Furniture o)
        {
            foreach (var r in riders) if (r.f == o) return true;
            return false;
        }

        void Drop(bool cancel)
        {
            if (held == null) return;
            var f = held;
            held = null;
            OrbitCamera.Blocked = false;
            if (cancel) f.Place(startPos, startRot);
            else if (!valid) { f.Place(lastValidPos, lastValidRot); Say("There is no room for that here."); }
            else f.Place(lastValidPos, f.transform.rotation);
            // the riders follow wherever the piece ended up (or go back to where they were)
            held = f;
            foreach (var r in riders)
            {
                if (cancel) r.f.Place(r.startPos, r.startRot);
                else r.f.Place(f.transform.TransformPoint(r.localPos), f.transform.rotation * r.localRot);
            }
            held = null;
            riders.Clear();
            foreach (var (rb, kin) in frozen) if (rb) rb.isKinematic = kin;
            frozen.Clear();
            Physics.SyncTransforms();
            Furniture.SaveAll();
        }

        // ---------- feedback ----------

        void OnGUI()
        {
            if (!Active && Time.time > toastUntil) return;
            float scale = Mathf.Max(1f, Screen.height / 900f);
            var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(14 * scale), alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Color.white;

            if (held != null && cam)
            {
                var lb = held.LocalBounds;
                var col = valid ? new Color(0.45f, 1f, 0.6f, 0.95f) : new Color(1f, 0.35f, 0.35f, 0.95f);
                var t = held.transform;
                float y = lb.min.y + 0.01f;
                Vector2[] c = new Vector2[4];
                Vector3[] w =
                {
                    t.TransformPoint(new Vector3(lb.min.x, y, lb.min.z)), t.TransformPoint(new Vector3(lb.max.x, y, lb.min.z)),
                    t.TransformPoint(new Vector3(lb.max.x, y, lb.max.z)), t.TransformPoint(new Vector3(lb.min.x, y, lb.max.z)),
                };
                for (int i = 0; i < 4; i++)
                {
                    var s = cam.WorldToScreenPoint(w[i]);
                    if (s.z < 0f) return;
                    c[i] = new Vector2(s.x, Screen.height - s.y);
                }
                for (int i = 0; i < 4; i++) Line(c[i], c[(i + 1) % 4], col, 3f * scale);
                var top = cam.WorldToScreenPoint(t.TransformPoint(new Vector3(lb.center.x, lb.max.y, lb.center.z)));
                var r = new Rect(top.x - 100 * scale, Screen.height - top.y - 34 * scale, 200 * scale, 26 * scale);
                GUI.Label(r, held.Label, style);
            }

            if (winWall != null && cam)
            {
                var wc = winWall.Corners(winIndex);
                var col = winValid ? new Color(0.45f, 1f, 0.6f, 0.95f) : new Color(1f, 0.35f, 0.35f, 0.95f);
                var sp = new Vector2[4];
                bool visible = true;
                for (int i = 0; i < 4; i++)
                {
                    var s = cam.WorldToScreenPoint(wc[i]);
                    if (s.z < 0f) visible = false;
                    sp[i] = new Vector2(s.x, Screen.height - s.y);
                }
                if (visible)
                {
                    for (int i = 0; i < 4; i++) Line(sp[i], sp[(i + 1) % 4], col, 3f * scale);
                    GUI.Label(new Rect(sp[3].x - 100 * scale, sp[3].y - 30 * scale, 200 * scale, 26 * scale), "Window (R changes the size)", style);
                }
            }

            if (Time.time < toastUntil)
                GUI.Label(new Rect(0, Screen.height - 96 * scale, Screen.width, 30 * scale), toast, style);
        }

        static void Line(Vector2 a, Vector2 b, Color c, float width)
        {
            var saved = GUI.matrix;
            var d = b - a;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(ang, a);
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(a.x, a.y - width / 2f, d.magnitude, width), white);
            GUI.color = old;
            GUI.matrix = saved;
        }
    }
}
