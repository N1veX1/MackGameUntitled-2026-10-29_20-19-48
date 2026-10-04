using UnityEngine;

public sealed class MaskInversionFallTrigger : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponentInParent<MaskInversionPlayer>();
        if (player) player.Respawn();
    }
}
