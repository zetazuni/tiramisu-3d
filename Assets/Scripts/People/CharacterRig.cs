using System.Collections.Generic;
using UnityEngine;

namespace Tiramisu
{
    /// <summary>
    /// Poses and animates a jointed figure from tools/blender_characters.py (people and pets) by swinging its joints.
    /// Angles are given as "forward" or "bend" values and turned into rotations about the figure's own right and up axes,
    /// so it does not matter how the FBX importer oriented the joints.
    /// </summary>
    public class CharacterRig : MonoBehaviour
    {
        public enum Pose { Stand, Walk, Sit, Lie, Wave, Crouch, Sleep, Groom, Happy }

        public bool pet;
        [Tooltip("person, cat or dog: which joint table builds the skeleton")]
        public string kind = "person";
        public Pose pose = Pose.Stand;
        public float walkSpeed = 1f;       // metres per second, drives the stride

        class Joint
        {
            public Transform t;
            public Quaternion rest;
            public Vector3 restPos;
            public Vector3 rightLocal, upLocal, upInParent;
            public float ang, tgt, lift, liftTgt, yaw, yawTgt;
        }

        readonly Dictionary<string, Joint> j = new Dictionary<string, Joint>();
        float phase, clock;

        static readonly string[] PersonJoints = { "pelvis", "spine", "neck", "arm.L", "forearm.L", "arm.R", "forearm.R", "leg.L", "shin.L", "leg.R", "shin.R" };
        static readonly string[] PetJoints = { "body", "head", "tail", "legFL", "legFR", "legBL", "legBR" };

        // Joint positions in Blender space (x, y, z), as in tools/blender_characters.py. The FBX is flat: every part is named
        // "joint|part", and the skeleton is built here at runtime and the parts are hung on it.
        static readonly (string name, string parent, Vector3 pos)[] PersonTable =
        {
            ("pelvis", null, new Vector3(0, 0, 0.95f)), ("spine", "pelvis", new Vector3(0, 0, 0.95f)), ("neck", "spine", new Vector3(0, 0, 1.44f)),
            ("arm.L", "spine", new Vector3(0.2f, 0, 1.4f)), ("forearm.L", "arm.L", new Vector3(0.2f, 0, 1.13f)),
            ("arm.R", "spine", new Vector3(-0.2f, 0, 1.4f)), ("forearm.R", "arm.R", new Vector3(-0.2f, 0, 1.13f)),
            ("leg.L", "pelvis", new Vector3(0.085f, 0, 0.93f)), ("shin.L", "leg.L", new Vector3(0.085f, 0, 0.5f)),
            ("leg.R", "pelvis", new Vector3(-0.085f, 0, 0.93f)), ("shin.R", "leg.R", new Vector3(-0.085f, 0, 0.5f)),
        };
        static readonly (string name, string parent, Vector3 pos)[] CatTable =
        {
            ("body", null, new Vector3(0, 0, 0.2f)), ("head", "body", new Vector3(0, -0.27f, 0.3f)), ("tail", "body", new Vector3(0, 0.3f, 0.26f)),
            ("legFL", "body", new Vector3(0.07f, -0.16f, 0.16f)), ("legFR", "body", new Vector3(-0.07f, -0.16f, 0.16f)),
            ("legBL", "body", new Vector3(0.08f, 0.16f, 0.16f)), ("legBR", "body", new Vector3(-0.08f, 0.16f, 0.16f)),
        };
        static readonly (string name, string parent, Vector3 pos)[] DogTable =
        {
            ("body", null, new Vector3(0, 0, 0.36f)), ("head", "body", new Vector3(0, -0.36f, 0.5f)), ("tail", "body", new Vector3(0, 0.38f, 0.44f)),
            ("legFL", "body", new Vector3(0.09f, -0.24f, 0.3f)), ("legFR", "body", new Vector3(-0.09f, -0.24f, 0.3f)),
            ("legBL", "body", new Vector3(0.1f, 0.24f, 0.3f)), ("legBR", "body", new Vector3(-0.1f, 0.24f, 0.3f)),
        };

        void BuildSkeleton()
        {
            if (FindDeep(transform, pet ? "body" : "pelvis")) return;   // already jointed
            var table = kind == "cat" ? CatTable : kind == "dog" ? DogTable : PersonTable;
            var made = new Dictionary<string, Transform>();
            foreach (var (n, par, pos) in table)
            {
                var g = new GameObject(n).transform;
                g.SetParent(transform, false);
                g.localPosition = new Vector3(-pos.x, pos.z, -pos.y);    // Blender to Unity
                if (par != null) g.SetParent(made[par], true);
                made[n] = g;
            }
            var parts = new List<Transform>();
            foreach (Transform c in transform) if (c.name.Contains("|")) parts.Add(c);
            foreach (var c in parts)
                if (made.TryGetValue(c.name.Split('|')[0], out var joint)) c.SetParent(joint, true);
        }

        void Awake()
        {
            BuildSkeleton();
            foreach (var n in pet ? PetJoints : PersonJoints)
            {
                var t = FindDeep(transform, n);
                if (!t) continue;
                j[n] = new Joint
                {
                    t = t,
                    rest = t.localRotation,
                    restPos = t.localPosition,
                    rightLocal = Quaternion.Inverse(t.rotation) * transform.right,
                    upLocal = Quaternion.Inverse(t.rotation) * transform.up,
                    upInParent = t.parent ? Quaternion.Inverse(t.parent.rotation) * transform.up : Vector3.up,
                };
            }
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform c in root)
            {
                if (c.name == name || (c.name.StartsWith(name + ".") && c.name.Length > name.Length + 1 && char.IsDigit(c.name[name.Length + 1]))) return c;   // Blender numbers repeated names (pelvis.001)
                var r = FindDeep(c, name);
                if (r) return r;
            }
            return null;
        }

