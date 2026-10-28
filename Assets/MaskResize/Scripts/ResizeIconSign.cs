using UnityEngine;

namespace MaskResize
{
    // Shared materials; no per-frame texture generation or material instances.
    [ExecuteAlways, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ResizeIconSign : MonoBehaviour
    {
        [Tooltip("Optional: use the dimensions allowed by this object instead of the manual flags.")]
        public ResizableMaskObject target;
        public ResizeIconAxes axes = ResizeIconAxes.All;
        ResizeIconAxes applied = (ResizeIconAxes)(-1);
        MeshFilter filter;
        MeshRenderer surface;
        void OnEnable() { applied = (ResizeIconAxes)(-1); Refresh(); }
        void LateUpdate() => Refresh();
        public void Refresh()
        {
            var value = target ? ResizeIconLibrary.For(target) : axes;
            if (!filter) filter = GetComponent<MeshFilter>();
            if (!surface) surface = GetComponent<MeshRenderer>();
            var mesh = ResizeIconLibrary.Quad;
            var material = ResizeIconLibrary.Get(value);
            if (value == applied && filter.sharedMesh == mesh && surface.sharedMaterial == material) return;
            filter.sharedMesh = mesh;
            surface.sharedMaterial = material;
            surface.enabled = value != ResizeIconAxes.None && material && mesh;
            surface.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            surface.receiveShadows = false;
            applied = value;
        }
    }
}
