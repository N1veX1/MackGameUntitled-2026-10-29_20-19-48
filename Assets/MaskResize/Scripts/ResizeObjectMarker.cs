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
        Vector3 lastSize = Vector3.negativeInfinity;
        Vector3 lastCenter = Vector3.negativeInfinity;
        static readonly int[] EdgeWalk = { 0, 1, 2, 3, 0, 4, 5, 1, 5, 6, 2, 6, 7, 3, 7, 4 };
        readonly Vector3[] corners = new Vector3[8];
        readonly Vector3[] outlinePoints = new Vector3[16];
        public void Configure(ResizableMaskObject target, Transform[] signs, LineRenderer bounds)
        {
            owner = target; surfaceSigns = signs; outline = bounds;
            solidRenderer = null; selectionApplied = false;
            SetSelection(false, ResizeDimension.Length); UpdateGeometry();
        }
        void OnEnable()
        {
            if (!owner) owner = GetComponent<ResizableMaskObject>();
            lastSize = Vector3.negativeInfinity;
            selectionApplied = false;
            SetSelection(false, ResizeDimension.Length); UpdateGeometry();
        }
        public void SetSelection(bool value, ResizeDimension dimension)
        {
            bool changed = !selectionApplied || selected != value;
            selected = value;
            if (outline && outline.enabled != value) outline.enabled = value;
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
        void LateUpdate() { UpdateGeometry(); }
        void UpdateGeometry()
        {
            if (!owner || !owner.Solid) return;
            Vector3 size = owner.Size, center = owner.Solid.localPosition;
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
            if (!outline) return;
            half += Vector3.one * .012f;
            corners[0] = center + new Vector3(-half.x, -half.y, -half.z);
            corners[1] = center + new Vector3( half.x, -half.y, -half.z);
            corners[2] = center + new Vector3( half.x, -half.y,  half.z);
            corners[3] = center + new Vector3(-half.x, -half.y,  half.z);
            corners[4] = center + new Vector3(-half.x,  half.y, -half.z);
            corners[5] = center + new Vector3( half.x,  half.y, -half.z);
            corners[6] = center + new Vector3( half.x,  half.y,  half.z);
            corners[7] = center + new Vector3(-half.x,  half.y,  half.z);
            outline.positionCount = EdgeWalk.Length;
            for (int i = 0; i < EdgeWalk.Length; i++) outlinePoints[i] = corners[EdgeWalk[i]];
            outline.SetPositions(outlinePoints);
            outline.enabled = selected;
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
