using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Original mask during the animation, with a clear first-person view once equipped.</summary>
public sealed class OrnamentMaskView : MonoBehaviour
{
    [SerializeField] Renderer cosmicOverlay;
    [SerializeField] Light interiorLight;
    [SerializeField] Vector3 wornPosition = new Vector3(-.052f, -.020f, -.015f);
    [SerializeField, Range(0, 1)] float cosmicIntensity = .25f;
    Camera view;
    GameObject mask;
    Renderer[] maskRenderers;
    MaskEquipAnimation animation;
    bool animating, equipping;
    CornerMaskTraversalController controller;
    MaterialPropertyBlock properties;
    float amount, pulse;
    public bool Visible => animation && animation.IsVisible;
    public void Configure(Camera camera, GameObject originalMask, CornerMaskTraversalController owner)
    {
        view = camera; mask = originalMask; controller = owner;
        view.nearClipPlane = .0015f;
        view.cullingMask |= 1; // Render the actual package model instead of hiding its layer.
        mask.transform.SetParent(view.transform, false);
        mask.transform.localScale = Vector3.one;
        SetLayer(mask.transform);
        maskRenderers = mask.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in maskRenderers) renderer.shadowCastingMode = ShadowCastingMode.Off;
        animation = MaskEquipAnimation.Attach(view, mask);
        properties ??= new MaterialPropertyBlock();
        FitOverlay();
    }
    public void ApplyState(bool worn)
    {
        if (!mask) return;
        if (animation) animation.End();
        animating = false;
        mask.SetActive(worn); mask.transform.localPosition = wornPosition; mask.transform.localRotation = Quaternion.identity;
        SetMeshVisible(false);
        amount = worn ? cosmicIntensity : 0;
        if (interiorLight) interiorLight.enabled = false;
        ApplyOverlay();
    }
    public void AnimateDonning(float progress, bool equip)
    {
        if (!animation) return;
        if (!animating || equipping != equip) { animation.Begin(equip); animating = true; equipping = equip; }
        animation.SetProgress(progress);
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
    static void SetLayer(Transform node) { node.gameObject.layer = 0; foreach (Transform child in node) SetLayer(child); }
}
