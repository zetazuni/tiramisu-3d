using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tiramisu
{
    /// <summary>
    /// Keeps the depth of field focused on whatever the orbit camera is looking at,
    /// like a camera operator pulling focus. Near and far things soften a little.
    /// </summary>
    public class CinematicFocus : MonoBehaviour
    {
        public Volume volume;
        DepthOfField dof;

        void Start()
        {
            if (volume && volume.profile) volume.profile.TryGet(out dof);
        }

        void LateUpdate()
        {
            var cam = OrbitCamera.Instance;
            if (dof == null || !cam) return;
            dof.focusDistance.value = cam.distance;
        }
    }
}
