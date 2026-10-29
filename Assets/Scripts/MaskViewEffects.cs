using System;

using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Upright first-person look and fixed-axis camera transitions.</summary>
[DefaultExecutionOrder(1000)]
public sealed class MaskViewEffects : MonoBehaviour
{
    [HideInInspector] public float closeSeconds = .42f;
    [HideInInspector] public float closedSeconds = .12f;
    [HideInInspector] public float openSeconds = .3f;
    [Min(.05f)] public float viewTransitionSeconds = .65f;
    public float mouseSensitivity = .15f;
    [Min(.01f)] public float lookSmoothSeconds = .045f;
    Vector2 smoothedLook;
    [HideInInspector] public float orbitLimit = 65f; // Retained for existing serialized components.

    public bool Busy { get; private set; }
    // Following a moving player never restarts or postpones an axis transition.
    public bool IsViewSettled => masked && !Busy && !transitioning;
    public Camera ViewCamera => view;
    // Restore every obstacle before the first moving-camera frame, including UI input.
    public event Action ViewTransitionStarted;
    // Kept separately from the projection camera, including when toggling the mask.
    public Quaternion FirstPersonLookRotation => Quaternion.Euler(smoothedLook.x, smoothedLook.y, 0);

    struct RotationSegment
    {
        public Vector2 from, to;
        public float angle;
    }

    readonly RotationSegment[] segments = new RotationSegment[3];
    Camera view;
    bool masked, transitioning;
    float distance, transitionElapsed, transitionDuration, totalAngle;
    int segmentCount;
    Vector3 center;
    Quaternion axisRotation = Quaternion.identity;
    Quaternion currentRotation = Quaternion.identity;
    // x = pitch, y = yaw; roll is always zero throughout a transition.
    Vector2 currentAngles, targetAngles;
    Vector2 lookAngles;
    bool dragging;

    CursorLockMode previousLock;
    bool previousVisible;

    public void Initialize(Camera camera)
    {
        view = camera;
        ResetFirstPersonLook();
    }

    // Retain both entry points for existing scenes and the imported XYZ package.
    public void Blink(Action change) => change?.Invoke();
    public void AnimateMaskChange(GameObject originalMask, bool equip, Action change) => change?.Invoke();
    public void SetView(Vector3 focus, Vector3 side, Vector3 up, float radius)
    {
        if (side.sqrMagnitude < .001f) return;
        Quaternion next = Quaternion.LookRotation(-side.normalized, up);
        center = focus;
        distance = Mathf.Max(.01f, radius);

        if (!masked)
        {
            // Mask changes are immediate.
            masked = true;
            transitioning = false;
            axisRotation = currentRotation = next;
            currentAngles = targetAngles = UprightAngles(next);
            ReleaseCursor();
            ApplyPose();
        }
        else if (Quaternion.Angle(axisRotation, next) > .01f)
        {
            axisRotation = next;
            targetAngles = UprightAngles(next);
            BeginTransition();
        }
    }

    static Vector2 UprightAngles(Quaternion rotation)
    {
        Vector3 forward = rotation * Vector3.forward;
        float pitch = -Mathf.Asin(Mathf.Clamp(forward.y, -1, 1)) * Mathf.Rad2Deg;
        // Both vertical views have canonical yaw zero: +Y has +Z up, -Y has -Z up.
        float yaw = new Vector2(forward.x, forward.z).sqrMagnitude > .000001f
            ? Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg : 0;
        return new Vector2(pitch, yaw);
    }

