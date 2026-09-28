using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// One mask, six orthographic views. Assign the blocking wall's renderers and
/// colliders in the Inspector. UI buttons may call SelectX/SelectMinusX/etc.
/// </summary>
public sealed class MaskAxisCamera : MonoBehaviour
{
    public enum ViewDirection { X, MinusX, Y, MinusY, Z, MinusZ }

    [Header("Scene")]
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Transform focusTarget;
    [SerializeField] private Vector3 focusOffset = Vector3.zero;
    [SerializeField, Min(0.1f)] private float cameraDistance = 15f;
    [SerializeField, Min(0.1f)] private float orthographicSize = 8f;
    [Tooltip("Only scripts that move/rotate this Camera, not player movement scripts.")]
    [SerializeField] private MonoBehaviour[] firstPersonCameraScripts;

    [Header("Obstacle removed only in X / -X")]
    [SerializeField] private Renderer[] wallRenderers;
    [SerializeField] private Collider[] wallColliders;
    [Tooltip("The imported MaskXYZ prefab attached to the character's head.")]
    [SerializeField] private GameObject maskVisual;
    [Tooltip("Legacy reference; ceiling remains visible.")]
    [SerializeField] private Renderer[] topCutawayRenderers;
    [Tooltip("Legacy reference; side wall remains visible.")]
    [SerializeField] private Renderer[] oppositeSideCutawayRenderers;

    [Header("Optional UI: 3 axis images (X, Y, Z) or 6 direction images")]
    [SerializeField] private Image[] directionIndicators;
    [SerializeField] private Color activeColor = new Color(0.62f, 0.35f, 1f);
    [SerializeField] private Color inactiveColor = new Color(0.19f, 0.19f, 0.23f);

    [Header("Optional keyboard")]
    [SerializeField] private bool keyboardEnabled = true;
    [SerializeField] private Key toggleMaskKey = Key.M;
    [SerializeField] private Key xAxisKey = Key.Digit1;
    [SerializeField] private Key yAxisKey = Key.Digit2;
    [SerializeField] private Key zAxisKey = Key.Digit3;

    public bool MaskEnabled { get; private set; }
    public int UnlockedAxis { get; private set; } = 2;
    public Renderer[] ObstacleRenderers => wallRenderers;
    public Collider[] ObstacleColliders => wallColliders;
    public void UnlockThrough(int axis)
    {
        UnlockedAxis = Mathf.Clamp(axis, 0, 2);
        if ((int)CurrentDirection / 2 > UnlockedAxis)
        {
            CurrentDirection = ViewDirection.X;
            if (MaskEnabled) PositionCamera();
            ApplyObstacle();
        }
    }
    public ViewDirection CurrentDirection { get; private set; } = ViewDirection.Z;

    [SerializeField] private Transform playerTarget;
    private MaskProximityWall proximityWall;
    private MaskViewEffects effects;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private bool originalOrthographic;
    private float originalOrthoSize;
    private bool[] wallRendererStates;
    private bool[] wallColliderStates;
    private bool[] cameraScriptStates;

    private void Awake()
    {
        if (viewCamera == null) viewCamera = Camera.main;
        if (viewCamera == null)
        {
            Debug.LogError("MaskAxisCamera: assign a Camera.", this);
            enabled = false;
            return;
        }
        if (xAxisKey == Key.None) xAxisKey = Key.Digit1;
        if (yAxisKey == Key.None) yAxisKey = Key.Digit2;
        if (zAxisKey == Key.None) zAxisKey = Key.Digit3;

        if (effects == null) { effects = GetComponent<MaskViewEffects>(); if (!effects) effects = gameObject.AddComponent<MaskViewEffects>(); effects.Initialize(viewCamera); }
        originalLocalPosition = viewCamera.transform.localPosition;
        originalLocalRotation = viewCamera.transform.localRotation;
        originalOrthographic = viewCamera.orthographic;
        originalOrthoSize = viewCamera.orthographicSize;

        cameraScriptStates = new bool[firstPersonCameraScripts == null ? 0 : firstPersonCameraScripts.Length];

        wallRendererStates = new bool[wallRenderers == null ? 0 : wallRenderers.Length];
        wallColliderStates = new bool[wallColliders == null ? 0 : wallColliders.Length];
        for (int i = 0; i < wallRendererStates.Length; i++)
            if (wallRenderers[i]) wallRendererStates[i] = wallRenderers[i].enabled;
        for (int i = 0; i < wallColliderStates.Length; i++)
            if (wallColliders[i]) wallColliderStates[i] = wallColliders[i].enabled;
        if (!playerTarget) { var body = viewCamera.GetComponentInParent<CharacterController>(); playerTarget = body ? body.transform : (maskVisual ? maskVisual.transform.parent : focusTarget); }
        proximityWall = GetComponent<MaskProximityWall>();
        if (!proximityWall) proximityWall = gameObject.AddComponent<MaskProximityWall>();
        proximityWall.Initialize(wallRenderers, wallColliders, playerTarget);
        if (maskVisual != null) maskVisual.SetActive(false);
        ApplyObstacle();
        UpdateIndicators();
    }

