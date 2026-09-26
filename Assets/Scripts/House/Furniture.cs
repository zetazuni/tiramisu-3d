using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tiramisu
{
    /// <summary>
    /// Marks a piece the player can pick up and move in decorate mode. The builder adds it to every piece it
    /// places and gives it a stable key ("sofa#0"), which is what the saved layout is filed under.
    /// </summary>
    public class Furniture : MonoBehaviour
    {
        public static readonly List<Furniture> All = new List<Furniture>();

        [Tooltip("Stable id used in the saved layout, made by the builder.")]
        public string key;
        [Tooltip("Built in pieces (kitchen run, shower, lamps hung from the ceiling) cannot be moved.")]
        public bool pinned;

        [NonSerialized] public Vector3 homePos;
        [NonSerialized] public Quaternion homeRot;
        Bounds local;
        bool haveLocal;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        void Awake()
        {
            homePos = transform.position;
            homeRot = transform.rotation;
        }

        /// <summary>Exact bounding box in this piece's own space (from the meshes), so the outline follows its rotation.</summary>
        public Bounds LocalBounds
        {
            get
            {
                if (haveLocal) return local;
                bool first = true;
                foreach (var mf in GetComponentsInChildren<MeshFilter>())
                {
                    if (!mf.sharedMesh) continue;
                    var b = mf.sharedMesh.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        var c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                        var l = transform.InverseTransformPoint(mf.transform.TransformPoint(c));
                        if (first) { local = new Bounds(l, Vector3.zero); first = false; }
                        else local.Encapsulate(l);
                    }
                }
                if (first) local = new Bounds(Vector3.zero, Vector3.one * 0.3f);
                haveLocal = true;
                return local;
            }
        }

        public string Label
        {
            get
            {
                string n = key;
                int h = n.IndexOf('#');
                if (h > 0) n = n.Substring(0, h);
                n = n.Replace('_', ' ');
                return n.Length > 0 ? char.ToUpper(n[0]) + n.Substring(1) : n;
            }
        }

        // ---------- saved layout (PlayerPrefs, new fields must keep defaults, the key is never renamed) ----------

        const string PrefKey = "tiramisu.layout";

        [Serializable] class Entry { public string key; public Vector3 pos; public float yaw; public string host; }
        [Serializable] class Layout { public int version = 1; public List<Entry> items = new List<Entry>(); }

        /// <summary>The piece a small thing rests on (a cushion on a sofa), or null.</summary>
        static Furniture HostOf(Furniture f)
        {
            Furniture best = null;
            float bestTop = -1f;
            foreach (var h in All)
            {
                if (h == f || h.pinned || h.GetComponent<StickyProp>()) continue;
                var lb = h.LocalBounds;
                var l = h.transform.InverseTransformPoint(f.transform.position);
                if (Mathf.Abs(l.x - lb.center.x) > lb.extents.x || Mathf.Abs(l.z - lb.center.z) > lb.extents.z) continue;
                if (l.y < lb.min.y + 0.05f || l.y > lb.max.y + 0.12f) continue;
                if (lb.max.y > bestTop) { best = h; bestTop = lb.max.y; }
            }
            return best;
        }

        public static void SaveAll()
        {
            var l = new Layout();
            foreach (var f in All)
            {
                if (f.pinned) continue;
                if ((f.transform.position - f.homePos).sqrMagnitude < 1e-4f && Quaternion.Angle(f.transform.rotation, f.homeRot) < 0.1f) continue;
                var e = new Entry { key = f.key, pos = f.transform.position, yaw = f.transform.eulerAngles.y };
                if (f.GetComponent<StickyProp>())
                {
                    var host = HostOf(f);
                    if (host)
                    {
                        e.host = host.key;
                        e.pos = host.transform.InverseTransformPoint(f.transform.position);
                        e.yaw = Mathf.DeltaAngle(host.transform.eulerAngles.y, f.transform.eulerAngles.y);
                    }
                }
                l.items.Add(e);
            }
            PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(l));
            PlayerPrefs.Save();
        }

        /// <summary>Puts every piece where the player left it. Returns how many moved.</summary>
        public static int LoadAll()
        {
            Physics.SyncTransforms();
            int n = 0;
            if (PlayerPrefs.HasKey(PrefKey))
            {
                Layout l = null;
                try { l = JsonUtility.FromJson<Layout>(PlayerPrefs.GetString(PrefKey)); } catch { }
                if (l != null && l.items != null)
                {
                    var byKey = new Dictionary<string, Furniture>();
                    foreach (var f in All) byKey[f.key] = f;
                    // hosts and loose pieces first, then the small things that rest on them
                    for (int pass = 0; pass < 2; pass++)
                        foreach (var e in l.items)
                        {
                            bool sticky = !string.IsNullOrEmpty(e.host);
                            if (sticky != (pass == 1)) continue;
                            if (!byKey.TryGetValue(e.key, out var f) || f.pinned) continue;
                            if (sticky)
                            {
                                if (!byKey.TryGetValue(e.host, out var h)) continue;
                                f.Place(h.transform.TransformPoint(e.pos), Quaternion.Euler(0f, h.transform.eulerAngles.y + e.yaw, 0f));
                            }
                            else f.Place(e.pos, Quaternion.Euler(0f, e.yaw, 0f));
                            n++;
                        }
                }
            }
            Physics.SyncTransforms();
            SettleSmallThings();
            return n;
        }

        /// <summary>Drops every small thing straight down onto what is under it, so nothing hangs in the air.</summary>
        public static void SettleSmallThings()
        {
            Physics.SyncTransforms();
            foreach (var st in UnityEngine.Object.FindObjectsByType<StickyProp>(FindObjectsInactive.Exclude)) st.Settle();
        }

        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(PrefKey);
            foreach (var f in All) if (!f.pinned) f.Place(f.homePos, f.homeRot);
            Physics.SyncTransforms();
        }

        /// <summary>Moves the piece and calms every body in it.</summary>
        public void Place(Vector3 pos, Quaternion rot)
        {
            transform.SetPositionAndRotation(pos, rot);
            foreach (var st in GetComponentsInChildren<StickyProp>()) st.Anchor();   // small things take their new spot as home
            foreach (var rb in GetComponentsInChildren<Rigidbody>())
            {
                if (rb.isKinematic) continue;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }
}
