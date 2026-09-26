using UnityEngine;

namespace Tiramisu
{
    /// <summary>
    /// Small things (cushions, books, towels, mugs, fruit, lamps) stay put on whatever they sit on. A nudge
    /// only shifts them a couple of centimetres and tilts them a touch, then they settle back onto their spot
    /// in 200 milliseconds. They are kinematic, so nothing can knock them off. Moving the piece they sit on in
    /// decorate mode carries them along and they take their new spot as home.
    /// </summary>
    public class StickyProp : MonoBehaviour
    {
        public const float ReturnSeconds = 0.2f;
        public float nudgeDistance = 0.03f;
        public float nudgeTilt = 5f;

        Vector3 restPos, fromPos;
        Quaternion restRot, fromRot;
        float t = 1f;

        void Awake() => Anchor();

        /// <summary>Takes the current pose as the home pose.</summary>
        public void Anchor()
        {
            restPos = transform.position;
            restRot = transform.rotation;
            fromPos = restPos;
            fromRot = restRot;
            t = 1f;
        }

        /// <summary>Drops the prop straight down onto the surface under it (or lifts it out of one), then makes that its home.</summary>
        public void Settle()
        {
            var rs = GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            var from = new Vector3(b.center.x, b.min.y + 0.35f, b.center.z);
            var hits = Physics.RaycastAll(from, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (p, q) => p.distance.CompareTo(q.distance));
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(transform)) continue;
                float delta = h.point.y - b.min.y;
                if (Mathf.Abs(delta) > 0.004f && Mathf.Abs(delta) < 0.5f) transform.position += Vector3.up * delta;
                break;
            }
            Anchor();
        }

        /// <summary>Shifts the prop a little along dir (horizontal) and lets it return.</summary>
        public void Nudge(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return;
            dir.Normalize();
            fromPos = restPos + dir * nudgeDistance + Vector3.up * 0.004f;
            var axis = Vector3.Cross(Vector3.up, dir);            // tips a little in the direction of the shove
            fromRot = Quaternion.AngleAxis(nudgeTilt, axis) * restRot;
            transform.SetPositionAndRotation(fromPos, fromRot);
            t = 0f;
        }

        void Update()
        {
            if (t >= 1f) return;
            t = Mathf.Min(1f, t + Time.deltaTime / ReturnSeconds);
            float e = 1f - (1f - t) * (1f - t) * (1f - t);        // fast at first, settles softly
            transform.SetPositionAndRotation(Vector3.Lerp(fromPos, restPos, e), Quaternion.Slerp(fromRot, restRot, e));
        }
    }
}
