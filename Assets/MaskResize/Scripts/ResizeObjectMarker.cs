using UnityEngine;

namespace MaskResize
{
    // Six fixed-size surface signs remain readable when the solid changes dimensions.
    [ExecuteAlways]
    public sealed class ResizeObjectMarker : MonoBehaviour
    {
        [SerializeField] ResizableMaskObject owner;
        [SerializeField] Transform[] surfaceSigns;
        [SerializeField] LineRenderer outline;
        MaterialPropertyBlock properties;
        Renderer solidRenderer;
        bool selected;
        bool selectionApplied;
        ResizeIconAxes lastAxes = (ResizeIconAxes)(-1);
        Vector3 lastSize = Vector3.negativeInfinity;
        Vector3 lastCenter = Vector3.negativeInfinity;
        void OnDisable() { if(outline) outline.enabled=false; }
        public void Configure(ResizableMaskObject target, Transform[] signs, LineRenderer bounds)
        {
            owner = target; surfaceSigns = signs; outline = bounds;
            solidRenderer = null; selectionApplied = false; lastAxes = (ResizeIconAxes)(-1);
            SetSelection(false, ResizeDimension.Length); RefreshIcons(); UpdateGeometry();
        }
        void OnEnable()
        {
            if (!owner) owner = GetComponent<ResizableMaskObject>();
            lastSize = Vector3.negativeInfinity;
            selectionApplied = false;
            lastAxes = (ResizeIconAxes)(-1);
            SetSelection(false, ResizeDimension.Length); RefreshIcons(); UpdateGeometry();
        }
        public void SetSelection(bool value, ResizeDimension dimension)
        {
            bool changed = !selectionApplied || selected != value;
            selected = value;
            if (outline) outline.enabled = false;
            if (!changed) return;
            if (owner && owner.Solid)
            {
                if (!solidRenderer) solidRenderer = owner.Solid.GetComponent<Renderer>();
                if (solidRenderer)
                {
                    properties ??= new MaterialPropertyBlock();
                    solidRenderer.GetPropertyBlock(properties);
                    properties.SetColor("_EmissionColor", value ? new Color(.02f, .22f, .18f) : Color.black);
                    solidRenderer.SetPropertyBlock(properties);
                    selectionApplied = true;
                }
            }
        }
        void LateUpdate() { RefreshIcons(); UpdateGeometry(); }
        public void RefreshIcons()
        {
            var axes = ResizeIconLibrary.For(owner);
            if (axes == lastAxes) return;
            var mesh = ResizeIconLibrary.Quad;
            var material = ResizeIconLibrary.Get(axes);
            if (!mesh || (axes != ResizeIconAxes.None && !material)) return;
            if (surfaceSigns != null) foreach (var sign in surfaceSigns)
            {
                if (!sign) continue;
                var filter = sign.GetComponent<MeshFilter>();
                var renderer = sign.GetComponent<MeshRenderer>();
                if (filter) filter.sharedMesh = mesh;
                if (renderer)
                {
                    renderer.sharedMaterial = material;
                    renderer.enabled = axes != ResizeIconAxes.None;
                    renderer.receiveShadows = false;
                }
            }
            lastAxes = axes;
        }
        void UpdateGeometry()
        {
            if (!owner || !owner.Solid) return;
            Vector3 size = owner.GeometrySize, center = owner.Solid.localPosition;
            if (size == lastSize && center == lastCenter) return;
            lastSize = size; lastCenter = center;
            Vector3 half = size * .5f;
            if (surfaceSigns != null && surfaceSigns.Length == 6)
            {
                Sign(0, center + Vector3.back * (half.z + .018f), Vector3.back, Vector3.up, Mathf.Min(size.x, size.y));
                Sign(1, center + Vector3.forward * (half.z + .018f), Vector3.forward, Vector3.up, Mathf.Min(size.x, size.y));
                Sign(2, center + Vector3.up * (half.y + .018f), Vector3.up, Vector3.forward, Mathf.Min(size.x, size.z));
                Sign(3, center + Vector3.down * (half.y + .018f), Vector3.down, Vector3.back, Mathf.Min(size.x, size.z));
                Sign(4, center + Vector3.left * (half.x + .018f), Vector3.left, Vector3.up, Mathf.Min(size.z, size.y));
                Sign(5, center + Vector3.right * (half.x + .018f), Vector3.right, Vector3.up, Mathf.Min(size.z, size.y));
            }

        }
        void Sign(int i, Vector3 position, Vector3 normal, Vector3 up, float available)
        {
            if (!surfaceSigns[i]) return;
            surfaceSigns[i].localPosition = position;
            surfaceSigns[i].localRotation = Quaternion.LookRotation(normal, up);
            surfaceSigns[i].localScale = Vector3.one * Mathf.Clamp(available * .65f, .1f, .85f);
        }
    }
}
