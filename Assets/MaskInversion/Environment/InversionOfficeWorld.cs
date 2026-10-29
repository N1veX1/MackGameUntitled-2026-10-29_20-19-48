using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Two visual ages of Tutorial 2. Gameplay colliders remain owned by the gates.</summary>
[ExecuteAlways]
public sealed class InversionOfficeWorld : MonoBehaviour
{
    public GameObject overgrownWorld;
    public GameObject restoredWorld;
    public Material abandonedWall, restoredWall, abandonedFloor, restoredFloor, restoredBridge;
    [Tooltip("Editor preview only; Play Mode follows the mask.")]
    public bool previewWithMask;
    public Color abandonedAmbient = new Color(.42f,.44f,.42f);
    public Color restoredAmbient = new Color(.51f,.51f,.47f);
    public Color nightFogColor = new Color(.025f,.065f,.043f);
    [Range(0,.08f)] public float nightFogDensity = .016f;
    public bool IsRestored { get; private set; }
    public MaskInversionPlayer Player => player;
    MaskInversionPlayer player;
    readonly List<Surface> surfaces = new List<Surface>();
    readonly List<Light> oldLights = new List<Light>();
    readonly List<bool> lightEnabled = new List<bool>();
    Color oldAmbient, oldFogColor;
    AmbientMode oldAmbientMode;
    bool oldFog, captured, boundForPlay;
    float oldFogDensity;
    sealed class Surface
    {
        public Renderer renderer;
        public Material[] original;
        public bool enabled;
        public int kind; // 0 wall, 1 floor, 2 bridge, 3 replaced with roots, 4 ceiling
    }

    void OnEnable() { Refresh(); }
    void Start() { Refresh(); }
    void Update()
    {
        if (!captured) Refresh();
        else if (boundForPlay != Application.isPlaying) { Bind(); Refresh(); }
    }
    bool TrySetup()
    {
        if (captured) return true;
        // OnEnable can run while Unity is still deserializing a saved scene.
        if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded || gameObject.scene.name != "Level02_Tutorial") return false;
        Capture(); Bind(); return true;
    }
    void Refresh() { if (TrySetup()) ApplyState(Application.isPlaying && player ? player.MaskOn : previewWithMask); }
    void Bind()
    {
        if (player) player.MaskStateChanged -= ApplyState;
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            var found = root.GetComponentInChildren<MaskInversionPlayer>(true);
            if (found) { player = found; break; }
        }
        if (Application.isPlaying && player) player.MaskStateChanged += ApplyState;
        boundForPlay = Application.isPlaying;
    }
    void Capture()
    {
        if (captured) return;
        oldAmbient = RenderSettings.ambientLight; oldAmbientMode = RenderSettings.ambientMode;
        oldFog = RenderSettings.fog; oldFogColor = RenderSettings.fogColor; oldFogDensity = RenderSettings.fogDensity;
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            if (root == gameObject) continue;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponentInParent<MaskInversionPlayer>()) continue;
                string n = r.gameObject.name;
                var gate = r.GetComponent<MaskInversionGate>(); int kind = -1;
                if (n.StartsWith("Tutorial sign")) kind = 5;
                else if (gate) kind = gate.Kind == MaskInversionGate.GateKind.Wall ? (gate.ExistsWithMaskOn ? 3 : 0) : (gate.ExistsWithMaskOn ? 2 : 7);
                else if (n.StartsWith("Level 2 main floor")) kind = 1;
                else if (n == "Segmented ceiling") kind = 4;
                else if (n == "Left concrete wall" || n == "Right concrete wall") kind = 6;
                else if (n.Contains("concrete wall") || n.Contains("back wall") || n.StartsWith("End wall") || n.StartsWith("Door opening")) kind = 0;
                if (kind >= 0) surfaces.Add(new Surface { renderer = r, original = r.sharedMaterials, enabled = r.enabled, kind = kind });
            }
            foreach (var l in root.GetComponentsInChildren<Light>(true))
            {
                oldLights.Add(l); lightEnabled.Add(l.enabled); l.enabled = false;
            }
        }
        captured = true;
    }
    public void ApplyState(bool maskOn)
    {
        if (!TrySetup()) return;
        IsRestored = maskOn;
        if (overgrownWorld) overgrownWorld.SetActive(!maskOn);
        if (restoredWorld) restoredWorld.SetActive(maskOn);
        foreach (var s in surfaces)
        {
            if (!s.renderer) continue;
            if (s.kind == 3 || s.kind == 5 || s.kind == 6) { s.renderer.enabled = false; continue; }
            if (s.kind == 7) continue; // Root bridge owns its mesh, materials and collision.
            if (s.kind == 4) s.renderer.enabled = maskOn;
            Material m = s.kind == 1 ? (maskOn ? restoredFloor : abandonedFloor) :
                s.kind == 2 ? restoredBridge : (maskOn ? restoredWall : abandonedWall);
            if (m) s.renderer.sharedMaterial = m;
        }
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = maskOn ? restoredAmbient : abandonedAmbient;
        RenderSettings.fog = !maskOn;
        RenderSettings.fogColor = nightFogColor;
        RenderSettings.fogDensity = nightFogDensity;
    }
    void LateUpdate()
    {
        if (!captured) return;
        // Gate.Apply also controls its primitive renderer. Roots replace that visual,
        // while the gate's existing collider still controls passage and crushing.
        foreach (var s in surfaces) if (s.kind == 3 && s.renderer) s.renderer.enabled = false;
        if (!Application.isPlaying && IsRestored != previewWithMask) ApplyState(previewWithMask);
    }
    [ContextMenu("Preview / Overgrown, mask off")]
    public void PreviewOvergrown() { previewWithMask = false; if (!Application.isPlaying) ApplyState(false); }
    [ContextMenu("Preview / Restored, mask on")]
    public void PreviewRestored() { previewWithMask = true; if (!Application.isPlaying) ApplyState(true); }
    void OnDisable()
    {
        if (player) player.MaskStateChanged -= ApplyState;
        if (!captured) return;
        foreach (var s in surfaces) if (s.renderer) { s.renderer.sharedMaterials = s.original; s.renderer.enabled = s.enabled; }
        for (int i=0;i<oldLights.Count;i++) if (oldLights[i]) oldLights[i].enabled = lightEnabled[i];
        RenderSettings.ambientLight=oldAmbient; RenderSettings.ambientMode=oldAmbientMode;
        RenderSettings.fog=oldFog; RenderSettings.fogColor=oldFogColor; RenderSettings.fogDensity=oldFogDensity;
        surfaces.Clear(); oldLights.Clear(); lightEnabled.Clear(); captured=false;
    }
}
