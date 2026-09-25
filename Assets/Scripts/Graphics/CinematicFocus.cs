using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Tiramisu
{
    /// <summary>
    /// Keeps the depth of field focused on whatever the orbit camera is looking at,
    /// like a camera operator pulling focus. Things much nearer or further than the subject soften a little.
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
            float d = cam.distance;
            dof.nearFocusStart.value = 0f;
            dof.nearFocusEnd.value = d * 0.35f;
            dof.farFocusStart.value = d * 1.6f;
            dof.farFocusEnd.value = d * 5f;
        }
    }
}
