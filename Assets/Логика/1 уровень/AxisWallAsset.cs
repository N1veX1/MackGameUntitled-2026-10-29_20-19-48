using UnityEngine;

/// <summary>Reusable axis wall, independent of the tutorial sequence.</summary>
[DefaultExecutionOrder(900)]
public sealed class AxisWallAsset : MonoBehaviour
{
    public MaskAxisCamera.ViewDirection direction;
    [Tooltip("Only the disappearing part; do not include the backing wall.")]
    public GameObject obstacle;
    [Tooltip("The surface behind the wall in the selected projection.")]
    public Renderer mergeSurface;
    public MaskAxisCamera mask;
    public MaskDemoPlayer player;
    MaskProximityWall gate;
    void Start()
    {
        if (!mask) foreach (var item in FindObjectsByType<MaskAxisCamera>())
            if (item.gameObject.scene == gameObject.scene) { mask = item; break; }
        if (!player) foreach (var item in FindObjectsByType<MaskDemoPlayer>())
            if (item.gameObject.scene == gameObject.scene) { player = item; break; }
        if (!mask || !player || !obstacle || !mergeSurface)
        { Debug.LogError("Axis wall needs a player, mask, obstacle and merge surface.", this); enabled = false; return; }
        gate = obstacle.GetComponent<MaskProximityWall>();
        if (!gate) gate = obstacle.AddComponent<MaskProximityWall>();
        gate.Initialize(obstacle.GetComponentsInChildren<Renderer>(true), obstacle.GetComponentsInChildren<Collider>(true),
            player.transform, mask.GetComponent<MaskViewEffects>());
        gate.ConfigureMerge(mergeSurface, true, true, mergeSurface);
    }
    void Update() { if (gate) gate.SetDimension(mask && mask.MaskEnabled && mask.CurrentDirection == direction); }
    void OnDisable() { if (gate) gate.Restore(); }
}