    private void Update()
    {
        if (keyboardEnabled)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard[toggleMaskKey].wasPressedThisFrame) ToggleMask();
            if (keyboard[xAxisKey].wasPressedThisFrame) SelectX();
            if (keyboard[yAxisKey].wasPressedThisFrame) SelectY();
            if (keyboard[zAxisKey].wasPressedThisFrame) SelectZ();
        }
    }

    private void LateUpdate()
    {
        // Follow after player movement has updated this frame.
        if (MaskEnabled) PositionCamera();
    }

    public void ToggleMask() => SetMaskEnabled(!MaskEnabled);

    public void SetMaskEnabled(bool on)
    {
        if (Application.isPlaying && isActiveAndEnabled)
        {
            if (!effects.Busy && MaskEnabled != on) effects.Blink(() => ApplyMaskEnabled(on));
            return;
        }
        ApplyMaskEnabled(on);
    }

    private void ApplyMaskEnabled(bool on)
    {
        if (MaskEnabled == on) return;
        if (on)
        {
            // Restore the current first-person view, not the position at startup.
            originalLocalPosition = viewCamera.transform.localPosition;
            originalLocalRotation = viewCamera.transform.localRotation;
            originalOrthographic = viewCamera.orthographic;
            originalOrthoSize = viewCamera.orthographicSize;
            for (int i = 0; i < cameraScriptStates.Length; i++)
            {
                if (firstPersonCameraScripts[i] == null) continue;
                cameraScriptStates[i] = firstPersonCameraScripts[i].enabled;
                firstPersonCameraScripts[i].enabled = false;
            }
        }
        MaskEnabled = on;

        // Only the explicitly assigned obstacle disappears in the X dimension.
        if (maskVisual != null) maskVisual.SetActive(on);

        if (on)
        {
            viewCamera.orthographic = true;
            viewCamera.orthographicSize = orthographicSize;
            PositionCamera();
        }
        else
        {
            viewCamera.orthographic = originalOrthographic;
            viewCamera.orthographicSize = originalOrthoSize;
            // Local pose follows the player's parent transform during mask use.
            viewCamera.transform.localPosition = originalLocalPosition;
            viewCamera.transform.localRotation = originalLocalRotation;
            effects.LeaveView();
            for (int i = 0; i < cameraScriptStates.Length; i++)
                if (firstPersonCameraScripts[i] != null)
                    firstPersonCameraScripts[i].enabled = cameraScriptStates[i];
        }
        ApplyObstacle();
        UpdateIndicators();
    }

    public void ResetAfterRespawn()
    {
        if (effects) effects.CancelTransition();
        if (MaskEnabled) ApplyMaskEnabled(false);
        if (proximityWall) proximityWall.Restore();
    }

    private void OnDisable()
    {
        if (effects) effects.CancelTransition();
        if (proximityWall) proximityWall.Restore();
        if (MaskEnabled) ApplyMaskEnabled(false);
    }

    public void SelectX() => Select(ViewDirection.X);
    public void SelectMinusX() => Select(ViewDirection.MinusX);
    public void SelectY() => Select(ViewDirection.Y);
    public void SelectMinusY() => Select(ViewDirection.MinusY);
    public void SelectZ() => Select(ViewDirection.Z);
    public void SelectMinusZ() => Select(ViewDirection.MinusZ);

    public void Select(ViewDirection pressed)
    {
        if (!MaskEnabled || (int)pressed / 2 > UnlockedAxis || (effects != null && effects.Busy)) return;
        // Pressing the currently active button flips the same axis.
        CurrentDirection = CurrentDirection == pressed
            ? Opposite(pressed)
            : pressed;
        PositionCamera();
        ApplyObstacle();
        UpdateIndicators();
    }

    private static ViewDirection Opposite(ViewDirection direction)
    {
        switch (direction)
        {
            case ViewDirection.X: return ViewDirection.MinusX;
            case ViewDirection.MinusX: return ViewDirection.X;
            case ViewDirection.Y: return ViewDirection.MinusY;
            case ViewDirection.MinusY: return ViewDirection.Y;
            case ViewDirection.Z: return ViewDirection.MinusZ;
            default: return ViewDirection.Z;
        }
    }

    private void PositionCamera()
    {
        Vector3 target = (playerTarget != null ? playerTarget.position : (focusTarget ? focusTarget.position : Vector3.zero)) + focusOffset;
        Vector3 side;
        switch (CurrentDirection)
        {
            case ViewDirection.X: side = Vector3.right; break;
            case ViewDirection.MinusX: side = Vector3.left; break;
            case ViewDirection.Y: side = Vector3.up; break;
            case ViewDirection.MinusY: side = Vector3.down; break;
            case ViewDirection.Z: side = Vector3.forward; break;
            default: side = Vector3.back; break;
        }

        // Y is above looking down; -Y is below looking up.
        // X/-X are opposing side elevations, Z/-Z opposing front elevations.
        Vector3 cameraUp = CurrentDirection == ViewDirection.Y ? Vector3.forward
            : CurrentDirection == ViewDirection.MinusY ? Vector3.back
            : Vector3.up;
        effects.SetView(target, side, cameraUp, cameraDistance);
        viewCamera.orthographic = true;
        viewCamera.orthographicSize = orthographicSize;
    }

    private void ApplyObstacle()
    {
        if (proximityWall) proximityWall.SetDimension(MaskEnabled && (CurrentDirection == ViewDirection.X || CurrentDirection == ViewDirection.MinusX));
    }

    private void UpdateIndicators()
    {
        if (directionIndicators == null) return;
        int activeIndex = directionIndicators.Length >= 6
            ? (int)CurrentDirection : (int)CurrentDirection / 2;
        for (int i = 0; i < directionIndicators.Length; i++)
            if (directionIndicators[i] != null)
                directionIndicators[i].color = MaskEnabled && i == activeIndex
                    ? activeColor : inactiveColor;
    }

}


