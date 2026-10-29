using System.Linq;
using UnityEngine;

namespace MaskXYZ
{
    /// <summary>
    /// Optional camera controller for the six views from the mask concept.
    /// Attach to a scene object and assign camera, focus and blocking walls.
    /// </summary>
    public sealed class MaskAxisCamera : MonoBehaviour
    {
        public enum View { X, NegativeX, Y, NegativeY, Z, NegativeZ }

        [SerializeField] Camera targetCamera;
        [SerializeField] Transform focus;
        [SerializeField] GameObject[] wallsHiddenByMask = new GameObject[0];
        [SerializeField] GameObject maskVisual;
        [SerializeField] GameObject[] visibleWhenEquipped = new GameObject[0];
        [SerializeField, Min(0.1f)] float distance = 5f;
        [SerializeField, Min(0.1f)] float orthographicSize = 3f;
        [SerializeField] View currentView = View.Z;
        [SerializeField] bool maskEquipped;

        [SerializeField] Transform playerTarget;
        MaskProximityWall proximityWall;
        MaskViewEffects effects;
        bool[] initialWallStates;
        Vector3 initialCameraPosition;
        Quaternion initialCameraRotation;
        bool initialOrthographic;
        float initialOrthographicSize;

        public View CurrentView => currentView;
        public bool IsEquipped => maskEquipped;

        void Awake()
        {
            initialWallStates = new bool[wallsHiddenByMask.Length];
            for (int i = 0; i < wallsHiddenByMask.Length; i++)
                initialWallStates[i] = wallsHiddenByMask[i] && wallsHiddenByMask[i].activeSelf;
            if (!targetCamera) targetCamera = Camera.main;
            if (targetCamera)
            {
                effects = GetComponent<MaskViewEffects>(); if (!effects) effects = gameObject.AddComponent<MaskViewEffects>(); effects.Initialize(targetCamera);
                initialCameraPosition = targetCamera.transform.position;
                initialCameraRotation = targetCamera.transform.rotation;
                initialOrthographic = targetCamera.orthographic;
                initialOrthographicSize = targetCamera.orthographicSize;
            }
            if (!playerTarget) playerTarget = maskVisual ? maskVisual.transform.parent : focus;
            proximityWall = GetComponent<MaskProximityWall>();
            if (!proximityWall) proximityWall = gameObject.AddComponent<MaskProximityWall>();
            proximityWall.Initialize(wallsHiddenByMask.Where(g => g).SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).ToArray(),
                wallsHiddenByMask.Where(g => g).SelectMany(g => g.GetComponentsInChildren<Collider>(true)).ToArray(), playerTarget);
            ApplyMask();
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.mKey.wasPressedThisFrame) ToggleMask();
                if (keyboard.digit1Key.wasPressedThisFrame) PressAxis(0, 1);
                if (keyboard.digit2Key.wasPressedThisFrame) PressAxis(1, 1);
                if (keyboard.digit3Key.wasPressedThisFrame) PressAxis(2, 1);
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.M)) ToggleMask();
            if (Input.GetKeyDown(KeyCode.Alpha1)) PressAxis(0, 1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) PressAxis(1, 1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) PressAxis(2, 1);
#endif
        }

        public void EquipMask() { ChangeMask(true); }
        public void RemoveMask() { ChangeMask(false); }
        public void ToggleMask() { ChangeMask(!maskEquipped); }

        void ChangeMask(bool on)
        {
            if (Application.isPlaying && isActiveAndEnabled && effects != null)
            {
                if (!effects.Busy && maskEquipped != on) effects.Blink(() => { maskEquipped = on; ApplyMask(); });
            }
            else { maskEquipped = on; ApplyMask(); }
        }

        // These public methods can be wired directly to six Unity UI Buttons.
        public void PressX() { PressAxis(0, 1); }
        public void PressNegativeX() { PressAxis(0, -1); }
        public void PressY() { PressAxis(1, 1); }
        public void PressNegativeY() { PressAxis(1, -1); }
        public void PressZ() { PressAxis(2, 1); }
        public void PressNegativeZ() { PressAxis(2, -1); }

        public void Configure(Camera camera, Transform viewFocus, GameObject[] blockingWalls,
            GameObject equippedMask, GameObject[] maskOnlyObjects)
        {
            targetCamera = camera;
            focus = viewFocus;
            wallsHiddenByMask = blockingWalls ?? new GameObject[0];
            maskVisual = equippedMask;
            visibleWhenEquipped = maskOnlyObjects ?? new GameObject[0];
        }

        void PressAxis(int axis, int sign)
        {
            if (!maskEquipped || (effects != null && effects.Busy)) return;
            int oldAxis = AxisOf(currentView);
            if (oldAxis == axis) sign = -SignOf(currentView);
            currentView = FromAxis(axis, sign);
            ApplyObstacle();
            ApplyView();
        }

        void ApplyMask()
        {
            ApplyObstacle();
            if (maskVisual) maskVisual.SetActive(maskEquipped);
            foreach (var obj in visibleWhenEquipped)
                if (obj) obj.SetActive(maskEquipped);
            if (maskEquipped) ApplyView();
            else if (targetCamera)
            {
                targetCamera.transform.SetPositionAndRotation(initialCameraPosition, initialCameraRotation);
                if (effects) effects.LeaveView();
                targetCamera.orthographic = initialOrthographic;
                targetCamera.orthographicSize = initialOrthographicSize;
            }
        }

        void OnDisable()
        {
            if (effects) effects.CancelTransition();
            if (proximityWall) proximityWall.Restore();
            if (initialWallStates != null) { maskEquipped = false; ApplyMask(); }
        }

        void ApplyObstacle()
        {
            if (proximityWall) proximityWall.SetDimension(maskEquipped && AxisOf(currentView) == 0);
        }
        void LateUpdate() { if (maskEquipped) ApplyView(); }

        void ApplyView()
        {
            if (!targetCamera) return;
            Vector3 center = playerTarget ? playerTarget.position + Vector3.up : (focus ? focus.position : transform.position);
            Vector3 side;
            switch (currentView)
            {
                case View.X: side = Vector3.right; break;
                case View.NegativeX: side = Vector3.left; break;
                case View.Y: side = Vector3.up; break;
                case View.NegativeY: side = Vector3.down; break;
                case View.NegativeZ: side = Vector3.back; break;
                default: side = Vector3.forward; break;
            }
            Vector3 screenUp = currentView == View.Y ? Vector3.forward
                : currentView == View.NegativeY ? Vector3.back : Vector3.up;
            targetCamera.orthographic = true;
            targetCamera.orthographicSize = orthographicSize;
            if (effects) effects.SetView(center, side, screenUp, distance);
        }

        static int AxisOf(View view)
        {
            if (view == View.X || view == View.NegativeX) return 0;
            if (view == View.Y || view == View.NegativeY) return 1;
            return 2;
        }

        static int SignOf(View view)
        {
            return view == View.NegativeX || view == View.NegativeY || view == View.NegativeZ ? -1 : 1;
        }

        static View FromAxis(int axis, int sign)
        {
            if (axis == 0) return sign > 0 ? View.X : View.NegativeX;
            if (axis == 1) return sign > 0 ? View.Y : View.NegativeY;
            return sign > 0 ? View.Z : View.NegativeZ;
        }
    }
}


