using UnityEngine;
using UnityEngine.InputSystem;
[RequireComponent(typeof(CharacterController))]
[DefaultExecutionOrder(-100)]
public sealed class MaskDemoPlayer : MonoBehaviour
{
    [SerializeField, Min(.1f)] float moveSpeed = 4f;
    [SerializeField] Camera movementCamera;
    [SerializeField] float gravity = -20f;
    [SerializeField] float eyeHeight = 1.7f;
    CharacterController controller;
    MaskAxisCamera mask;
    MaskViewEffects viewEffects;
    GameObject head;
    Transform headVisualPivot;
    float verticalSpeed;
    Vector3 walkForward = Vector3.forward;
    [SerializeField] float fallRespawnHeight = -5f;
    Vector3 checkpoint;
    Quaternion checkpointRotation;
    public event System.Action Respawned;
    public Vector3 Checkpoint => checkpoint;
    public Transform HeadVisualPivot => headVisualPivot;
    public void SetCheckpoint(Vector3 position) { checkpoint = position; checkpointRotation = transform.rotation; }
    public void Respawn()
    {
        if (!controller) return;
        if (mask) mask.ResetAfterRespawn();
        controller.enabled = false;
        transform.SetPositionAndRotation(checkpoint, checkpointRotation);
        ResetMotion();
        controller.enabled = true;
        Physics.SyncTransforms();
        controller.Move(Vector3.down * .25f);
        verticalSpeed = -2f;
        Respawned?.Invoke();
    }
    public void ResetMotion() { verticalSpeed = 0; }
    void Awake()
    {
        controller = GetComponent<CharacterController>();
        SetCheckpoint(transform.position);
        if (!movementCamera) movementCamera = GetComponentInChildren<Camera>();
        if (!movementCamera) movementCamera = Camera.main;
        mask = GetComponentInChildren<MaskAxisCamera>();
        controller.enabled = false;
        controller.radius = .28f; controller.height = 1.8f;
        controller.center = Vector3.zero; controller.skinWidth = .02f; controller.stepOffset = .2f; controller.minMoveDistance = 0;
        controller.enabled = true;
        if(movementCamera && movementCamera.transform.IsChildOf(transform))
            movementCamera.transform.localPosition = new Vector3(0, eyeHeight - .9f, .06f);
        Transform equippedMask = null;
        foreach(Transform child in transform)
        {
            switch(child.name)
            {
                case "Torso": case "Left arm": case "Right arm": case "Left leg": case "Right leg":
                    child.gameObject.SetActive(false); break;
                case "Head":
                    head=child.gameObject;
                    child.localPosition=new Vector3(0,eyeHeight-.9f,0);
                    child.localScale=Vector3.one*.36f;
                    head.SetActive(false); break;
            }
            if(child.name.Contains("equipped visual"))
            {
                child.localPosition=new Vector3(0,eyeHeight-.9f,.19f);
                equippedMask = child;
            }
        }
        // Reparent only the visual objects, after traversing the original children.
        // The player capsule and both camera modes keep their existing transforms.
        if (head || equippedMask)
        {
            headVisualPivot = new GameObject("Head Visual Pivot").transform;
            headVisualPivot.SetParent(transform, false);
            headVisualPivot.localPosition = new Vector3(0, eyeHeight - .9f, 0);
            if (movementCamera) headVisualPivot.rotation = movementCamera.transform.rotation;
            if (head)
            {
                head.transform.SetParent(headVisualPivot, false);
                head.transform.localPosition = Vector3.zero;
            }
            if (equippedMask)
            {
                // The mask controller still references this same prefab instance.
                equippedMask.SetParent(headVisualPivot, false);
                equippedMask.localPosition = new Vector3(0, 0, .19f);
            }
        }
    }
    public Vector3 MovementDirection(Vector3 input)
    {
        if(!movementCamera) return Vector3.zero;
        // Keep the last first-person heading during a projection; keys never become flight controls.
        if(!mask || !mask.MaskEnabled)
        {
            Vector3 flat=Vector3.ProjectOnPlane(movementCamera.transform.forward,Vector3.up);
            if(flat.sqrMagnitude>.001f) walkForward=flat.normalized;
        }
        Vector3 right=Vector3.Cross(Vector3.up,walkForward);
        return Vector3.ClampMagnitude(right*input.x+walkForward*input.z,1);
    }
    void Update()
    {
        if(transform.position.y < fallRespawnHeight) { Respawn(); return; }
        if(!controller.enabled) return;
        var k=Keyboard.current;
        Vector3 input=Vector3.zero;
        if(k!=null) input=new Vector3((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),0,(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));
        if(controller.isGrounded && verticalSpeed<0) verticalSpeed=-2;
        verticalSpeed+=gravity*Time.deltaTime;
        Vector3 displacement = (MovementDirection(input)*moveSpeed+Vector3.up*verticalSpeed)*Time.deltaTime;
        MaskProximityWall.PreparePlayerMovement(transform, displacement);
        controller.Move(displacement);
    }
    void LateUpdate()
    {
        // MaskAxisCamera creates these effects in Awake after this player's Awake.
        if (!viewEffects && mask) viewEffects = mask.GetComponent<MaskViewEffects>();
        if (headVisualPivot && viewEffects)
            headVisualPivot.rotation = viewEffects.FirstPersonLookRotation;
        if(head) head.SetActive(mask && mask.MaskEnabled);
    }
}


