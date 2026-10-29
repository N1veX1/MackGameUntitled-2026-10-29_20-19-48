using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Render-only copy of the supplied mask; never duplicates gameplay scripts or colliders.</summary>
public sealed class MaskEquipAnimation : MonoBehaviour
{
    [SerializeField, Min(.05f)] float maskWidth = .36f;
    [SerializeField] Vector3 loweredPosition = new Vector3(0, -.52f, .46f);
    [SerializeField] Vector3 liftPosition = new Vector3(0, -.015f, .42f);
    [SerializeField] Vector3 facePosition = new Vector3(0, 0, .055f);
    Transform visual;
    Light faceLight;
    bool equipping;
    public bool IsVisible => visual && visual.gameObject.activeSelf;

    public static MaskEquipAnimation Attach(Camera camera, GameObject source)
    {
        if (!Application.isPlaying || !camera || !source) return null;
        var animation = camera.GetComponent<MaskEquipAnimation>();
        if (!animation) animation = camera.gameObject.AddComponent<MaskEquipAnimation>();
        animation.Build(source);
        return animation;
    }
    void Build(GameObject source)
    {
        if (visual) return;
        var root = new GameObject("Mask animation — visual only");
        visual = root.transform;
        CopyMeshes(source.transform, visual);
        var meshes = root.GetComponentsInChildren<Renderer>();
        if (meshes.Length > 0)
        {
            var bounds = meshes[0].bounds;
            foreach (var renderer in meshes) bounds.Encapsulate(renderer.bounds);
            float scale = maskWidth / Mathf.Max(.001f, bounds.size.x);
            foreach (Transform part in visual) part.position -= bounds.center;
            visual.localScale = Vector3.one * scale;
        }
        visual.SetParent(transform, false);
        var lamp = new GameObject("Mask animation light"); lamp.transform.SetParent(transform, false);
        lamp.transform.localPosition = new Vector3(0, .08f, .18f);
        faceLight = lamp.AddComponent<Light>(); faceLight.type = LightType.Point;
        faceLight.range = .85f; faceLight.intensity = .35f; faceLight.shadows = LightShadows.None;
        End();
    }
    static void CopyMeshes(Transform source, Transform parent)
    {
        var copy = new GameObject(source.name).transform;
        copy.SetParent(parent, false);
        copy.localPosition = source.localPosition; copy.localRotation = source.localRotation; copy.localScale = source.localScale;
        var filter = source.GetComponent<MeshFilter>(); var renderer = source.GetComponent<MeshRenderer>();
        if (filter && renderer && filter.sharedMesh)
        {
            copy.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var target = copy.gameObject.AddComponent<MeshRenderer>(); target.sharedMaterials = renderer.sharedMaterials;
            target.shadowCastingMode = ShadowCastingMode.Off; target.receiveShadows = false;
        }
        foreach (Transform child in source) CopyMeshes(child, copy);
    }
    public void Begin(bool equip)
    {
        if (!visual) return;
        equipping = equip; visual.gameObject.SetActive(true); faceLight.enabled = true; SetProgress(0);
    }
    public void SetProgress(float progress)
    {
        if (!visual) return;
        float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(equipping ? progress : 1 - progress));
        // Lift from below the screen, then draw the mask onto the face. Removal reverses the same arc.
        float remainder = 1 - t;
        visual.localPosition = remainder * remainder * loweredPosition + 2 * remainder * t * liftPosition + t * t * facePosition;
        visual.localRotation = Quaternion.Euler(Mathf.Lerp(-18, 0, t), 180, 0);
    }
    public void End()
    {
        if (visual) visual.gameObject.SetActive(false);
        if (faceLight) faceLight.enabled = false;
    }
    void OnDisable() => End();
}
