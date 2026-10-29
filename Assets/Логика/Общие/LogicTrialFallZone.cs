using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class LogicTrialFallZone : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        var axis = other.GetComponentInParent<MaskDemoPlayer>();
        if (axis) axis.Respawn();
        var inversion = other.GetComponentInParent<MaskInversionPlayer>();
        if (inversion) inversion.Respawn();
    }
}
