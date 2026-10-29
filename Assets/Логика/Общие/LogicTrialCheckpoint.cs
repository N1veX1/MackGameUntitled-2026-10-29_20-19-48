using UnityEngine;

/// <summary>Portable checkpoint for the XYZ and inversion player prefabs.</summary>
[RequireComponent(typeof(BoxCollider))]
public sealed class LogicTrialCheckpoint : MonoBehaviour
{
    [Tooltip("Marker at the centre of the player's capsule, 0.9 m above the floor.")]
    public Transform spawn;
    public bool inversionMaskOn;
    void OnTriggerEnter(Collider other)
    {
        if (!spawn) return;
        var axis = other.GetComponentInParent<MaskDemoPlayer>();
        if (axis) axis.SetCheckpoint(spawn.position);
        var inversion = other.GetComponentInParent<MaskInversionPlayer>();
        if (inversion) inversion.SetRespawnPoint(spawn.position, spawn.rotation, inversionMaskOn);
    }
}
