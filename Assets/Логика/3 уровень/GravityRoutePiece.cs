using UnityEngine;

/// <summary>Local route samples and the editable entry/exit sockets of a platform module.</summary>
public sealed class GravityRoutePiece : MonoBehaviour
{
    public Transform samples;
    public RoundedCornerTransition corner;
    public Transform Entry => samples && samples.childCount > 0 ? samples.GetChild(0) : null;
    public Transform Exit => samples && samples.childCount > 0 ? samples.GetChild(samples.childCount - 1) : null;
    void OnDrawGizmosSelected()
    {
        if (!samples) return;
        Gizmos.color = Color.cyan;
        for (int i = 1; i < samples.childCount; i++) Gizmos.DrawLine(samples.GetChild(i - 1).position, samples.GetChild(i).position);
        if (Entry) { Gizmos.color = Color.green; Gizmos.DrawRay(Entry.position, Entry.forward); Gizmos.DrawSphere(Entry.position, .08f); }
        if (Exit) { Gizmos.color = Color.yellow; Gizmos.DrawRay(Exit.position, Exit.up); Gizmos.DrawSphere(Exit.position, .08f); }
    }
}
