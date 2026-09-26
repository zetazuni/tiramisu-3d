using System.Collections.Generic;
using UnityEngine;

namespace Tiramisu
{
    /// <summary>
    /// A place where a person can sit or lie on a piece of furniture. It is a child of the piece, so it moves with it.
    /// The object's own position is where the pelvis ends up (in the piece's space), yaw is the way the person faces
    /// relative to the piece, and approach is where they stand before they get on.
    /// </summary>
    public class UseSpot : MonoBehaviour
    {
        public static readonly List<UseSpot> All = new List<UseSpot>();

        public string label = "sit";
        public CharacterRig.Pose pose = CharacterRig.Pose.Sit;
        public float yaw;
        public Vector3 approachLocal;
        public Vector2 seconds = new Vector2(20f, 60f);
        [System.NonSerialized] public Character occupant;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        public Vector3 ApproachWorld => transform.parent.TransformPoint(approachLocal);

        /// <summary>The figure's root position and rotation that put its pelvis on this spot.</summary>
        public void RootFor(CharacterRig rig, float scale, out Vector3 pos, out Quaternion rot)
        {
            var yawRot = Quaternion.Euler(0f, transform.parent.eulerAngles.y + yaw, 0f);
            rot = pose == CharacterRig.Pose.Lie ? yawRot * Quaternion.Euler(-90f, 0f, 0f) : yawRot;
            float hip = pose == CharacterRig.Pose.Sit ? 0.45f : 0.95f;
            pos = transform.position - rot * new Vector3(0f, hip * scale, 0f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(transform.position, 0.08f);
        }
    }
}
