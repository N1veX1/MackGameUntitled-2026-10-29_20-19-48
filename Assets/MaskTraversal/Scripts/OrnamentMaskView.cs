using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Visible original mask in the editor; excluded only from the player's camera at runtime.</summary>
public sealed class OrnamentMaskView : MonoBehaviour
{
    [SerializeField] Renderer cosmicOverlay;
    [SerializeField] Light interiorLight;
    [SerializeField] Vector3 wornPosition = new Vector3(0, -.02f, .09f);
    [SerializeField, Range(0, 1)] float cosmicIntensity = .25f;
    Camera view;
    GameObject mask;
    Renderer[] maskRenderers;
    CornerMaskTraversalController controller;
    MaterialPropertyBlock properties;
    float amount, pulse;
    public bool Visible => mask && mask.activeInHierarchy && maskRenderers != null && view &&
        System.Array.Exists(maskRenderers, r => r && r.enabled && (view.cullingMask & (1 << r.gameObject.layer)) != 0);
    public void Configure(Camera camera, GameObject originalMask, CornerMaskTraversalController owner)
    {
        view = camera; mask = originalMask; controller = owner;
        view.nearClipPlane = .0015f;
        // Ignore Raycast is visible in Scene View. Only this player's camera excludes it.
        view.cullingMask &= ~(1 << 2);
        mask.transform.SetParent(view.transform, false);
        mask.transform.localScale = Vector3.one;
        SetLayer(mask.transform);
        maskRenderers = mask.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in maskRenderers) { renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.enabled = true; }
        properties ??= new MaterialPropertyBlock();
        FitOverlay();
    }
    public void ApplyState(bool worn)
    {
        if (!mask) return;
        mask.SetActive(!Application.isPlaying || worn);
        mask.transform.localPosition = wornPosition; mask.transform.localRotation = Quaternion.identity;
        SetMeshVisible(true);
        amount = Application.isPlaying && worn ? cosmicIntensity : 0;
        if (interiorLight) interiorLight.enabled = false;
        ApplyOverlay();
    }
    public void AnimateDonning(float progress, bool equip)
    {
        // Compatibility for older preview tools. Mask animations have been removed.
    }
    void SetMeshVisible(bool visible)
    {
        if (maskRenderers == null) return;
        foreach (var renderer in maskRenderers)
            if (renderer) renderer.enabled = visible;
    }
    void LateUpdate()
    {
        if (!controller) return;
        pulse = Mathf.Lerp(pulse, controller.TurnIntensity, 1 - Mathf.Exp(-Time.deltaTime * 6));
        amount = Mathf.MoveTowards(amount, controller.MaskOn ? cosmicIntensity : 0, Time.deltaTime * 3);
        FitOverlay(); ApplyOverlay();
    }
    void FitOverlay()
    {
        if (!view || !cosmicOverlay) return;
        const float depth = .012f;
        float height = Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad * .5f) * depth * 2;
        cosmicOverlay.transform.localPosition = Vector3.forward * depth;
        cosmicOverlay.transform.localRotation = Quaternion.identity;
        cosmicOverlay.transform.localScale = new Vector3(height * view.aspect, height, 1);
    }
    void ApplyOverlay()
    {
        if (!cosmicOverlay) return;
        properties ??= new MaterialPropertyBlock();
        properties.SetFloat("_Amount", amount); properties.SetFloat("_Turn", pulse);
        properties.SetFloat("_Aspect", view ? view.aspect : 1.7778f);
        cosmicOverlay.SetPropertyBlock(properties); cosmicOverlay.enabled = amount > .001f;
    }
    static void SetLayer(Transform node) { node.gameObject.layer = 2; foreach (Transform child in node) SetLayer(child); }
}
