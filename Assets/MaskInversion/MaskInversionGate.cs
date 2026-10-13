using UnityEngine;

public sealed class MaskInversionGate : MonoBehaviour
{
    public enum GateKind { Wall, Bridge }
    [SerializeField] GateKind kind;
    [Tooltip("For walls: opens with mask on. For bridges: exists with mask on.")]
    [SerializeField] bool existsWithMaskOn;
    [Header("Standalone prefab — automatically finds the player in this scene")]
    [SerializeField] MaskInversionPlayer player;
    [SerializeField, Range(0, .15f)] float wallContactMargin = .06f;
    bool subscribed;
    Renderer[] renderers;
    Collider[] colliders;
    public GateKind Kind => kind;
    public bool ExistsWithMaskOn => existsWithMaskOn;
    public bool IsBlocking { get; private set; }
    public Collider MainCollider => colliders != null && colliders.Length > 0 ? colliders[0] : null;

    public bool BlocksWithMask(bool maskOn) => kind == GateKind.Wall && maskOn != existsWithMaskOn;

    void OnEnable() { if (Application.isPlaying) Bind(); }
    void Start() { Bind(); }
    void Bind()
    {
        if (subscribed) return;
        // Tutorial-owned gates keep their existing ordered checkpoints and death handling.
        foreach (var tutorial in FindObjectsByType<MaskInversionTutorial>(FindObjectsSortMode.None))
            if (tutorial.gameObject.scene == gameObject.scene && tutorial.OwnsGate(this)) return;
        if (!player)
            foreach (var candidate in FindObjectsByType<MaskInversionPlayer>(FindObjectsSortMode.None))
                if (candidate.gameObject.scene == gameObject.scene) { player = candidate; break; }
        if (!player) return;
        player.MaskStateChanged += OnMaskChanged; subscribed = true; Apply(player.MaskOn);
    }
    void OnMaskChanged(bool maskOn)
    {
        Physics.SyncTransforms();
        bool crushed = !IsBlocking && BlocksWithMask(maskOn) && !player.IsRespawning
            && TouchesPlayer(player.Controller, wallContactMargin);
        Apply(maskOn);
        if (crushed) player.Respawn();
    }
    void OnDisable()
    {
        if (subscribed && player) player.MaskStateChanged -= OnMaskChanged;
        subscribed = false;
    }

    public bool TouchesPlayer(CharacterController player, float contactMargin)
    {
        if (!player || !player.enabled) return false;
        if (colliders == null) colliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider wall in colliders)
        {
            if (!wall || wall.isTrigger) continue;
            if (wall is BoxCollider box)
            {
                Vector3 scale = box.transform.lossyScale;
                Vector3 half = Vector3.Scale(box.size * .5f,
                    new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                // Use the actual capsule dimensions, independent of physics query
                // skin shrinkage and the disabled wall's empty Collider.bounds.
                Vector3 playerScale = player.transform.lossyScale;
                float radius = player.radius * Mathf.Max(Mathf.Abs(playerScale.x), Mathf.Abs(playerScale.z));
                Vector3 center = player.transform.TransformPoint(player.center);
                Vector3 axis = player.transform.up * Mathf.Max(0f, player.height * Mathf.Abs(playerScale.y) * .5f - radius);
                Quaternion inverse = Quaternion.Inverse(box.transform.rotation);
                Vector3 origin = box.transform.TransformPoint(box.center);
                Vector3 a = inverse * (center - axis - origin), b = inverse * (center + axis - origin);
                float lower = 0f, upper = 1f;
                // Squared point-to-box distance is convex along the capsule axis.
                for (int i = 0; i < 28; i++)
                {
                    float t0 = Mathf.Lerp(lower, upper, 1f / 3f), t1 = Mathf.Lerp(lower, upper, 2f / 3f);
                    if (BoxDistanceSquared(Vector3.Lerp(a, b, t0), half) < BoxDistanceSquared(Vector3.Lerp(a, b, t1), half)) upper = t1;
                    else lower = t0;
                }
                float distance = Mathf.Min(BoxDistanceSquared(a, half), BoxDistanceSquared(b, half),
                    BoxDistanceSquared(Vector3.Lerp(a, b, (lower + upper) * .5f), half));
                if (distance <= Mathf.Pow(radius + Mathf.Max(0f, contactMargin), 2f)) return true;
            }
            else if (Physics.ComputePenetration(player, player.transform.position, player.transform.rotation,
                wall, wall.transform.position, wall.transform.rotation, out _, out _)) return true;
        }
        return false;
    }

    static float BoxDistanceSquared(Vector3 point, Vector3 half)
    {
        Vector3 outside = new Vector3(Mathf.Max(0f, Mathf.Abs(point.x) - half.x),
            Mathf.Max(0f, Mathf.Abs(point.y) - half.y), Mathf.Max(0f, Mathf.Abs(point.z) - half.z));
        return outside.sqrMagnitude;
    }
    public void Configure(GateKind gateKind, bool maskOn)
    {
        kind = gateKind;
        existsWithMaskOn = maskOn;
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        Apply(false);
    }

    public void Apply(bool maskOn)
    {
        if (renderers == null) renderers = GetComponentsInChildren<Renderer>(true);
        if (colliders == null) colliders = GetComponentsInChildren<Collider>(true);
        bool active = kind == GateKind.Wall ? maskOn != existsWithMaskOn : maskOn == existsWithMaskOn;
        IsBlocking = kind == GateKind.Wall && active;
        foreach (Renderer renderer in renderers) if (renderer) renderer.enabled = active;
        foreach (Collider collider in colliders) if (collider) collider.enabled = active;
    }
}
