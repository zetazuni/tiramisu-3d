using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Tiramisu.EditorTools
{
    /// <summary>
    /// Realistic physics (rule 6): project settings, surface physics materials matched to the
    /// render material names, and colliders plus real masses for furniture.
    /// Furniture models come from Blender as a root with one child per part, and each part gets
    /// its own fitted box collider (or the exact mesh when it never moves), so the whole piece is an accurate compound collider.
    /// </summary>
    public static class PhysicsSetup
    {
        const string Dir = "Assets/Art/Physics";

        public struct FurnitureSpec
        {
            public float mass;        // kg
            public bool dynamic;      // false = never moves (rugs, built in pieces)
            public float dropHeight;  // placed this high and allowed to fall and settle (cushions)
        }

        static readonly Dictionary<string, FurnitureSpec> Specs = new Dictionary<string, FurnitureSpec>
        {
            { "sofa",        new FurnitureSpec { mass = 70f, dynamic = true } },
            { "marbletable", new FurnitureSpec { mass = 38f, dynamic = true } },
            { "geomrug",     new FurnitureSpec { mass = 6f,  dynamic = false } },
            { "kitchenrun",  new FurnitureSpec { mass = 400f, dynamic = false } },
            { "fridge",      new FurnitureSpec { mass = 90f,  dynamic = false } },
            { "kitchenisland", new FurnitureSpec { mass = 300f, dynamic = false } },
            { "barstool",    new FurnitureSpec { mass = 5f,   dynamic = true } },
            { "diningtable", new FurnitureSpec { mass = 35f,  dynamic = true } },
            { "diningchair", new FurnitureSpec { mass = 5.5f, dynamic = true } },
            { "pendant",     new FurnitureSpec { mass = 1f,   dynamic = false } },
            { "cushion",     new FurnitureSpec { mass = 0.8f, dynamic = true, dropHeight = 0.9f } },
        };

        public static FurnitureSpec Spec(string id) =>
            Specs.TryGetValue(id, out var s) ? s : new FurnitureSpec { mass = 20f, dynamic = true };

        // ---------- project settings ----------

        public static void ProjectSettings()
        {
            Time.fixedDeltaTime = 1f / 90f;
            Physics.defaultSolverIterations = 12;
            Physics.defaultSolverVelocityIterations = 4;
            Physics.bounceThreshold = 1f;
            Physics.defaultContactOffset = 0.005f;
            Physics.sleepThreshold = 0.005f;
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
            AssetDatabase.SaveAssets();
        }

        // ---------- surfaces ----------

        static PhysicsMaterial Surface(string name, float dyn, float stat, float bounce,
            PhysicsMaterialCombine friction = PhysicsMaterialCombine.Average)
        {
            System.IO.Directory.CreateDirectory(Dir);
            string path = $"{Dir}/{name}.asset";
            var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (!m) { m = new PhysicsMaterial(name); AssetDatabase.CreateAsset(m, path); }
            m.dynamicFriction = dyn;
            m.staticFriction = stat;
            m.bounciness = bounce;
            m.frictionCombine = friction;
            m.bounceCombine = PhysicsMaterialCombine.Average;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Dictionary<string, PhysicsMaterial> byRenderMaterial;

        static PhysicsMaterial For(string renderMaterial)
        {
            if (byRenderMaterial == null)
            {
                var wood = Surface("Wood", 0.42f, 0.55f, 0.08f);
                var stone = Surface("Stone", 0.55f, 0.65f, 0.04f);
                var glassM = Surface("Glass", 0.25f, 0.35f, 0.06f);
                var grass = Surface("Grass", 0.7f, 0.85f, 0.02f);
                var rubber = Surface("Rubber", 0.9f, 1.0f, 0.35f, PhysicsMaterialCombine.Maximum);
                var metal = Surface("Metal", 0.35f, 0.45f, 0.1f);
                var fabric = Surface("Fabric", 0.75f, 0.9f, 0.02f, PhysicsMaterialCombine.Maximum);
                var foliage = Surface("Foliage", 0.6f, 0.7f, 0.15f);
                byRenderMaterial = new Dictionary<string, PhysicsMaterial>
                {
                    { "Oak", wood }, { "OakDark", wood }, { "Deck", wood }, { "Walnut", wood }, { "Trunk", wood },
                    { "WallWhite", stone }, { "Slab", stone }, { "Stone", stone }, { "GarageFloor", stone },
                    { "KitchenTile", stone }, { "BathTile", stone }, { "PoolTile", stone }, { "Marble_main", stone },
                    { "Glass", glassM }, { "PoolWater", glassM },
                    { "Lawn", grass }, { "LawnEdge", grass },
                    { "GymFloor", rubber },
                    { "Roof", metal }, { "BlackSteel", metal }, { "Mailbox", metal }, { "DarkCap", metal },
                    { "Fabric_main", fabric }, { "Cushion_main", fabric }, { "Rug_main", fabric }, { "RugBorder", fabric },
                    { "Leaves", foliage },
                };
            }
            return byRenderMaterial.TryGetValue(renderMaterial, out var p) ? p : null;
        }

        /// <summary>Gives every collider under root the physics material that matches what it looks like.</summary>
        public static void AssignSurfaces(Transform root)
        {
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
            {
                var r = c.GetComponent<Renderer>();
                if (!r || !r.sharedMaterial) continue;
                var pm = For(r.sharedMaterial.name);
                if (pm) c.sharedMaterial = pm;
            }
        }

        // ---------- furniture ----------

        public static void MakeSolid(GameObject go, FurnitureSpec spec)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (spec.dynamic)
                {
                    // PhysX caps convex hulls at 256 polygons, far below our high poly parts, so moving
                    // pieces get one fitted box per part. The parts are close to boxes, so it stays accurate.
                    var bc = mf.gameObject.AddComponent<BoxCollider>();
                    bc.center = mf.sharedMesh.bounds.center;
                    bc.size = mf.sharedMesh.bounds.size;
                }
                else
                {
                    var mc = mf.gameObject.AddComponent<MeshCollider>(); // static pieces can use the exact mesh
                    mc.sharedMesh = mf.sharedMesh;
                }
            }
            AssignSurfaces(go.transform);
            if (!spec.dynamic) return;

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = spec.mass;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.2f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = spec.mass < 5f ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.Discrete;
        }
    }
}
