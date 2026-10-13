using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>The mask permits changes of gravity only along the authored rounded platform.</summary>
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class CornerMaskTraversalController : MonoBehaviour
{
    [Header("Editable platform route")]
    [SerializeField] Transform routeRoot;
    [SerializeField] RoundedCornerTransition[] roundedCorners;
    [SerializeField, Min(.1f)] float moveSpeed = 3.3f;
    [SerializeField] float startDistance = 2;
    [SerializeField, Min(.1f)] float platformHalfWidth = 2.2f;
    [SerializeField, Min(.01f)] float eyeHeight = .78f;
    [SerializeField] float gravityStrength = 18;
    [SerializeField] float fallResetDistance = 14;
    [SerializeField, Range(25, 120)] float maxTurnDegreesPerSecond = 60;
    [SerializeField, Min(0)] float stopBeforeMaskSeconds = .18f;
    [Header("Camera and original ornament mask")]
    [SerializeField] Camera playerCamera;
    [SerializeField] GameObject ornamentMask;
    [SerializeField] float mouseSensitivity = .11f;
    [SerializeField, Min(.01f)] float lookSmoothSeconds = .045f;
    [SerializeField, Min(1)] float inputAcceleration = 9f;
    [Header("Closed eyes during mask changes")]
    [SerializeField] float eyesCloseSeconds = .42f, eyesHoldSeconds = .12f, eyesOpenSeconds = .3f;

    Transform[] nodes;
    float[] lengths;
    Rigidbody body;
    OrnamentMaskView maskView;
    Vector2 movementRequested, freeMove;
    Vector2 smoothedInput;
    float smoothYaw, smoothPitch;
    float lastMotionTime = -100, lastGroundTime = -100, turnIntensity;
    string notice;
    float noticeUntil;
    float routeDistance, sideDistance, checkpoint, yaw, pitch, eyelid;
    bool maskOn, transitioning, cursorLocked, attached = true, complete, blocked;
    Vector3 gravityUp = Vector3.up, fallOrigin;
    Quaternion surfaceFrame = Quaternion.identity;
    string currentAxis = "Y";
    public float RouteDistance => routeDistance;
    public float RouteLength => lengths == null ? 0 : lengths[lengths.Length - 1];
    public bool MaskOn => maskOn;
    public string CurrentAxis => currentAxis;
    public bool Complete => complete;
    public bool GravityChangeBlocked => blocked;
    public Vector3 GravityDirection => -gravityUp;
    public float TurnIntensity => turnIntensity;
    public bool IsAttached => attached;
    public bool OnRoundedTransition => InCorner(routeDistance);
    public bool IsStationary => movementRequested.sqrMagnitude < .001f && Time.unscaledTime - lastMotionTime >= stopBeforeMaskSeconds
        && (attached || (body && Vector3.ProjectOnPlane(body.linearVelocity, gravityUp).sqrMagnitude < .01f));

    void Awake() { InitializeRoute(); }
    public void ConfigureRoute(Transform orderedNodes, RoundedCornerTransition[] corners, float halfWidth)
    {
        routeRoot = orderedNodes; roundedCorners = corners; platformHalfWidth = Mathf.Max(.5f, halfWidth);
    }
    public void InitializeRoute()
    {
        if (!playerCamera) playerCamera = GetComponentInChildren<Camera>(true);
        var list = new List<Transform>();
        if (routeRoot) foreach (Transform node in routeRoot) if (node.name.StartsWith("Route ")) list.Add(node);
        nodes = list.ToArray();
        if (nodes.Length < 2) { Debug.LogError("The platform requires ordered Route nodes.", this); enabled = false; return; }
        lengths = new float[nodes.Length];
        for (int i = 1; i < nodes.Length; i++) lengths[i] = lengths[i - 1] + Vector3.Distance(nodes[i - 1].position, nodes[i].position);
        body = GetComponent<Rigidbody>();
        body.useGravity = false; // Physics.gravity stays unchanged for other masks and world XYZ.
        body.isKinematic = true;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        var capsule = GetComponent<CapsuleCollider>();
        capsule.radius = .26f; capsule.height = 1.7f; capsule.center = new Vector3(0, -.04f, 0);
        checkpoint = Mathf.Clamp(startDistance, 0, RouteLength);
        routeDistance = checkpoint; sideDistance = 0; attached = true;
        transform.position = Sample(routeDistance, out surfaceFrame);
        transform.rotation = surfaceFrame; gravityUp = surfaceFrame * Vector3.up;
        if (playerCamera) { playerCamera.transform.localPosition = Vector3.up * eyeHeight; playerCamera.transform.localRotation = Quaternion.identity; }
        if (ornamentMask && playerCamera)
        {
            maskView = playerCamera.GetComponent<OrnamentMaskView>();
            if (maskView) maskView.Configure(playerCamera, ornamentMask, this);
        }
        lastMotionTime = -100; movementRequested = freeMove = Vector2.zero; complete = blocked = false;
        yaw = pitch = smoothYaw = smoothPitch = 0; smoothedInput = Vector2.zero;
        SetMaskState(false);
        if (Application.isPlaying) LockCursor(true);
    }
    void Update()
    {
        if (nodes == null || nodes.Length < 2) return;
        var keys = Keyboard.current;
        if (keys == null) return;
        if (keys.escapeKey.wasPressedThisFrame) LockCursor(false);
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !cursorLocked) LockCursor(true);
        if (!cursorLocked) return;
        movementRequested = new Vector2((keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0),
            (keys.wKey.isPressed || keys.upArrowKey.isPressed ? 1 : 0) - (keys.sKey.isPressed || keys.downArrowKey.isPressed ? 1 : 0));
        if (keys.mKey.wasPressedThisFrame && !transitioning) TryToggleMask();
        if (transitioning) { smoothedInput = Vector2.zero; return; }
        if (Mouse.current != null)
        {
            Vector2 look = Mouse.current.delta.ReadValue() * mouseSensitivity;
            yaw += look.x; pitch = Mathf.Clamp(pitch - look.y, -82, 82);
        }
        float follow = 1 - Mathf.Exp(-Time.unscaledDeltaTime / lookSmoothSeconds);
        smoothYaw = Mathf.LerpAngle(smoothYaw, yaw, follow); smoothPitch = Mathf.Lerp(smoothPitch, pitch, follow);
        if (playerCamera) playerCamera.transform.localRotation = Quaternion.Euler(smoothPitch, smoothYaw, 0);
        smoothedInput = Vector2.MoveTowards(smoothedInput, Vector2.ClampMagnitude(movementRequested, 1), inputAcceleration * Time.deltaTime);
        Vector2 input = smoothedInput;
        float angle = smoothYaw * Mathf.Deg2Rad;
        MoveOnPlatform(new Vector2(input.x * Mathf.Cos(angle) + input.y * Mathf.Sin(angle),
            input.y * Mathf.Cos(angle) - input.x * Mathf.Sin(angle)), Time.deltaTime);
        if (attached && !InCorner(routeDistance)) checkpoint = routeDistance;
    }
    // Input x = across the platform, y = along it, after projection from the camera.
    public void MoveOnPlatform(Vector2 input, float deltaTime)
    {
        freeMove = input;
        if (!attached || transitioning || lengths == null) return;
        Vector3 previousPosition = transform.position, previousUp = gravityUp;
        float desired = Mathf.Clamp(routeDistance + input.y * moveSpeed * deltaTime, 0, RouteLength);
        if (maskOn) desired = LimitTurnSpeed(routeDistance, desired, deltaTime);
        float allowed = maskOn ? desired : ClampToCurrentGravity(routeDistance, desired);
        blocked = Mathf.Abs(allowed - desired) > .0001f;
        routeDistance = allowed;
        // The floor fills the room. Its side walls are solid boundaries, not lethal ledges.
        sideDistance = Mathf.Clamp(sideDistance + input.x * moveSpeed * deltaTime, -platformHalfWidth + .3f, platformHalfWidth - .3f);
        Vector3 center = Sample(routeDistance, out Quaternion frame);
        if (maskOn || Vector3.Angle(gravityUp, frame * Vector3.up) < .1f) surfaceFrame = frame;
        if (maskOn) gravityUp = frame * Vector3.up;
        transform.SetPositionAndRotation(center + (frame * Vector3.right) * sideDistance, surfaceFrame);
        currentAxis = AxisName(gravityUp); complete = routeDistance >= RouteLength - 1.2f;
        if ((transform.position - previousPosition).sqrMagnitude > .000001f) lastMotionTime = Time.unscaledTime;
        turnIntensity = Mathf.Clamp01(Vector3.Angle(previousUp, gravityUp) / Mathf.Max(.001f, deltaTime * maxTurnDegreesPerSecond));
    }
    float LimitTurnSpeed(float from, float to, float dt)
    {
        // Subdivide long steps: a complete pair of bends may finish with the same up vector.
        float allowed = from, angleBudget = maxTurnDegreesPerSecond * Mathf.Max(0, dt);
        int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(to - from) / .025f));
        Sample(from, out Quaternion previous);
        for (int i = 1; i <= steps; i++)
        {
            float next = Mathf.Lerp(from, to, i / (float)steps); Sample(next, out Quaternion frame);
            float angle = Quaternion.Angle(previous, frame);
            if (angle > angleBudget) return Mathf.Lerp(allowed, next, angleBudget / Mathf.Max(.0001f, angle));
            angleBudget -= angle; allowed = next; previous = frame;
        }
        return to;
    }
    float ClampToCurrentGravity(float from, float to)
    {
        if (Mathf.Approximately(from, to)) return from;
        // Check the entire interval so a slow frame cannot skip a complete corner.
        int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(to - from) / .035f));
        float good = from;
        for (int step = 1; step <= steps; step++)
        {
            float candidate = Mathf.Lerp(from, to, step / (float)steps);
            Sample(candidate, out Quaternion frame);
            if (Vector3.Angle(gravityUp, frame * Vector3.up) > .01f)
            {
                float bad = candidate;
                for (int i = 0; i < 12; i++)
                {
                    float mid = (good + bad) * .5f; Sample(mid, out frame);
                    if (Vector3.Angle(gravityUp, frame * Vector3.up) <= .01f) good = mid; else bad = mid;
                }
                return good;
            }
            good = candidate;
        }
        return to;
    }
    bool InCorner(float distance)
    {
        if (roundedCorners != null) foreach (var corner in roundedCorners)
                if (corner && distance > corner.startDistance + .015f && distance < corner.endDistance - .015f) return true;
        return false;
    }
    Vector3 Sample(float distance, out Quaternion rotation)
    {
        int i = 0;
        while (i < lengths.Length - 2 && lengths[i + 1] < distance) i++;
        float t = Mathf.InverseLerp(lengths[i], lengths[i + 1], distance);
        rotation = Quaternion.Slerp(nodes[i].rotation, nodes[i + 1].rotation, t);
        return Vector3.Lerp(nodes[i].position, nodes[i + 1].position, t);
    }
    public bool SetMaskState(bool worn)
    {
        if (!worn && maskOn && InCorner(routeDistance)) return false;
        maskOn = worn;
        if (maskView) maskView.ApplyState(worn);
        else if (ornamentMask) ornamentMask.SetActive(worn);
        // Both toggles preserve the current gravity and frame.
        return true;
    }
    public bool TryToggleMask()
    {
        if (transitioning) return false;
        if (maskOn && InCorner(routeDistance)) { Notify("СНАЧАЛА ЗАКОНЧИ ПЕРЕХОД НА ДРУГОЙ ПОЛ"); return false; }
        if (!IsStationary || (!attached && Time.time - lastGroundTime > .15f)) { Notify("ОСТАНОВИСЬ, ЧТОБЫ СМЕНИТЬ МАСКУ"); return false; }
        StartCoroutine(SwitchMask()); return true;
    }
    void Notify(string message) { notice = message; noticeUntil = Time.unscaledTime + 2; }
    public void ReleaseToPhysics()
    {
        attached = false; fallOrigin = transform.position;
        body.isKinematic = false; body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
    }
    void FixedUpdate()
    {
        if (attached || !body) return;
        Vector3 lateral = surfaceFrame * new Vector3(freeMove.x, 0, freeMove.y);
        Vector3 planar = transitioning ? Vector3.zero : lateral * moveSpeed;
        body.linearVelocity = planar + Vector3.Project(body.linearVelocity, gravityUp);
        body.AddForce(GravityDirection * gravityStrength, ForceMode.Acceleration);
        if (Time.time - lastGroundTime < .15f) fallOrigin = transform.position;
        else if (Vector3.Dot(transform.position - fallOrigin, GravityDirection) > fallResetDistance) Respawn();
    }
    void OnCollisionEnter(Collision collision)
    {
        if (attached) return;
        foreach (var contact in collision.contacts)
            if (Vector3.Dot(contact.normal, gravityUp) > .55f) { HandleLanding(contact.point, contact.normal); break; }
    }
    void OnCollisionStay(Collision collision) { OnCollisionEnter(collision); }
    public bool HandleLanding(Vector3 contact, Vector3 normal)
    {
        if (Vector3.Dot(normal, gravityUp) < .55f) return false;
        lastGroundTime = Time.time;
        Vector3 desiredCenter = contact + gravityUp * .9f;
        float best = float.PositiveInfinity, bestDistance = 0, bestSide = 0;
        for (int i = 0; i < nodes.Length - 1; i++)
        {
            Vector3 right = nodes[i].right;
            float side = Vector3.Dot(desiredCenter - nodes[i].position, right);
            if (Mathf.Abs(side) > platformHalfWidth) continue;
            Vector3 flat = desiredCenter - right * side, delta = nodes[i + 1].position - nodes[i].position;
            float t = Mathf.Clamp01(Vector3.Dot(flat - nodes[i].position, delta) / Mathf.Max(.00001f, delta.sqrMagnitude));
            var frame = Quaternion.Slerp(nodes[i].rotation, nodes[i + 1].rotation, t);
            if (Vector3.Angle(frame * Vector3.up, gravityUp) > 3) continue;
            float error = (flat - Vector3.Lerp(nodes[i].position, nodes[i + 1].position, t)).sqrMagnitude;
            if (error < best) { best = error; bestDistance = Mathf.Lerp(lengths[i], lengths[i + 1], t); bestSide = side; }
        }
        if (best > .12f * .12f) return false; // A different valid floor: continue physics-based walking on it.
        body.linearVelocity = Vector3.zero; body.isKinematic = true; attached = true;
        routeDistance = bestDistance; sideDistance = Mathf.Clamp(bestSide, -platformHalfWidth + .3f, platformHalfWidth - .3f);
        Vector3 center = Sample(routeDistance, out Quaternion landedFrame);
        transform.SetPositionAndRotation(center + landedFrame * Vector3.right * sideDistance, surfaceFrame);
        return true;
    }
    public void Respawn()
    {
        body.isKinematic = true; attached = true; routeDistance = checkpoint; sideDistance = 0;
        transform.position = Sample(routeDistance, out surfaceFrame); transform.rotation = surfaceFrame;
        gravityUp = surfaceFrame * Vector3.up; yaw = pitch = smoothYaw = smoothPitch = 0; smoothedInput = Vector2.zero; complete = blocked = false;
        currentAxis = AxisName(gravityUp);
    }
    IEnumerator SwitchMask()
    {
        transitioning = true; freeMove = Vector2.zero; bool equip = !maskOn;
        for (float t = 0; t < eyesCloseSeconds; t += Time.unscaledDeltaTime)
        {
            float progress = Mathf.SmoothStep(0, 1, t / eyesCloseSeconds);
            eyelid = progress;
            if (equip && maskView) maskView.AnimateDonning(t / eyesCloseSeconds, true);
            yield return null;
        }
        eyelid = 1;
        SetMaskState(equip); yield return new WaitForSecondsRealtime(eyesHoldSeconds);
        for (float t = 0; t < eyesOpenSeconds; t += Time.unscaledDeltaTime)
        {
            float progress = Mathf.Clamp01(t / eyesOpenSeconds);
            eyelid = 1 - Mathf.SmoothStep(0, 1, progress);
            if (!equip && maskView) maskView.AnimateDonning(progress, false);
            yield return null;
        }
        eyelid = 0; if (maskView) maskView.ApplyState(maskOn); transitioning = false;
    }
    IEnumerator Lids(float from, float to, float duration)
    {
        for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
        { eyelid = Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, t / duration)); yield return null; }
        eyelid = to;
    }
    static string AxisName(Vector3 up)
    {
        if (Mathf.Abs(up.y) > .999f) return up.y > 0 ? "Y" : "-Y";
        if (Mathf.Abs(up.z) > .999f) return up.z > 0 ? "Z" : "-Z";
        if (Mathf.Abs(up.x) > .999f) return up.x > 0 ? "X" : "-X";
        return "ПОВОРОТ";
    }
    void LockCursor(bool locked) { cursorLocked = locked; Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !locked; }
    static void SetLayer(Transform node, int layer) { node.gameObject.layer = layer; foreach (Transform child in node) SetLayer(child, layer); }
    void OnDisable() { if (Application.isPlaying) LockCursor(false); }
    void OnGUI()
    {
        Color old = GUI.color;
        GUI.color = new Color(.018f, .03f, .036f, .88f);
        GUI.DrawTexture(new Rect(22, 20, 440, 106), Texture2D.whiteTexture);
        GUI.color = maskOn ? new Color(.76f, .72f, 1) : Color.white;
        GUI.Label(new Rect(38, 30, 420, 24), maskOn ? "МАСКА НАДЕТА · ПОВОРОТЫ ДОСТУПНЫ" : "МАСКА СНЯТА · ГРАВИТАЦИЯ СОХРАНЕНА");
        GUI.Label(new Rect(38, 56, 420, 24), "WASD — идти  ·  Мышь — обзор  ·  M — маска");
        GUI.Label(new Rect(38, 82, 420, 24), Time.unscaledTime < noticeUntil ? notice : blocked ? "НАДЕНЬ МАСКУ ДЛЯ СМЕНЫ ГРАВИТАЦИИ" : complete ? "ВЫХОД ДОСТИГНУТ · можно вернуться назад" : $"ВЕРТИКАЛЬ: {currentAxis}   /   {Mathf.RoundToInt(routeDistance)} м");
        if (eyelid > 0)
        {
            GUI.color = Color.black; float h = Screen.height * .5f * eyelid + 1;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, h), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, Screen.height - h, Screen.width, h), Texture2D.whiteTexture);
        }
        GUI.color = old;
    }
}
