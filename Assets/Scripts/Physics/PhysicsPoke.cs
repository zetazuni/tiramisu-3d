using UnityEngine;

namespace Tiramisu
{
    /// <summary>
    /// Test tool for the physics: click (or tap) anything that can move and it gets a shove
    /// away from the camera, scaled by its mass so a cushion flies and a sofa only budges.
    /// Will make way for proper interactions once decorate mode arrives.
    /// </summary>
    public class PhysicsPoke : MonoBehaviour
    {
        public float strength = 3f;
        public float maxImpulse = 160f;
        Camera cam;

        void Awake() => cam = GetComponent<Camera>();

        // LateUpdate so the orbit camera has already decided whether this was a click or a drag
        void LateUpdate()
        {
            var orbit = OrbitCamera.Instance;
            if (!orbit || !orbit.ClickedThisFrame || DecorateMode.Active) return;
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 200f)) return;
            var rb = hit.rigidbody;
            if (!rb || rb.isKinematic) return;
            Vector3 dir = (ray.direction + Vector3.up * 0.35f).normalized;
            float impulse = Mathf.Min(rb.mass * strength, maxImpulse);
            rb.AddForceAtPosition(dir * impulse, hit.point, ForceMode.Impulse);
        }
    }
}
