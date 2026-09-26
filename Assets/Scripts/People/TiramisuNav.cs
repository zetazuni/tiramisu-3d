using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Tiramisu
{
    /// <summary>
    /// Builds the walkable surface for people and pets from the house's colliders. It bakes once shortly after the start
    /// (all floors switched on and all walls up for that moment) and again a second after furniture was moved. Doors and
    /// gates are left out of the bake, they open for whoever walks up to them.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public class TiramisuNav : MonoBehaviour
    {
        public static TiramisuNav Instance { get; private set; }
        public static bool Ready { get; private set; }
        public static int AgentType { get; private set; }
        public const int StairsArea = 3;

        NavMeshSurface surface;
        float rebuildAt;

        void Awake()
        {
            Instance = this;
            var s = NavMesh.CreateSettings();
            s.agentRadius = 0.24f;
            s.agentHeight = 1.7f;
            s.agentClimb = 0.4f;
            s.agentSlope = 48f;
            s.overrideVoxelSize = true;
            s.voxelSize = 0.05f;
            AgentType = s.agentTypeID;
            surface = gameObject.AddComponent<NavMeshSurface>();
            surface.agentTypeID = AgentType;
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
        }

        IEnumerator Start()
        {
            yield return new WaitForSeconds(1.6f);   // the furniture has settled by now
            Build();
            Ready = true;
        }

        public void RequestRebuild() => rebuildAt = Time.time + 1.2f;

        void Update()
        {
            if (rebuildAt > 0f && Time.time > rebuildAt && DecorateMode.Instance != null && !DecorateMode.Instance.Holding)
            {
                rebuildAt = 0f;
                Build();
            }
        }

        void Build()
        {
            var view = HouseView.Instance;
            bool upperWas = view && view.upperFloor && view.upperFloor.activeSelf;
            bool roofWas = view && view.roof && view.roof.activeSelf;
            if (view && view.upperFloor) view.upperFloor.SetActive(true);
            if (view && view.roof) view.roof.SetActive(false);
            foreach (var w in WallCutaway.All) w.BakeMode(true);
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            foreach (var w in WallCutaway.All) w.BakeMode(false);
            if (view && view.upperFloor) view.upperFloor.SetActive(upperWas);
            if (view && view.roof) view.roof.SetActive(roofWas);
        }
    }
}