    void BeginTransition()
    {
        segmentCount = 0;
        totalAngle = 0;
        Vector2 start = currentAngles;
        float yawDelta = Mathf.DeltaAngle(start.y, targetAngles.y);
        float endYaw = start.y + yawDelta;

        if (Mathf.Abs(yawDelta) > .01f)
        {
            // Turn around world Y only while level. Twisting yaw at a vertical view
            // rotates the entire map on the screen, even with an Euler roll of zero.
            AddSegment(start, new Vector2(0, start.y));
            AddSegment(new Vector2(0, start.y), new Vector2(0, endYaw));
            AddSegment(new Vector2(0, endYaw), new Vector2(targetAngles.x, endYaw));
        }
        else AddSegment(start, new Vector2(targetAngles.x, endYaw));

        transitionElapsed = 0;
        transitionDuration = Mathf.Max(.05f, viewTransitionSeconds);
        transitioning = segmentCount > 0;
        if (transitioning) ViewTransitionStarted?.Invoke();
        else FinishTransition();
    }

    void AddSegment(Vector2 from, Vector2 to)
    {
        float angle = Mathf.Abs(to.x - from.x) + Mathf.Abs(to.y - from.y);
        if (angle <= .01f) return;
        segments[segmentCount++] = new RotationSegment { from = from, to = to, angle = angle };
        totalAngle += angle;
    }

    void AdvanceTransition(float deltaTime)
    {
        transitionElapsed += deltaTime;
        if (transitionElapsed >= transitionDuration)
        {
            FinishTransition();
            return;
        }

        float remaining = transitionElapsed;
        for (int i = 0; i < segmentCount; i++)
        {
            RotationSegment segment = segments[i];
            float seconds = transitionDuration * segment.angle / totalAngle;
            if (remaining <= seconds || i == segmentCount - 1)
            {
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(remaining / seconds));
                currentAngles = Vector2.LerpUnclamped(segment.from, segment.to, t);
                currentRotation = Quaternion.Euler(currentAngles.x, currentAngles.y, 0);
                return;
            }
            remaining -= seconds;
        }
    }

    void FinishTransition()
    {
        transitioning = false;
        currentAngles = targetAngles;
        currentRotation = axisRotation;
    }

    public void LeaveView()
    {
        masked = false;
        transitioning = false;
        ResetFirstPersonLook();
    }

    /// <summary>Call after restoring a first-person pose, including respawn.</summary>
    public void ResetFirstPersonLook()
    {
        if (!view) return;
        lookAngles = UprightAngles(view.transform.rotation);
        lookAngles.x = Mathf.Clamp(lookAngles.x, -85, 85);
        smoothedLook = lookAngles;
    }

    void LateUpdate()
    {
        if (!view) return;
        var mouse = Mouse.current;
        bool held = mouse != null && mouse.rightButton.isPressed && !Busy && !masked;
        if (held && !dragging)
        {
            previousLock = Cursor.lockState;
            previousVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            dragging = true;
        }
        if (!held) ReleaseCursor();
        if (held)
        {
            Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;
            lookAngles.x = Mathf.Clamp(lookAngles.x - delta.y, -85, 85);
            lookAngles.y = Mathf.Repeat(lookAngles.y + delta.x + 180, 360) - 180;
        }

        if (masked)
        {
            if (transitioning) AdvanceTransition(Time.unscaledDeltaTime);
            ApplyPose();
        }
        else
        {
            // World yaw/pitch also removes any inherited camera-parent roll.
            float follow = 1 - Mathf.Exp(-Time.unscaledDeltaTime / lookSmoothSeconds);
            if (!Busy) { smoothedLook.x = Mathf.Lerp(smoothedLook.x, lookAngles.x, follow); smoothedLook.y = Mathf.LerpAngle(smoothedLook.y, lookAngles.y, follow); }
            view.transform.rotation = Quaternion.Euler(smoothedLook.x, smoothedLook.y, 0);
        }
    }

    void ApplyPose()
    {
        if (view) view.transform.SetPositionAndRotation(
            center - currentRotation * Vector3.forward * distance, currentRotation);
    }

    void ReleaseCursor()
    {
        if (!dragging) return;
        Cursor.lockState = previousLock;
        Cursor.visible = previousVisible;
        dragging = false;
    }

    public void CancelTransition()
    {
        StopAllCoroutines();


        Busy = false;
        if (masked) FinishTransition();
        ReleaseCursor();
    }

    void OnDisable() => CancelTransition();
}
