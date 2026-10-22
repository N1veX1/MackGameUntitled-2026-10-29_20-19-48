using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class MaskInversionPlayer : MonoBehaviour
{
    [SerializeField] Camera playerCamera;
    [SerializeField] GameObject wornMaskVisual;
    [SerializeField, Min(.1f)] float moveSpeed = 4.2f;
    [SerializeField, Min(.01f)] float mouseSensitivity = .12f;
    [SerializeField, Min(.01f)] float lookSmoothSeconds = .045f;
    [SerializeField, Min(1)] float acceleration = 28f;
    float targetYaw, smoothYaw, smoothPitch;
    Vector3 horizontalVelocity;
    [SerializeField] float eyeHeight = 1.7f;
    [SerializeField] float gravity = -22f;
    [SerializeField] float fallRespawnY = -2f;
    [Header("Head attachment")]
    [SerializeField] Vector3 wornMaskLocalPosition = new Vector3(0f, -.016f, .055f);
    [Header("Respawn fade only")]
    [SerializeField, HideInInspector] float closeSeconds = .42f;
    [SerializeField, Min(.05f)] float closedSeconds = .12f;
    [SerializeField, Min(.05f)] float openSeconds = .3f;
    [Header("Mask screen effect")]
    [SerializeField, Range(0f, 1f)] float edgeEffectStrength = .55f;
    CharacterController controller;
    Vector3 checkpoint;
    Quaternion checkpointRotation;
    bool checkpointMaskOn;
    Transform headVisualPivot;
    GameObject headVisual;
    float pitch, verticalSpeed, eyelid, maskPulse;
    bool cursorCaptured;
    int transitionVersion;
    Texture2D edgeTexture;

    public bool MaskOn { get; private set; }
    public bool Transitioning { get; private set; }
    public bool IsRespawning { get; private set; }
    public event Action<bool> MaskStateChanged;
    public event Action RespawnStarted;
    public Transform HeadVisualPivot => headVisualPivot;
    public CharacterController Controller => controller;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = .28f;
        controller.center = Vector3.zero;
        controller.skinWidth = .02f;
        controller.stepOffset = .2f;
        controller.minMoveDistance = 0f;
        if (!playerCamera) playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera)
        {
            playerCamera.transform.localPosition = new Vector3(0f, eyeHeight - .9f, 0f);
            playerCamera.transform.localRotation = Quaternion.identity;
        }
        if (wornMaskVisual) wornMaskVisual.SetActive(false);
        InitializeWornVisuals();

        checkpoint = transform.position;
        checkpointRotation = transform.rotation;
        targetYaw = smoothYaw = transform.eulerAngles.y;
        CaptureCursor(true);
    }

    void Update()
    {
        if (!controller.enabled) return;
        if (transform.position.y < fallRespawnY && !IsRespawning) Respawn();
        maskPulse = Mathf.MoveTowards(maskPulse, 0f, Time.unscaledDeltaTime * 1.4f);
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.escapeKey.wasPressedThisFrame) CaptureCursor(false);
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !cursorCaptured)
            CaptureCursor(true);
        if (cursorCaptured && keyboard.mKey.wasPressedThisFrame) ToggleMask();
        // Freeze only during respawn. Mask changes complete within the same frame.
        if (Transitioning) { horizontalVelocity = Vector3.zero; return; }
        if (cursorCaptured && Mouse.current != null)
        {
            Vector2 look = Mouse.current.delta.ReadValue() * mouseSensitivity;
            targetYaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, -82f, 82f);
        }
        float follow = 1 - Mathf.Exp(-Time.unscaledDeltaTime / lookSmoothSeconds);
        smoothYaw = Mathf.LerpAngle(smoothYaw, targetYaw, follow); smoothPitch = Mathf.Lerp(smoothPitch, pitch, follow);
        transform.rotation = Quaternion.Euler(0, smoothYaw, 0);
        if (playerCamera) playerCamera.transform.localRotation = Quaternion.Euler(smoothPitch, 0, 0);
        Vector3 input = cursorCaptured ? new Vector3(
            (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f), 0f,
            (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f)) : Vector3.zero;
        Vector3 horizontal = (transform.right * input.x + transform.forward * input.z).normalized * moveSpeed;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, horizontal, acceleration * Time.deltaTime);
        if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
        verticalSpeed += gravity * Time.deltaTime;
        controller.Move((horizontalVelocity + Vector3.up * verticalSpeed) * Time.deltaTime);
    }

    public void ToggleMask()
    {
        if (Transitioning || !isActiveAndEnabled) return;
        int version = ++transitionVersion;
        Transitioning = true; // Guard reentrant state events, not an animation delay.
        MaskOn = !MaskOn;
        UpdateWornVisuals();
        MaskStateChanged?.Invoke(MaskOn);
        // A returning wall may start a respawn synchronously during the event.
        if (version != transitionVersion) return;
        maskPulse = 0;
        Transitioning = false;
    }
    IEnumerator AnimateLids(float from, float to, float duration)
    {
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float progress = Mathf.SmoothStep(0f, 1f, t / duration);
            eyelid = Mathf.Lerp(from, to, progress);
            yield return null;
        }
        eyelid = to;
    }

    void InitializeWornVisuals()
    {
        if (!playerCamera || !wornMaskVisual) return;
        headVisualPivot = new GameObject("Head Visual Pivot").transform;
        headVisualPivot.SetParent(transform, false);
        headVisualPivot.localPosition = new Vector3(0f, eyeHeight - .9f, 0f);
        wornMaskVisual.transform.SetParent(headVisualPivot, false);
        wornMaskVisual.transform.localPosition = wornMaskLocalPosition;
        wornMaskVisual.transform.localRotation = Quaternion.identity;
        wornMaskVisual.transform.localScale = Vector3.one;
        headVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        headVisual.name = "Head";
        headVisual.transform.SetParent(headVisualPivot, false);
        headVisual.transform.localPosition = new Vector3(0f, -.018f, -.018f);
        headVisual.transform.localScale = new Vector3(.23f, .28f, .22f);
        Collider headCollider = headVisual.GetComponent<Collider>();
        headCollider.enabled = false;
        Destroy(headCollider);
        var face = wornMaskVisual.GetComponentInChildren<Renderer>(true);
        if (face && face.sharedMaterials.Length > 0)
            headVisual.GetComponent<Renderer>().sharedMaterial = face.sharedMaterials[face.sharedMaterials.Length - 1];
        // The face exists on the head for scene/third-person cameras. The player's
        // own camera cannot see the inside of their head or the mask shell.
        foreach (Transform part in headVisualPivot.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 2;
        playerCamera.cullingMask &= ~(1 << 2);
        UpdateWornVisuals();
    }

    void UpdateWornVisuals()
    {
        if (wornMaskVisual) wornMaskVisual.SetActive(MaskOn);
        if (headVisual) headVisual.SetActive(MaskOn);
    }

    void LateUpdate()
    {
        if (headVisualPivot && playerCamera) headVisualPivot.rotation = playerCamera.transform.rotation;
    }

    public void SetRespawnPoint(Vector3 position, Quaternion rotation, bool maskOn)
    {
        checkpoint = position;
        checkpointRotation = rotation;
        checkpointMaskOn = maskOn;
    }
    public void SetCheckpoint(Vector3 position)
    {
        checkpoint = position;
        checkpointRotation = transform.rotation;
    }

    public void Respawn()
    {
        if (IsRespawning || !isActiveAndEnabled) return;
        ++transitionVersion;
        StopAllCoroutines();

        IsRespawning = true;
        Transitioning = true;
        if (wornMaskVisual) wornMaskVisual.SetActive(false);
        RespawnStarted?.Invoke();
        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        yield return AnimateLids(eyelid, 1f, .12f);
        // Move first, then restore walls: no recursive death at the old position.
        controller.enabled = false;
        transform.SetPositionAndRotation(checkpoint, checkpointRotation);
        pitch = verticalSpeed = maskPulse = 0f;
        smoothPitch = 0; targetYaw = smoothYaw = checkpointRotation.eulerAngles.y; horizontalVelocity = Vector3.zero;
        MaskOn = checkpointMaskOn;
        UpdateWornVisuals();
        if (playerCamera) playerCamera.transform.localRotation = Quaternion.identity;
        controller.enabled = true;
        Physics.SyncTransforms();
        MaskStateChanged?.Invoke(MaskOn);
        yield return new WaitForSecondsRealtime(closedSeconds);
        yield return AnimateLids(1f, 0f, openSeconds);
        IsRespawning = Transitioning = false;
    }

    void CaptureCursor(bool captured)
    {
        cursorCaptured = captured;
        Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !captured;
    }

    void CreateEdgeTexture()
    {
        const int size = 128;
        edgeTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        edgeTexture.name = "Inversion mask peripheral glow";
        edgeTexture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float u = x / (float)(size - 1), v = y / (float)(size - 1);
            float edge = Mathf.Max(Mathf.Abs(u * 2f - 1f), Mathf.Abs(v * 2f - 1f));
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.65f, 1f, edge));
            Color color = Color.Lerp(new Color(.08f, .12f, .18f), new Color(.72f, .79f, .86f), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.35f, .65f, u)));
            color.a = alpha;
            pixels[y * size + x] = color;
        }
        edgeTexture.SetPixels(pixels);
        edgeTexture.Apply(false, true);
    }

    void OnGUI()
    {
        int oldDepth = GUI.depth;
        Color oldColor = GUI.color;
        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.identity;
        GUI.depth = -9999;
        if (MaskOn)
        {
            if (!edgeTexture) CreateEdgeTexture();
            float pulse = .025f * Mathf.Sin(Time.unscaledTime * 1.4f);
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(edgeEffectStrength + pulse + maskPulse * .12f));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), edgeTexture);
        }
        if (eyelid > 0f)
        {
            GUI.depth = -10000;
            GUI.color = Color.black;
            float h = Screen.height * .5f * eyelid + 1f;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, h), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, Screen.height - h, Screen.width, h), Texture2D.whiteTexture);
        }
        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
        GUI.depth = oldDepth;
    }

    void OnDisable()
    {
        StopAllCoroutines();

        eyelid = 0f;
        Transitioning = IsRespawning = false;
        if (wornMaskVisual) wornMaskVisual.SetActive(false);
        CaptureCursor(false);
    }

    void OnDestroy() { if (edgeTexture) Destroy(edgeTexture); }
}

