using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Assigned obstacles merge only in an aligned, unobstructed camera projection.</summary>
[DefaultExecutionOrder(1100)] // Evaluate after the camera's final LateUpdate pose.
public sealed class MaskProximityWall : MonoBehaviour
{
    [Min(0)] public float fullyTransparentDistance = .14f;
    [Min(.1f)] public float visibleDistance = .40f;
    [Tooltip("Time to merge while approaching, in seconds.")]
    [Min(.01f)] public float fadeSeconds = .06f;
    [Tooltip("Time to restore the wall after moving away, in seconds.")]
    [Min(.01f)] public float reappearSeconds = .15f;
    [Tooltip("Require contact before merging. Disable for a wall that merges whenever its projection aligns.")]
    public bool requireProximity = true;
    public float Opacity { get; private set; } = 1;
    public Bounds ObstacleBounds => surfaces != null && surfaces.Length > 0 && surfaces[0] ? surfaces[0].bounds : new Bounds();
    static readonly List<MaskProximityWall> activeWalls = new List<MaskProximityWall>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetActiveWalls() => activeWalls.Clear();

    // Evaluate the upcoming step before CharacterController.Move can hit the wall.
    // Visibility and camera settling are still required; this does not open other axes.
    public static void PreparePlayerMovement(Transform target, Vector3 displacement)
    {
        displacement.y = 0;
        if (!target || displacement.sqrMagnitude < .000001f) return;
        foreach (var wall in activeWalls)
            if (wall && wall.player == target && wall.isActiveAndEnabled)
                wall.PrepareMovement(displacement);
    }

    void PrepareMovement(Vector3 displacement)
    {
        if (!player || surfaces == null || !activeDimension || !cameraEffects ||
            !cameraEffects.IsViewSettled || !CanVisiblyMerge()) return;
        var body = player.GetComponent<CharacterController>();
        float radius = body ? body.radius : 0;
        foreach (var surface in surfaces)
        {
            if (!surface) continue;
            Vector3 toward = surface.bounds.ClosestPoint(player.position) - player.position;
            toward.y = 0;
            float length = toward.magnitude;
            if (length < .00001f) continue;
            float approach = Vector3.Dot(displacement, toward / length);
            float gap = Mathf.Max(0, length - radius);
            if (approach <= 0 || gap > approach + fullyTransparentDistance) continue;
            // Finish the short merge before this step reaches contact, including
            // a slow frame. Tick(0) applies the same visual/collision safeguards.
            Opacity = 0;
            Tick(0);
            break;
        }
    }

    Renderer[] surfaces;
    Collider[] solids;
    Material[][] originals, transparent;
    bool[] collisionStates;
    bool[] rendererStates;
    ShadowCastingMode[] shadows;
    Transform player;
    bool activeDimension;
    Renderer mergeSurface;
    MaskBackingAppearance backingAppearance;
    public bool IsAppearanceMatched { get; private set; }
    public void ConfigureMerge(Renderer surface, bool requireNearPlayer = true, bool matchAppearance = false, Renderer appearanceReference = null)
    {
        backingAppearance?.Dispose();
        mergeSurface = surface; requireProximity = requireNearPlayer;
        backingAppearance = matchAppearance ? new MaskBackingAppearance(surface, surfaces, appearanceReference) : null;
    }
    MaskViewEffects cameraEffects;

