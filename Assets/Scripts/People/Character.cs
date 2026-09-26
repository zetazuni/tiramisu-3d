using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Tiramisu
{
    /// <summary>
    /// A person or a pet living in the house. It walks over the navigation surface (doors and gates open for it), sits on
    /// sofas and chairs, lies on beds and loungers, chats with the other person and pats the pets. Pets wander, sit,
    /// groom and sleep and stay on the ground floor.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class Character : MonoBehaviour
    {
        public static readonly List<Character> All = new List<Character>();

        static readonly List<Character> Everyone = new List<Character>();   // also the ones that are switched off

        /// <summary>Decorate mode stops everybody where they are and shows them as see-through silhouettes.</summary>
        public static bool Frozen { get; private set; }
        readonly List<(Renderer r, Material[] mats, UnityEngine.Rendering.ShadowCastingMode shadows)> original = new List<(Renderer, Material[], UnityEngine.Rendering.ShadowCastingMode)>();

        public static void SetAllFrozen(bool on, Material ghost)
        {
            Frozen = on;
            foreach (var c in Everyone.ToArray()) if (c) c.SetGhost(on, ghost);
        }

        void SetGhost(bool on, Material ghost)
        {
            if (on)
            {
                if (original.Count > 0 || !ghost) return;
                foreach (var r in GetComponentsInChildren<Renderer>())
                {
                    original.Add((r, r.sharedMaterials, r.shadowCastingMode));
                    var g = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < g.Length; i++) g[i] = ghost;
                    r.sharedMaterials = g;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            else
            {
                foreach (var o in original) if (o.r) { o.r.sharedMaterials = o.mats; o.r.shadowCastingMode = o.shadows; }
                original.Clear();
                if (agent && agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
            }
        }

        void HoldStill()
        {
            if (agent && agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
            rig.walkSpeed = 0f;
            if ((mode == Mode.Using || mode == Mode.Sitting) && spot != null)
            {
                SeatPose(out var p, out var q);   // still rides along if the piece is moved
                transform.SetPositionAndRotation(p, q);
            }
        }

        public string displayName = "";
        public bool isPet;
        public float scale = 1f;

        /// <summary>Something the player told a person to do (live mode). They are done one after the other before the person goes back to their own plans.</summary>
        public class Order
        {
            public enum Kind { Go, Use, Talk, Pet }
            public System.Action after;   // done on arrival (Go) or once seated (Use), for switching the TV and the like
            public Kind kind;
            public Vector3 point;
            public UseSpot spot;
            public Character target;
            public string label;
        }

        readonly Queue<Order> orders = new Queue<Order>();
        bool onOrder, holdSeat;
        Vector3 approachUsed;
        System.Action arrive, afterSeat;
        float autoResume;
        string doing = "";

        public int Queued => orders.Count;
        public bool OnOrder => onOrder;
        public bool CanChat => mode == Mode.Idle || mode == Mode.Walk || mode == Mode.ChatWalk || mode == Mode.PetWalk;

        public string Activity
        {
            get
            {
                switch (mode)
                {
                    case Mode.Waiting: return "Just arrived";
                    case Mode.Walk: case Mode.ToSpot: return onOrder && !string.IsNullOrEmpty(doing) ? doing : "Walking";
                    case Mode.Sitting: case Mode.Using: return spot != null && spot.pose == CharacterRig.Pose.Lie ? "Lying down" : "Sitting";
                    case Mode.Rising: return "Getting up";
                    case Mode.ChatWalk: return "Going to chat";
                    case Mode.Chatting: return "Chatting";
                    case Mode.PetWalk: return "Going to pet";
                    case Mode.Petting: return "Petting";
                    case Mode.BeingPetted: return "Being petted";
                    default: return "Relaxing";
                }
            }
        }

        public void GiveOrder(Order o)
        {
            orders.Enqueue(o);
            switch (mode)
            {
                case Mode.Using: case Mode.Sitting: timer = 0f; break;          // stands up first, then does the new thing
                case Mode.Walk: case Mode.ChatWalk: case Mode.PetWalk:
                    if (!onOrder) { partner = null; SetIdle(0f); }
                    break;
                case Mode.ToSpot:
                    if (!onOrder) { if (spot) spot.occupant = null; spot = null; SetIdle(0f); }
                    break;
                case Mode.Chatting: partner = null; SetIdle(0f); break;
                case Mode.Petting: if (partner) partner.EndPetted(); partner = null; SetIdle(0f); break;
                case Mode.Idle: timer = 0f; break;
            }
        }

        public void CancelOrders()
        {
            orders.Clear();
            if (mode == Mode.Walk || mode == Mode.ChatWalk || mode == Mode.PetWalk) { partner = null; SetIdle(0.5f); }
            else if (mode == Mode.ToSpot) { if (spot) spot.occupant = null; spot = null; SetIdle(0.5f); }
            else if (mode == Mode.Using) timer = 0f;
            onOrder = false;
        }

        void FaceForward()
        {
            // after walking up to a thing, turn to face what is straight ahead of the walk
            var v = agent && agent.enabled ? agent.velocity : Vector3.zero; v.y = 0f;
            if (v.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(v);
        }

        void Say(string line, float seconds = 2.6f) { Bubble = line; bubbleUntil = Time.time + seconds; }

        void StartOrder()
        {
            var o = orders.Dequeue();
            onOrder = true;
            doing = o.label ?? "";
            bool ok = false;
            switch (o.kind)
            {
                case Order.Kind.Go:
                    ok = GoTo(o.point, 2.2f);
                    if (ok) { mode = Mode.Walk; arrive = o.after; }
                    break;
                case Order.Kind.Use:
                    if (o.spot != null && (o.spot.occupant == null) && GoToApproach(o.spot))
                    {
                        spot = o.spot; spot.occupant = this; mode = Mode.ToSpot; holdSeat = true; afterSeat = o.after; ok = true;
                    }
                    break;
                case Order.Kind.Talk:
                    if (o.target != null && o.target.CanChat && GoTo(o.target.transform.position, 1.8f)) { partner = o.target; mode = Mode.ChatWalk; ok = true; }
                    break;
                case Order.Kind.Pet:
                    if (o.target != null && GoTo(o.target.transform.position, 1.4f)) { partner = o.target; mode = Mode.PetWalk; ok = true; }
                    break;
            }
            if (!ok) { Say("Hmm, I can't get there."); SetIdle(0.05f); }
        }

        enum Mode { Waiting, Idle, Walk, ToSpot, Sitting, Using, Rising, ChatWalk, Chatting, PetWalk, Petting, BeingPetted }

        NavMeshAgent agent;
        CharacterRig rig;
        Mode mode = Mode.Waiting;
        float timer, blend;
        UseSpot spot;
        Character partner;
        Vector3 fromPos, standPos;
        Quaternion fromRot;
        int lineIndex;
        float nextLine;

        public string Bubble { get; private set; }
        float bubbleUntil;
        public bool Speaking => Time.time < bubbleUntil;

        static readonly string[] Lines =
        {
            "Have you eaten yet?", "This house feels so cozy tonight.", "Shall I make some tea?", "I love the lights in the garden.",
            "Look how the pool glows.", "Did you feed the pets?", "Let's watch something later.", "Your day was good?",
            "The sofa is my favourite spot.", "I could stay here all evening.", "Thank you for today.", "Come, sit with me.",
        };

        void Awake()
        {
            Everyone.Add(this);
            agent = GetComponent<NavMeshAgent>();
            rig = GetComponent<CharacterRig>();
            agent.enabled = false;
        }

        void OnDestroy() => Everyone.Remove(this);
        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        void Update()
        {
            UpdateVisibility();
            if (Frozen) { HoldStill(); return; }
            if (mode == Mode.Waiting)
            {
                if (TiramisuNav.Ready) Begin();
                return;
            }
            switch (mode)
            {
                case Mode.Idle: TickIdle(); break;
                case Mode.Walk: TickWalk(); break;
                case Mode.ToSpot: TickToSpot(); break;
                case Mode.Sitting: TickSitting(); break;
                case Mode.Using: TickUsing(); break;
                case Mode.Rising: TickRising(); break;
                case Mode.ChatWalk: TickChatWalk(); break;
                case Mode.Chatting: TickChatting(); break;
                case Mode.PetWalk: TickPetWalk(); break;
                case Mode.Petting: TickPetting(); break;
                case Mode.BeingPetted: TickBeingPetted(); break;
            }
            if (agent.enabled && agent.isOnNavMesh)
            {
                float v = agent.velocity.magnitude;
                if (mode == Mode.Walk || mode == Mode.ToSpot || mode == Mode.ChatWalk || mode == Mode.PetWalk)
                {
                    rig.pose = v > 0.15f ? CharacterRig.Pose.Walk : CharacterRig.Pose.Stand;
                    rig.walkSpeed = v;
                }
            }
        }

        void UpdateVisibility()
        {
            // somebody upstairs is not drawn while the upper floor is switched off
            var view = HouseView.Instance;
            bool show = !(transform.position.y > 2.8f && view && view.upperFloor && !view.upperFloor.activeSelf);
            foreach (var r in GetComponentsInChildren<Renderer>()) if (r.enabled != show) r.enabled = show;
        }

        // ---------- start and decisions ----------

        void Begin()
        {
            agent.agentTypeID = TiramisuNav.AgentType;
            agent.radius = isPet ? 0.2f : 0.24f;
            agent.height = 1.7f * scale;
            agent.speed = isPet ? (name.Contains("cat") ? 1.1f : 1.6f) : 1.3f;
            agent.acceleration = 5f;
            agent.angularSpeed = 300f;
            agent.stoppingDistance = 0.1f;
            agent.autoBraking = true;
            if (isPet) agent.areaMask = ~(1 << TiramisuNav.StairsArea);   // pets stay on the ground floor
            if (NavMesh.SamplePosition(transform.position, out var hit, 3f, new NavMeshQueryFilter { agentTypeID = TiramisuNav.AgentType, areaMask = agent.areaMask }))
                transform.position = hit.position;
            agent.enabled = true;
            agent.Warp(transform.position);
            SetIdle(Random.Range(0.5f, 3f));
        }

        void SetIdle(float seconds)
        {
            mode = Mode.Idle;
            timer = seconds;
            rig.pose = CharacterRig.Pose.Stand;
            rig.walkSpeed = 0f;
            if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
        }

        void TickIdle()
        {
            if (orders.Count > 0) { StartOrder(); return; }
            if (onOrder) { onOrder = false; autoResume = Time.time + 12f; }   // told what to do: no wandering off for a while
            if (Time.time < autoResume) return;
            timer -= Time.deltaTime;
            if (timer <= 0f) Decide();
        }

        void Decide()
        {
            float r = Random.value;
            if (isPet)
            {
                if (r < 0.45f && Wander(6f)) return;
                if (r < 0.65f) { Rest(CharacterRig.Pose.Sit, Random.Range(6f, 14f)); return; }
                if (r < 0.8f) { Rest(CharacterRig.Pose.Groom, Random.Range(5f, 9f)); return; }
                if (r < 0.92f) { Rest(CharacterRig.Pose.Sleep, Random.Range(20f, 45f)); return; }
                SetIdle(Random.Range(2f, 5f));
                return;
            }
            if (r < 0.40f && TryUseSpot()) return;
            if (r < 0.62f && Wander(40f)) return;
            if (r < 0.78f && TryChat()) return;
            if (r < 0.92f && TryPet()) return;
            SetIdle(Random.Range(2f, 6f));
        }

        void Rest(CharacterRig.Pose p, float seconds)
        {
            SetIdle(seconds);
            rig.pose = p;
        }

        bool Wander(float radius)
        {
            for (int tries = 0; tries < 10; tries++)
            {
                Vector3 p;
                if (radius > 20f)
                {
                    float x = Random.Range(-2.4f, 33f), z = Random.Range(-4.4f, 22f);
                    float y = (Random.value < 0.3f && x > 0.5f && x < 29.5f && z > 0.5f && z < 7.5f) ? 3.4f : 0.1f;
                    p = new Vector3(x, y, z);
                }
                else
                {
                    var d = Random.insideUnitCircle * radius;
                    p = transform.position + new Vector3(d.x, 0f, d.y);
                    p.x = Mathf.Clamp(p.x, -2.4f, 33f); p.z = Mathf.Clamp(p.z, -4.4f, 22f);
                }
                if (GoTo(p, 1.5f)) { mode = Mode.Walk; return true; }
            }
            return false;
        }

        /// <summary>The nearest navigation point at about the same height: the tops of tables and sofas are walkable islands too, and must not be picked.</summary>
        static bool SampleFloor(Vector3 p, float maxSnap, NavMeshQueryFilter filter, out NavMeshHit hit)
        {
            if (NavMesh.SamplePosition(p, out hit, maxSnap, filter) && Mathf.Abs(hit.position.y - p.y) < 0.3f) return true;
            for (float r = 0.25f; r <= maxSnap + 0.01f; r += 0.25f)
                for (int k = 0; k < 12; k++)
                {
                    float a = k * 30f * Mathf.Deg2Rad;
                    var q = p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    if (NavMesh.SamplePosition(q, out hit, 0.2f, filter) && Mathf.Abs(hit.position.y - p.y) < 0.3f) return true;
                }
            hit = default;
            return false;
        }

        bool GoTo(Vector3 p, float maxSnap)
        {
            if (!agent.enabled || !agent.isOnNavMesh) return false;
            var filter = new NavMeshQueryFilter { agentTypeID = TiramisuNav.AgentType, areaMask = agent.areaMask };
            if (!SampleFloor(p, maxSnap, filter, out var hit)) return false;
            var path = new NavMeshPath();
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            agent.SetPath(path);
            return true;
        }

        bool Arrived() => agent.enabled && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.12f;

        void TickWalk()
        {
            if (Arrived())
            {
                var a = arrive; arrive = null;
                if (a != null) { FaceForward(); a(); }
                SetIdle(onOrder ? 0.05f : Random.Range(1.5f, 5f));
            }
        }

        // ---------- furniture ----------

        /// <summary>
        /// Walks to the usual spot in front of a seat or bed; when that is blocked (a coffee table close to the sofa) the person
        /// comes from the side or the front instead, and gets up again at the same place.
        /// </summary>
        bool GoToApproach(UseSpot s)
        {
            if (GoTo(s.ApproachWorld, 1.2f)) { approachUsed = s.ApproachWorld; return true; }
            var facing = Quaternion.Euler(0f, (s.transform.parent ? s.transform.parent.eulerAngles.y : 0f) + s.yaw, 0f);
            var filter = new NavMeshQueryFilter { agentTypeID = TiramisuNav.AgentType, areaMask = agent.areaMask };
            foreach (float r in new[] { 1.1f, 1.5f, 1.9f, 2.4f })
                for (int k = 0; k < 8; k++)
                {
                    float a = k * 45f;                                                // straight in front first, then round the piece
                    var p = s.transform.position + facing * (Quaternion.Euler(0f, a, 0f) * Vector3.forward * r);
                    p.y = transform.position.y;
                    if (!SampleFloor(p, 0.5f, filter, out var hit)) continue;
                    if (GoTo(hit.position, 0.3f)) { approachUsed = hit.position; return true; }
                }
            return false;
        }

        bool TryUseSpot()
        {
            var free = new List<UseSpot>();
            foreach (var s in UseSpot.All) if (s.occupant == null && s.gameObject.activeInHierarchy) free.Add(s);
            for (int tries = 0; tries < 6 && free.Count > 0; tries++)
            {
                var s = free[Random.Range(0, free.Count)];
                if (!GoToApproach(s)) { free.Remove(s); continue; }
                spot = s; s.occupant = this; mode = Mode.ToSpot;
                return true;
            }
            return false;
        }

        void TickToSpot()
        {
            if (spot == null) { SetIdle(1f); return; }
            if (!Arrived()) return;
            agent.enabled = false;
            fromPos = transform.position; fromRot = transform.rotation;
            blend = 0f; mode = Mode.Sitting;
            rig.seat = spot;
            rig.pose = spot.pose == CharacterRig.Pose.Lie ? CharacterRig.Pose.Lie : CharacterRig.Pose.Sit;
            rig.walkSpeed = 0f;
        }

        void SeatPose(out Vector3 pos, out Quaternion rot)
        {
            // sitting: the pelvis is lowered by the pose, so the root goes where the pelvis target minus the hip height is
            rig.pose = spot.pose;
            rig.seat = spot;
            spot.RootFor(rig, scale, out pos, out rot);
        }

        void TickSitting()
        {
            blend += Time.deltaTime / 0.8f;
            SeatPose(out var pos, out var rot);
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(blend));
            transform.SetPositionAndRotation(Vector3.Lerp(fromPos, pos, e), Quaternion.Slerp(fromRot, rot, e));
            if (blend >= 1f)
            {
                mode = Mode.Using; timer = holdSeat ? 150f : Random.Range(spot.seconds.x, spot.seconds.y);
                bool ordered = holdSeat; holdSeat = false;
                var seated = afterSeat; afterSeat = null;
                if (seated != null) seated();
                else if (!ordered)
                {
                    var tv = TvScreen.Facing(spot);       // sat down in front of the TV on their own: sometimes puts it on
                    if (tv != null && !tv.on && Random.value < 0.6f) tv.SetOn(true, true);
                }
            }
        }

        void TickUsing()
        {
            SeatPose(out var pos, out var rot);
            transform.SetPositionAndRotation(pos, rot);   // follows the piece if it is moved
            timer -= Time.deltaTime;
            if (orders.Count > 0) timer = 0f;   // told to do something else
            if (timer <= 0f)
            {
                var tvLeft = TvScreen.Facing(spot);
                if (tvLeft != null && tvLeft.on && tvLeft.autoSwitched && !TvScreen.AnyoneWatching(tvLeft, this)) tvLeft.SetOn(false);
                fromPos = transform.position; fromRot = transform.rotation;
                standPos = approachUsed;
                blend = 0f; mode = Mode.Rising;
                rig.pose = CharacterRig.Pose.Stand;
            }
        }

        void TickRising()
        {
            blend += Time.deltaTime / 0.7f;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(blend));
            var upright = Quaternion.Euler(0f, fromRot.eulerAngles.y, 0f);
            var faceOut = Quaternion.LookRotation(new Vector3(standPos.x - fromPos.x, 0f, standPos.z - fromPos.z).sqrMagnitude > 0.01f ? new Vector3(standPos.x - fromPos.x, 0f, standPos.z - fromPos.z) : transform.forward);
            transform.SetPositionAndRotation(Vector3.Lerp(fromPos, standPos, e), Quaternion.Slerp(fromRot, faceOut, e));
            if (blend >= 1f)
            {
                if (spot) spot.occupant = null;
                spot = null;
                agent.enabled = true;
                agent.Warp(standPos);
                SetIdle(Random.Range(1f, 3f));
            }
        }

        // ---------- talking and petting ----------

        Character OtherPerson()
        {
            foreach (var c in All) if (c != this && !c.isPet) return c;
            return null;
        }

        bool TryChat()
        {
            var o = OtherPerson();
            if (o == null || o.mode == Mode.Waiting || o.mode == Mode.Sitting || o.mode == Mode.Using || o.mode == Mode.Rising || o.mode == Mode.Chatting || o.mode == Mode.ChatWalk) return false;
            if (!GoTo(o.transform.position, 1.6f)) return false;
            partner = o; mode = Mode.ChatWalk;
            return true;
        }

        void TickChatWalk()
        {
            if (partner == null || partner.mode == Mode.Using || partner.mode == Mode.Sitting) { SetIdle(1f); return; }
            if (Vector3.Distance(transform.position, partner.transform.position) < 1.5f)
            {
                agent.ResetPath();
                lineIndex = 0; nextLine = 0f; timer = 12f; mode = Mode.Chatting;
                partner.JoinChat(this);
                return;
            }
            if (!agent.pathPending && agent.remainingDistance < 0.3f) GoTo(partner.transform.position, 1.6f);
        }

        void JoinChat(Character other)
        {
            partner = other;
            if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
            timer = 12f; lineIndex = 1; nextLine = 2.4f; mode = Mode.Chatting;
        }

        void TickChatting()
        {
            timer -= Time.deltaTime;
            if (partner == null) { SetIdle(1f); return; }
            var d = partner.transform.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 6f);
            rig.pose = lineIndex % 2 == 0 && Speaking ? CharacterRig.Pose.Wave : CharacterRig.Pose.Stand;
            nextLine -= Time.deltaTime;
            if (nextLine <= 0f && lineIndex % 2 == 0)
            {
                Bubble = Lines[Random.Range(0, Lines.Length)];
                bubbleUntil = Time.time + 3.2f;
                nextLine = 6f;
                lineIndex++;
                partner.nextLine = 3.2f;
            }
            else if (nextLine <= 0f)
            {
                lineIndex++;
                nextLine = 3.4f;
                if (lineIndex % 2 == 1) { Bubble = Lines[Random.Range(0, Lines.Length)]; bubbleUntil = Time.time + 3.2f; }
            }
            if (timer <= 0f) { partner = null; SetIdle(Random.Range(1f, 3f)); }
        }

        bool TryPet()
        {
            foreach (var c in All)
            {
                if (!c.isPet || c.mode == Mode.Waiting || c.mode == Mode.BeingPetted) continue;
                if (!GoTo(c.transform.position, 1.2f)) continue;
                partner = c; mode = Mode.PetWalk;
                return true;
            }
            return false;
        }

        void TickPetWalk()
        {
            if (partner == null) { SetIdle(1f); return; }
            if (Vector3.Distance(transform.position, partner.transform.position) < 0.9f)
            {
                agent.ResetPath();
                timer = 6f; mode = Mode.Petting;
                partner.BePetted(this);
                return;
            }
            if (!agent.pathPending && agent.remainingDistance < 0.3f) GoTo(partner.transform.position, 1.2f);
        }

        void TickPetting()
        {
            timer -= Time.deltaTime;
            if (partner == null) { SetIdle(1f); return; }
            var d = partner.transform.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 6f);
            rig.pose = CharacterRig.Pose.Crouch;
            if (timer <= 0f) { if (partner) partner.EndPetted(); partner = null; SetIdle(Random.Range(1f, 3f)); }
        }

        void BePetted(Character by)
        {
            partner = by;
            if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
            timer = 6.2f; mode = Mode.BeingPetted;
            rig.pose = CharacterRig.Pose.Happy;
        }

        void EndPetted() { if (mode == Mode.BeingPetted) SetIdle(Random.Range(1f, 3f)); }

        void TickBeingPetted()
        {
            timer -= Time.deltaTime;
            if (partner != null)
            {
                var d = partner.transform.position - transform.position; d.y = 0f;
                if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 5f);
            }
            if (timer <= 0f) SetIdle(Random.Range(1f, 3f));
        }
    }
}
