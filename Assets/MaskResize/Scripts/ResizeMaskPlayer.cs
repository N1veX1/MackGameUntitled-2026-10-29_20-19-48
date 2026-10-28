using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MaskResize
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ResizeMaskPlayer : MonoBehaviour
    {
        [SerializeField] Camera playerCamera;
        [SerializeField] GameObject wornMask;
        [Header("Movement")]
        [SerializeField, Min(.1f)] float speed = 4.2f;
        [SerializeField, Min(.01f)] float sensitivity = .12f;
        [SerializeField, Min(.01f)] float lookSmoothing = .045f;
        [SerializeField, Min(1)] float acceleration = 28;
        [SerializeField] float fallHeight = -8;
        [Header("Resize controls")]
        [Tooltip("Maximum distance to a visible resize target, in metres. Solid obstacles block selection.")]
        [SerializeField, Min(1)] float reach = 20;
        [SerializeField, Min(.01f)] float sizeStep = .25f;
        [SerializeField, Min(.01f)] float preciseStep = .05f;
        [Header("Эффект растягивания обзора")]
        [SerializeField, Range(0, 15)] float resizeViewExpansion = 6;
        [SerializeField, Min(.01f)] float resizeViewAttack = .12f;
        [SerializeField, Min(.01f)] float resizeViewReturn = .35f;
        float resizeViewBase, resizeViewOffset, resizeViewVelocity;
        bool resizeViewActive;
        CharacterController controller;
        Vector3 velocity, checkpoint;
        Quaternion checkpointRotation;
        float yaw, pitch, smoothYaw, smoothPitch, verticalSpeed;
        bool captured, finished;
        string message;
        float messageUntil;
        ResizeObjectMarker targetMarker;
        ResizableMaskObject observedTarget;
        Texture2D edges;
        GUIStyle small, title, centered;
        readonly RaycastHit[] aimHits = new RaycastHit[128];
        readonly List<ResizableMaskObject> resizingObjects = new List<ResizableMaskObject>();
        static readonly string[] DimensionLabels = { "1 · ДЛИНА", "2 · ВЫСОТА", "3 · ШИРИНА" };
        ResizableMaskObject captionTarget;
        Vector3 targetHit;
        int targetSide = 1;
        ResizeDimension? pendingDimension;
        readonly Dictionary<ResizableMaskObject,int> activeResizeSides = new Dictionary<ResizableMaskObject,int>();
        public int TargetSide => targetSide;
        static bool HalfHeld => Keyboard.current != null && Keyboard.current.eKey.isPressed;
        Vector3Int captionDimensions;
        string dimensionsCaption;
        public bool MaskOn { get; private set; }
        public ResizeDimension Dimension { get; private set; }
        public ResizableMaskObject Target { get; private set; }
        public Camera PlayerCamera => playerCamera;
        public CharacterController Controller => controller;
        public bool IsAxisDimmedInHud(ResizeDimension dimension)
            => Target && Target.ShowLockedAxesInHud && !Target.CanResize(dimension);

        static readonly List<ResizeMaskPlayer> activePlayers = new List<ResizeMaskPlayer>();
        void OnEnable() { if(!activePlayers.Contains(this)) activePlayers.Add(this); }
        internal static bool IsStandingOn(ResizableMaskObject platform)
        {
            // Also protects platforms the player has stepped onto without ever aiming at them.
            foreach(var player in activePlayers)
                if(player && platform.HasStandingPlayer(player.controller)) return true;
            return false;
        }

        public void Configure(Camera camera, GameObject mask)
        {
            ResetResizeView();
            playerCamera = camera; wornMask = mask;
            ConfigureVisual();
        }
        void ConfigureVisual()
        {
            if (!playerCamera || !wornMask) return;
            wornMask.transform.SetParent(playerCamera.transform, false);
            wornMask.transform.localPosition = new Vector3(0, -.025f, .09f);
            wornMask.transform.localRotation = Quaternion.identity;
            wornMask.transform.localScale = Vector3.one;
            foreach (Transform part in wornMask.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 2;
            playerCamera.cullingMask &= ~(1 << 2);
            wornMask.SetActive(!Application.isPlaying || MaskOn);
        }
        void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.height = 1.8f; controller.radius = .28f; controller.center = Vector3.up * .9f;
            controller.skinWidth = .02f; controller.stepOffset = .24f; controller.minMoveDistance = 0;
            if (!playerCamera) playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera) playerCamera.transform.localPosition = Vector3.up * 1.7f;
            checkpoint = transform.position; checkpointRotation = transform.rotation;
            yaw = smoothYaw = transform.eulerAngles.y;
            ConfigureVisual(); Capture(true);
        }
        void Update()
        {
            if (transform.position.y < fallHeight) Respawn();
            AdvanceResizing(Time.deltaTime);
            var keys = Keyboard.current; var mouse = Mouse.current;
            if (keys == null) return;
            if (keys.escapeKey.wasPressedThisFrame) Capture(false);
            if (!captured && mouse != null && mouse.leftButton.wasPressedThisFrame) { Capture(true); return; }
            if (captured)
            {
                if (keys.mKey.wasPressedThisFrame) SetMaskState(!MaskOn);
                if (keys.digit1Key.wasPressedThisFrame) SetDimension(ResizeDimension.Length);
                if (keys.digit2Key.wasPressedThisFrame) SetDimension(ResizeDimension.Height);
                if (keys.digit3Key.wasPressedThisFrame) SetDimension(ResizeDimension.Width);
                if (mouse != null)
                {
                    Vector2 delta = mouse.delta.ReadValue() * sensitivity;
                    yaw += delta.x; pitch = Mathf.Clamp(pitch - delta.y, -85, 85);
                }
            }
            float follow = 1 - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(.001f, lookSmoothing));
            smoothYaw = Mathf.LerpAngle(smoothYaw, yaw, follow); smoothPitch = Mathf.Lerp(smoothPitch, pitch, follow);
            transform.rotation = Quaternion.Euler(0, smoothYaw, 0);
            if (playerCamera) playerCamera.transform.localRotation = Quaternion.Euler(smoothPitch, 0, 0);
            Vector2 input = captured ? new Vector2((keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0),
                (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0)) : Vector2.zero;
            input = Vector2.ClampMagnitude(input, 1);
            Vector3 desired = (transform.right * input.x + transform.forward * input.y) * speed;
            velocity = Vector3.MoveTowards(velocity, desired, acceleration * Time.deltaTime);
            if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            verticalSpeed -= 22 * Time.deltaTime;
            controller.Move((velocity + Vector3.up * verticalSpeed) * Time.deltaTime);
            RefreshTarget();
            if (captured && mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > .01f)
                {
                    float step = keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed ? preciseStep : sizeStep;
                    if (!ResizeTarget(Mathf.Sign(scroll) * step, out string reason)) ShowMessage(reason);
                }
            }
        }
        public void SetMaskState(bool enabled)
        {
            MaskOn = enabled;
            if (wornMask) wornMask.SetActive(enabled);
            RefreshTarget();
        }
        public void SetDimension(ResizeDimension value)
        {
            if(HasActiveResize()) { pendingDimension=value==Dimension?(ResizeDimension?)null:value;return; }
            pendingDimension=null;
            Dimension = value;
            if (targetMarker) targetMarker.SetSelection(MaskOn, Dimension);
        }
        bool HasActiveResize()
        {
            foreach(var item in resizingObjects) if(item && item.isActiveAndEnabled && item.IsResizing) return true;
            return false;
        }
        public void RefreshTarget()
        {
            ResizableMaskObject next = null,lookedAt=null;
            if (playerCamera)
            {
                // A freshly moved CharacterController can intercept a downward ray from its own
                // camera. Skip only our own hierarchy, then use the nearest real obstruction.
                int count = Physics.RaycastNonAlloc(playerCamera.transform.position, playerCamera.transform.forward,
                    aimHits, Mathf.Max(reach,playerCamera.farClipPlane), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                float nearest = float.PositiveInfinity;
                if (count < aimHits.Length)
                    for (int i = 0; i < count; i++)
                    {
                        var hit = aimHits[i];
                        if (hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
                        nearest = hit.distance; targetHit=hit.point;
                        lookedAt = hit.collider.GetComponentInParent<ResizableMaskObject>();
                        next=hit.distance<=reach?lookedAt:null;
                    }
            }
            if (next != Target)
            {
                if (targetMarker) targetMarker.SetSelection(false, Dimension);
                if(Target) Target.ShowResizeGuide(0,false);
                Target = next; targetMarker = Target ? Target.GetComponent<ResizeObjectMarker>() : null;
            }
            ResizableMaskObject heldByGaze=MaskOn?lookedAt:null;
            if(observedTarget!=heldByGaze)
            {
                if(observedTarget) observedTarget.SetObserved(false);
                observedTarget=heldByGaze;
            }
            // Only the worn mask holds a shape by gaze. Removing it releases that hold
            // immediately, while an already requested smooth resize is allowed to finish.
            // Distant visible objects can be held, but editing still requires being in reach.
            if(observedTarget) observedTarget.SetObserved(true,controller);
            if(Target)
            {
                int guideAxis=Dimension==ResizeDimension.Length?2:Dimension==ResizeDimension.Height?1:0;
                float offset=Target.transform.InverseTransformPoint(targetHit)[guideAxis]-Target.HalfSplitCoordinate(guideAxis);
                if(!Target.IsResizing && Mathf.Abs(offset)>.015f)
                {
                    int aimedSide=offset<0?-1:1;
                    targetSide=aimedSide;
                }
                int guideSide=Target.IsResizing && activeResizeSides.TryGetValue(Target,out int activeSide)?activeSide:HalfHeld?targetSide:0;
                if(guideSide!=0) targetSide=guideSide;
                Target.ShowResizeGuide(guideAxis,MaskOn && Target.CanResize(Dimension),guideSide);
            }
            if (targetMarker)
            {
                targetMarker.SetSelection(MaskOn, Dimension);
            }
        }
        public bool ResizeTarget(float delta, out string reason)
            => ResizeTarget(delta, HalfHeld, out reason);
        public bool ResizeTarget(float delta, bool chooseSide, out string reason)
        {
            RefreshTarget();
            if (!MaskOn) { reason = "Сначала надень маску: M"; return false; }
            if (!Target) { reason = string.Empty; return false; }
            if(!Target.CanResize(Dimension))
            { reason=Target.ShowLockedAxesInHud?"Этот размер закреплён. Смотри стрелки на объекте.":string.Empty;return false; }
            // A new axis/half must not cancel an unfinished smooth movement. Wheel input
            // on the current operation still accumulates normally.
            if(pendingDimension.HasValue){reason=string.Empty;return false;}
            int resizeSide=chooseSide?targetSide:0;
            if(Target.IsResizing && activeResizeSides.TryGetValue(Target,out int previousSide) && previousSide!=resizeSide)
            {reason=string.Empty;return false;}
            Target.SetResizeSide(Dimension, resizeSide);
            if (!Target.RequestResize(Dimension, delta, controller, out reason)) return false;
            activeResizeSides[Target]=resizeSide;
            if (!resizingObjects.Contains(Target)) resizingObjects.Add(Target);
            return true;
        }
        public void AdvanceResizing(float deltaTime)
        {
            // Carrying a rider rebuilds its capsule. Preserve its support state, then let
            // the ordinary Move below re-establish grounded contact in this same frame.
            bool supported = controller && controller.isGrounded;
            float growth = 0;
            for (int i = resizingObjects.Count - 1; i >= 0; i--)
            {
                var item = resizingObjects[i];
                if (!item || !item.isActiveAndEnabled) { if(!ReferenceEquals(item,null))activeResizeSides.Remove(item);resizingObjects.RemoveAt(i); continue; }
                Vector3 before = item.Size;
                if (!item.AdvanceResize(deltaTime, out string reason) && !string.IsNullOrEmpty(reason)) ShowMessage(reason);
                Vector3 change = item.Size - before;
                growth = Mathf.Max(growth, Mathf.Max(change.x, Mathf.Max(change.y, change.z)));
                if (!item.IsResizing) { activeResizeSides.Remove(item);resizingObjects.RemoveAt(i); }
            }
            if(pendingDimension.HasValue && !HasActiveResize()) SetDimension(pendingDimension.Value);
            if (supported && verticalSpeed < 0) verticalSpeed = -2;
            AdvanceResizeView(deltaTime, growth);
        }
        void AdvanceResizeView(float deltaTime, float growth)
        {
            if (!playerCamera || deltaTime <= 0) return;
            // Follow actual growth, so blocked requests and shrinking never produce a false pulse.
            float dt = Mathf.Min(deltaTime, .05f);
            float target = MaskOn ? resizeViewExpansion * Mathf.Clamp01(growth / dt) : 0;
            if (!resizeViewActive)
            {
                if (target <= 0) return;
                resizeViewBase = playerCamera.fieldOfView;
                resizeViewActive = true;
            }
            target = Mathf.Min(target, Mathf.Max(0, 120 - resizeViewBase));
            resizeViewOffset = Mathf.SmoothDamp(resizeViewOffset, target, ref resizeViewVelocity,
                target > resizeViewOffset ? resizeViewAttack : resizeViewReturn, Mathf.Infinity, dt);
            playerCamera.fieldOfView = resizeViewBase + resizeViewOffset;
            if (target == 0 && resizeViewOffset < .001f) ResetResizeView();
        }
        void ResetResizeView()
        {
            if (resizeViewActive && playerCamera) playerCamera.fieldOfView = resizeViewBase;
            resizeViewActive = false;
            resizeViewOffset = resizeViewVelocity = 0;
        }
        void StopResizing()
        {
            foreach (var item in resizingObjects) if (item) item.CancelResize();
            resizingObjects.Clear();
            activeResizeSides.Clear();pendingDimension=null;
        }
        public void SetCheckpoint(Vector3 feet, Quaternion rotation)
        {
            checkpoint = feet; checkpointRotation = rotation;
        }
        public void Teleport(Vector3 feet, Quaternion rotation)
        {
            StopResizing();
            ResetResizeView();
            if (!controller) controller = GetComponent<CharacterController>();
            controller.enabled = false; transform.SetPositionAndRotation(feet, rotation); controller.enabled = true;
            velocity = Vector3.zero; verticalSpeed = 0; pitch = smoothPitch = 0; yaw = smoothYaw = rotation.eulerAngles.y;
            if (playerCamera) playerCamera.transform.localRotation = Quaternion.identity;
            Physics.SyncTransforms(); RefreshTarget();
        }
        public void Respawn()
        {
            Teleport(checkpoint, checkpointRotation);
            ShowMessage("Возвращение на площадку. Без внимания размеры восстанавливаются через 5 секунд.");
        }
        public void CompleteTutorial() { finished = true; }
        void ShowMessage(string value) { if(string.IsNullOrEmpty(value))return;message = value; messageUntil = Time.unscaledTime + 2.5f; }
        void Capture(bool value) { captured = value; Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !value; }
        void OnDisable() { activePlayers.Remove(this); if(observedTarget) observedTarget.SetObserved(false); if(Target) Target.ShowResizeGuide(0,false); StopResizing(); ResetResizeView(); if (targetMarker) targetMarker.SetSelection(false, Dimension); Capture(false); }
        void OnDestroy() { if (edges) Destroy(edges); }
        void MakeEdges()
        {
            edges = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var colors = new Color[64 * 64];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float rim = Mathf.Max(Mathf.Abs(x / 63f * 2 - 1), Mathf.Abs(y / 63f * 2 - 1));
                colors[y * 64 + x] = new Color(.12f, .7f, .8f, Mathf.SmoothStep(0, .13f, Mathf.InverseLerp(.78f, 1, rim)));
            }
            edges.SetPixels(colors); edges.Apply(false, true);
        }
        void OnGUI()
        {
            // This overlay has no clickable controls; Layout/input events need no drawing work.
            if (Event.current.type != EventType.Repaint) return;
            if (small == null)
            {
                small = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
                title = new GUIStyle(small) { fontSize = 22, fontStyle = FontStyle.Bold };
                centered = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
            }
            Color color = GUI.color; Matrix4x4 matrix = GUI.matrix;
            if (MaskOn) { if (!edges) MakeEdges(); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), edges); }
            float scale = Mathf.Clamp(Screen.height / 900f, .65f, 1.6f);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            float w = Screen.width / scale, h = Screen.height / scale;
            GUI.color = new Color(.035f, .055f, .065f, .86f);
            GUI.DrawTexture(new Rect(24, 24, 380, 78), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(40, 32, 345, 30), "04 / МАСШТАБ", title);
            GUI.Label(new Rect(40, 66, 345, 30), MaskOn ? $"МАСКА НАДЕТА · M — снять · до {reach:0} м" : "M — надеть маску", small);
            GUI.color = MaskOn && Target ? new Color(.3f, 1, .9f) : Color.white;
            GUI.DrawTexture(new Rect(w / 2 - 2, h / 2 - 2, 4, 4), Texture2D.whiteTexture);
            GUI.color = new Color(.035f, .055f, .065f, .9f);
            GUI.DrawTexture(new Rect(w / 2 - 320, h - 142, 640, 120), Texture2D.whiteTexture);
            for (int i = 0; i < 3; i++)
            {
                bool selected=(int)Dimension==i;
                float glow=.88f+.12f*Mathf.Sin(Time.unscaledTime*2.2f);
                GUI.color = IsAxisDimmedInHud((ResizeDimension)i) ? new Color(.3f,.32f,.33f) : selected ? new Color(.35f*glow,glow,.88f*glow) : new Color(.65f, .72f, .75f);
                GUI.Label(new Rect(w / 2 - 305 + i * 205, h - 133, 200, 28), DimensionLabels[i], centered);
            }
            GUI.color = Color.white;
            GUI.Label(new Rect(w / 2 - 310, h - 99, 620, 27), GetDimensionsCaption(), centered);
            GUI.Label(new Rect(w / 2 - 310, h - 67, 620, 32), "Колесо — размер · Зажми E — менять половину · Shift — точнее", centered);
            if(MaskOn && Target)
            {
                int axis=Dimension==ResizeDimension.Length?2:Dimension==ResizeDimension.Height?1:0;
                string label=Dimension==ResizeDimension.Length?"длина":Dimension==ResizeDimension.Height?"высота":"ширина";
                GUI.Label(new Rect(w/2-320,h-235,640,30),$"Максимальная {label}: {Target.MaxSize[axis]:F2} м",centered);
            }
            if (Time.unscaledTime < messageUntil) GUI.Label(new Rect(w / 2 - 370, h - 200, 740, 50), message, centered);
            if (finished) GUI.Label(new Rect(w / 2 - 360, 130, 720, 65), "Обучение пройдено. Можно вернуться и менять размеры дальше.", centered);
            if (!captured) GUI.Label(new Rect(w / 2 - 260, h / 2 + 35, 520, 50), "Нажми мышью, чтобы продолжить", centered);
            GUI.matrix = matrix; GUI.color = color;
        }
        string GetDimensionsCaption()
        {
            if (!Target) { captionTarget = null; return string.Empty; }
            Vector3 size = Target.Size;
            var displayed = new Vector3Int(Mathf.RoundToInt(size.x * 100), Mathf.RoundToInt(size.y * 100), Mathf.RoundToInt(size.z * 100));
            if (captionTarget != Target || captionDimensions != displayed || dimensionsCaption == null)
            {
                captionTarget = Target; captionDimensions = displayed;
                dimensionsCaption = $"Д {size.z:F2} м   ·   В {size.y:F2} м   ·   Ш {size.x:F2} м";
            }
            return dimensionsCaption;
        }
    }
}