    public void Initialize(Renderer[] renderers, Collider[] colliders, Transform target, MaskViewEffects viewEffects = null)
    {
        if (cameraEffects) cameraEffects.ViewTransitionStarted -= ForceSolidDuringTransition;
        cameraEffects = viewEffects ? viewEffects : GetComponent<MaskViewEffects>();
        surfaces = renderers ?? new Renderer[0]; solids = colliders ?? new Collider[0]; player = target;
        originals = new Material[surfaces.Length][]; transparent = new Material[surfaces.Length][];
        rendererStates = new bool[surfaces.Length];
        shadows = new ShadowCastingMode[surfaces.Length]; collisionStates = new bool[solids.Length];
        for (int i=0;i<solids.Length;i++) if (solids[i]) collisionStates[i] = solids[i].enabled;
        for (int i=0;i<surfaces.Length;i++)
        {
            if (!surfaces[i]) continue;
            rendererStates[i] = surfaces[i].enabled;
            shadows[i] = surfaces[i].shadowCastingMode;
            originals[i] = surfaces[i].sharedMaterials;
            transparent[i] = new Material[originals[i].Length];
            for (int j=0;j<originals[i].Length;j++)
            {
                if (!originals[i][j]) continue;
                var material = new Material(originals[i][j]); transparent[i][j] = material;
                material.SetFloat("_Surface", 1); material.SetFloat("_ZWrite", 0);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.DisableKeyword("_ALPHAMODULATE_ON");
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_Blend", 0);
                material.SetFloat("_BlendModePreserveSpecular", 0);
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
        }
        RegisterActiveWall();
        SubscribeToCamera();
    }
    public void SetDimension(bool active) => activeDimension = active;
    void OnEnable()
    {
        RegisterActiveWall();
        SubscribeToCamera();
    }
    void RegisterActiveWall()
    {
        if (!activeWalls.Contains(this)) activeWalls.Add(this);
    }
    void SubscribeToCamera()
    {
        if (!cameraEffects) return;
        cameraEffects.ViewTransitionStarted -= ForceSolidDuringTransition;
        if (isActiveAndEnabled) cameraEffects.ViewTransitionStarted += ForceSolidDuringTransition;
    }
    void ForceSolidDuringTransition()
    {
        Opacity = 1;
        backingAppearance?.Restore(); IsAppearanceMatched = false;
        if (surfaces == null) return;
        for (int i = 0; i < surfaces.Length; i++)
        {
            if (!surfaces[i]) continue;
            surfaces[i].enabled = rendererStates[i];
            surfaces[i].sharedMaterials = originals[i];
            surfaces[i].shadowCastingMode = shadows[i];
        }
        // Keep colliders untouched here. Tick restores them only after its
        // occupancy check, so changing views cannot trap a player inside a wall.
    }
    void LateUpdate() => Tick(Time.deltaTime);
    public void Tick(float deltaTime)
    {
        if (surfaces == null) return;
        float distance = float.PositiveInfinity;
        if (player)
            foreach (var surface in surfaces)
                if (surface) distance = Mathf.Min(distance, Vector3.Distance(player.position, surface.bounds.ClosestPoint(player.position)));
        var head = player ? player.GetComponent<CharacterController>() : null;
        if (head) distance = Mathf.Max(0, distance - head.radius);
        float target = 1;
        bool aligned = activeDimension && cameraEffects && cameraEffects.IsViewSettled && CanVisiblyMerge();
        if (aligned)
            target = requireProximity
                ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(fullyTransparentDistance, Mathf.Max(visibleDistance, fullyTransparentDistance + .1f), distance))
                : 0;
        Opacity = cameraEffects && !cameraEffects.IsViewSettled
            ? 1f
            : Mathf.MoveTowards(Opacity, target, deltaTime / Mathf.Max(.01f, target < Opacity ? fadeSeconds : reappearSeconds));
        bool matchAppearance = aligned && (distance < visibleDistance || (IsAppearanceMatched && Opacity < .999f));
        IsAppearanceMatched = matchAppearance && backingAppearance != null &&
            backingAppearance.Apply(cameraEffects.ViewCamera, Opacity);
        if (!IsAppearanceMatched) backingAppearance?.Restore();
        for (int i=0;i<surfaces.Length;i++)
        {
            if (!surfaces[i]) continue;
            // End the fade by removing the draw call: no opaque/specular remnants.
            surfaces[i].enabled = rendererStates[i] && Opacity > .001f;
            bool fading = Opacity < .999f;
            surfaces[i].sharedMaterials = IsAppearanceMatched ? backingAppearance.ForObstacle(i) : (fading ? transparent[i] : originals[i]);
            surfaces[i].shadowCastingMode = fading || IsAppearanceMatched ? ShadowCastingMode.Off : shadows[i];
            if (!fading || IsAppearanceMatched) continue;
            for (int j=0;j<transparent[i].Length;j++)
            {
                var mat = transparent[i][j]; if (!mat) continue;
                string property = mat.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                Color color = originals[i][j].GetColor(property); color.a *= Opacity; mat.SetColor(property, color);
            }
        }
        var body = player ? player.GetComponent<CharacterController>() : null;
        for (int i=0;i<solids.Length;i++)
        {
            if (!solids[i]) continue;
            // Do not recreate collision inside the player's capsule when leaving X.
            bool occupied = !solids[i].enabled && body && solids[i].GetComponent<Renderer>() &&
                solids[i].GetComponent<Renderer>().bounds.Intersects(body.bounds);
            solids[i].enabled = collisionStates[i] && Opacity > .15f && !occupied;
        }
    }
    // Test the actual game camera, not Scene-view visibility or distance alone.
    public bool CanVisiblyMerge()
    {
        if (!cameraEffects || !cameraEffects.ViewCamera || surfaces.Length == 0 || !surfaces[0]) return false;
        Camera camera = cameraEffects.ViewCamera;
        Bounds bounds = surfaces[0].bounds;
        if (mergeSurface && !FitsBackingProjection(camera, bounds)) return false;
        Vector3 direction = camera.transform.forward;
        Vector3[] samples = { bounds.center, bounds.center + Vector3.up * bounds.extents.y * .65f,
            bounds.center - Vector3.up * bounds.extents.y * .65f };
        foreach (Vector3 point in samples)
        {
            Vector3 viewport = camera.WorldToViewportPoint(point);
            if (viewport.z <= 0 || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1) continue;
            Ray ray = camera.ViewportPointToRay(viewport);
            if (!bounds.IntersectRay(ray, out float entry)) continue;
            bool blocked = false;
            foreach (RaycastHit hit in Physics.RaycastAll(ray, entry + .01f, ~0, QueryTriggerInteraction.Ignore))
            {
                bool own = System.Array.IndexOf(solids, hit.collider) >= 0;
                bool character = player && hit.transform.IsChildOf(player);
                if (!own && !character && hit.distance < entry - .025f) { blocked = true; break; }
            }
            if (blocked) continue;
            // The adjacent surface must occupy the same projected location behind the obstacle.
            float extent = Mathf.Abs(direction.x) * bounds.extents.x + Mathf.Abs(direction.y) * bounds.extents.y + Mathf.Abs(direction.z) * bounds.extents.z;
            Ray behind = new Ray(point + direction * (extent + .02f), direction);
            if (!mergeSurface || (mergeSurface.enabled && mergeSurface.bounds.IntersectRay(behind, out _))) return true;
        }
        return false;
    }