        void Set(string n, float forward = 0f, float lift = 0f, float yaw = 0f)
        {
            if (!j.TryGetValue(n, out var jt)) return;
            jt.tgt = forward; jt.liftTgt = lift; jt.yawTgt = yaw;
        }

        void Update()
        {
            clock += Time.deltaTime;
            float stride = Mathf.Clamp(walkSpeed, 0f, 3f);
            phase += Time.deltaTime * (pet ? 7f : 5.2f) * Mathf.Max(0.35f, stride / (pet ? 1.2f : 1.4f));
            foreach (var jt in j.Values) { jt.tgt = 0f; jt.liftTgt = 0f; jt.yawTgt = 0f; }
            if (pet) PetTargets(); else PersonTargets();

            float k = 1f - Mathf.Exp(-14f * Time.deltaTime);
            foreach (var kv in j)
            {
                var jt = kv.Value;
                if (!jt.t) continue;
                jt.ang = Mathf.Lerp(jt.ang, jt.tgt, k);
                jt.lift = Mathf.Lerp(jt.lift, jt.liftTgt, k);
                jt.yaw = Mathf.Lerp(jt.yaw, jt.yawTgt, k);
                // positive "forward" swings the joint towards the front of the figure, that is a negative turn about its right axis
                var q = jt.rest * Quaternion.AngleAxis(-jt.ang, jt.rightLocal) * Quaternion.AngleAxis(jt.yaw, jt.upLocal);
                jt.t.localRotation = q;
                jt.t.localPosition = jt.restPos + jt.upInParent * jt.lift;
            }
        }

        void PersonTargets()
        {
            float s = Mathf.Sin(phase), breathe = Mathf.Sin(clock * 1.6f);
            switch (pose)
            {
                case Pose.Walk:
                    Set("leg.L", 30f * s); Set("leg.R", -30f * s);
                    Set("shin.L", -Mathf.Max(0f, -s) * 45f); Set("shin.R", -Mathf.Max(0f, s) * 45f);   // shins bend backwards (negative forward)
                    Set("arm.L", -26f * s); Set("arm.R", 26f * s);
                    Set("forearm.L", 12f + Mathf.Max(0f, s) * 12f); Set("forearm.R", 12f + Mathf.Max(0f, -s) * 12f);
                    Set("spine", 2f, 0f, 3f * s);
                    Set("pelvis", 0f, Mathf.Abs(s) * 0.012f);
                    break;
                case Pose.Sit:
                    Set("pelvis", 0f, -0.5f);
                    Set("leg.L", 90f); Set("leg.R", 90f);
                    Set("shin.L", -90f); Set("shin.R", -90f);
                    Set("arm.L", 18f); Set("arm.R", 18f); Set("forearm.L", 45f); Set("forearm.R", 45f);
                    Set("spine", 2f + breathe);
                    break;
                case Pose.Lie:
                    Set("arm.L", 4f); Set("arm.R", 4f); Set("forearm.L", 15f); Set("forearm.R", 15f);
                    Set("spine", breathe * 1.5f);
                    break;
                case Pose.Wave:
                    Set("arm.R", 165f); Set("forearm.R", 30f + 25f * Mathf.Sin(clock * 9f));
                    Set("spine", breathe);
                    break;
                case Pose.Crouch:
                    Set("pelvis", 0f, -0.42f);
                    Set("leg.L", 105f); Set("leg.R", 105f); Set("shin.L", -120f); Set("shin.R", -120f);
                    Set("spine", 22f); Set("arm.L", 50f); Set("arm.R", 50f); Set("forearm.L", 30f + 10f * Mathf.Sin(clock * 3f)); Set("forearm.R", 30f);
                    break;
                default:
                    Set("spine", breathe * 1.2f);
                    Set("arm.L", 2f); Set("arm.R", 2f);
                    break;
            }
        }

        void PetTargets()
        {
            float s = Mathf.Sin(phase), tailSway = Mathf.Sin(clock * 3.5f) * 12f;
            switch (pose)
            {
                case Pose.Walk:
                    float sw = 34f * s;
                    Set("legFL", sw); Set("legBR", sw); Set("legFR", -sw); Set("legBL", -sw);
                    Set("body", 0f, Mathf.Abs(s) * 0.012f);
                    Set("head", -4f * s);
                    Set("tail", 0f, 0f, 18f * Mathf.Sin(clock * 5f));
                    break;
                case Pose.Sit:
                case Pose.Groom:
                case Pose.Happy:
                    Set("body", 32f, -0.07f);
                    Set("legBL", 88f); Set("legBR", 88f);
                    Set("head", pose == Pose.Groom ? -30f + 10f * Mathf.Sin(clock * 6f) : -8f);
                    Set("tail", 0f, 0f, pose == Pose.Happy ? 38f * Mathf.Sin(clock * 14f) : tailSway);
                    break;
                case Pose.Sleep:
                    Set("body", 0f, -0.09f);
                    Set("legFL", 80f); Set("legFR", 80f); Set("legBL", 80f); Set("legBR", 80f);
                    Set("head", -30f + 2f * Mathf.Sin(clock * 1.4f));
                    Set("tail", 0f, 0f, 55f);
                    break;
                default:
                    Set("body", 0f, Mathf.Sin(clock * 1.8f) * 0.004f);
                    Set("tail", 0f, 0f, tailSway);
                    break;
            }
        }

        /// <summary>How high the pelvis (people) or body (pets) hangs above the figure's origin in each pose, unscaled.</summary>
        public float HipHeight => pose switch
        {
            Pose.Sit => 0.45f,
            Pose.Crouch => 0.53f,
            _ => 0.95f,
        };
    }
}
