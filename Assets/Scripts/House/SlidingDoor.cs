using UnityEngine;

namespace Tiramisu
{
    /// <summary>
    /// A modern sliding door that runs on a black rail along the wall. It opens by itself when the mouse hovers
    /// over it, or when anyone carrying a <see cref="DoorOpener"/> (people, pets) walks up to it from either side,
    /// and glides shut again a moment after the last one has gone.
    /// </summary>
    public class SlidingDoor : MonoBehaviour
    {
        public static readonly System.Collections.Generic.List<SlidingDoor> All = new System.Collections.Generic.List<SlidingDoor>();

        [Tooltip("The moving panel (its children move with it).")]
        public Transform panel;
        [Tooltip("How far the panel slides, in the panel's parent space.")]
        public Vector3 slide = new Vector3(0f, 0f, -2f);
        [Tooltip("Zone in front of the doorway that wakes the door for people and pets (world space centre and size).")]
        public Vector3 sensorCenter;
        public Vector3 sensorSize = new Vector3(2.6f, 2.2f, 2.6f);
        public float openSeconds = 0.9f;
        public float stayOpenSeconds = 1.2f;

        Vector3 closedPos;
        float openness, holdUntil, nextScan;
        bool open;

        static int hoverFrame = -1;
        static SlidingDoor hovered;
        static readonly Collider[] buffer = new Collider[24];

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        void Awake()
        {
            if (panel) closedPos = panel.localPosition;
        }

        /// <summary>Opens the door and keeps it open for a moment (call again to keep it open).</summary>
        public void Open(float seconds = -1f)
        {
            holdUntil = Mathf.Max(holdUntil, Time.time + (seconds < 0f ? stayOpenSeconds : seconds));
        }

        void Update()
        {
            if (!panel) return;

            // one mouse ray per frame for all doors
            if (hoverFrame != Time.frameCount)
            {
                hoverFrame = Time.frameCount;
                hovered = null;
                var cam = Camera.main;
                if (cam && !(OrbitCamera.Blocked) && Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out var hit, 400f, ~0, QueryTriggerInteraction.Ignore))
                    hovered = hit.collider.GetComponentInParent<SlidingDoor>();
            }
            if (hovered == this) Open();

            // people and pets
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 0.15f;
                int n = Physics.OverlapBoxNonAlloc(sensorCenter, sensorSize * 0.5f, buffer, Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
                for (int i = 0; i < n; i++)
                    if (buffer[i].GetComponentInParent<DoorOpener>() != null) { Open(); break; }
            }

            open = Time.time < holdUntil;
            float target = open ? 1f : 0f;
            openness = Mathf.MoveTowards(openness, target, Time.deltaTime / Mathf.Max(openSeconds, 0.05f));
            float eased = openness * openness * (3f - 2f * openness);   // smooth start and stop
            panel.localPosition = closedPos + slide * eased;
        }
    }
}