    bool FitsBackingProjection(Camera camera, Bounds obstacle)
    {
        if (!mergeSurface.enabled || !camera.orthographic) return false;
        Vector3 right = camera.transform.right, up = camera.transform.up, forward = camera.transform.forward;
        float halfWidth = ProjectExtent(obstacle.extents, right) * .995f;
        float halfHeight = ProjectExtent(obstacle.extents, up) * .995f;
        float depth = ProjectExtent(obstacle.extents, forward) + .02f;
        // Matching the center alone can remove a wall whose edges still stand clear
        // of the neighbouring surface. All four projected corners must fit behind it.
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            {
                Vector3 point = obstacle.center + right * (x * halfWidth) + up * (y * halfHeight);
                if (!mergeSurface.bounds.IntersectRay(new Ray(point + forward * depth, forward), out _)) return false;
            }
        return true;
    }

    static float ProjectExtent(Vector3 extents, Vector3 direction) =>
        Mathf.Abs(direction.x) * extents.x + Mathf.Abs(direction.y) * extents.y + Mathf.Abs(direction.z) * extents.z;

    public void Restore()
    {
        activeDimension = false; Opacity = 1;
        backingAppearance?.Restore(); IsAppearanceMatched = false;
        if (surfaces == null) return;
        for(int i=0;i<surfaces.Length;i++) if(surfaces[i]) { surfaces[i].enabled = rendererStates[i]; surfaces[i].sharedMaterials = originals[i]; surfaces[i].shadowCastingMode = shadows[i]; }
        for(int i=0;i<solids.Length;i++) if(solids[i]) solids[i].enabled = collisionStates[i];
    }
    void OnDisable()
    {
        activeWalls.Remove(this);
        if (cameraEffects) cameraEffects.ViewTransitionStarted -= ForceSolidDuringTransition;
        Restore();
    }
    void OnDestroy()
    {
        Restore();
        backingAppearance?.Dispose();
        if (transparent == null) return;
        foreach(var group in transparent) if(group != null) foreach(var material in group) if(material) Destroy(material);
    }
}


