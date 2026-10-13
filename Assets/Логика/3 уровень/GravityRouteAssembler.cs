using System.Collections.Generic;
using UnityEngine;

/// <summary>Connect modules in array order; wire their route to the gravity player.</summary>
[DefaultExecutionOrder(-200)]
public sealed class GravityRouteAssembler : MonoBehaviour
{
    public CornerMaskTraversalController player;
    public GravityRoutePiece[] pieces;
    [Min(.5f)] public float halfWidth = 2;
    [Tooltip("Align each module's entry to the previous exit. Rotate the first module to orient the course.")]
    public bool snapModules = true;
    [SerializeField, HideInInspector] Transform generated;
    void Awake() { BuildRoute(); }
    [ContextMenu("Build route from modules")]
    public void BuildRoute()
    {
        if (!player || pieces == null || pieces.Length == 0) return;
        foreach (var piece in pieces)
            if (!piece || !piece.Entry || !piece.Exit) { Debug.LogError("Every route slot needs a valid module.", this); return; }
        // Only unit scale: scaling a module would change the player's surface offset.
        foreach (var piece in pieces)
            if ((piece.transform.lossyScale - Vector3.one).sqrMagnitude > .0001f)
            { Debug.LogError("Gravity modules require scale (1,1,1). Edit mesh width in the builder instead.", piece); return; }
        if (snapModules)
            for (int i = 1; i < pieces.Length; i++)
            {
                var previous = pieces[i - 1].Exit; var entry = pieces[i].Entry;
                Quaternion delta = previous.rotation * Quaternion.Inverse(entry.rotation);
                pieces[i].transform.rotation = delta * pieces[i].transform.rotation;
                pieces[i].transform.position += previous.position - entry.position;
            }
        for (int i = 1; i < pieces.Length; i++)
            if (Vector3.Distance(pieces[i - 1].Exit.position, pieces[i].Entry.position) > .03f ||
                Quaternion.Angle(pieces[i - 1].Exit.rotation, pieces[i].Entry.rotation) > 2)
            { Debug.LogError("Module sockets do not meet. Enable Snap Modules or align entry and exit.", pieces[i]); return; }
        if (generated) { generated.gameObject.SetActive(false); if (Application.isPlaying) Destroy(generated.gameObject); else DestroyImmediate(generated.gameObject); }
        generated = new GameObject("Generated route — edit the modules instead").transform;
        generated.SetParent(transform, false);
        var nodes = new GameObject("Ordered editable route samples").transform; nodes.SetParent(generated, false);
        var corners = new List<RoundedCornerTransition>();
        float distance = 0; Vector3 previousPoint = Vector3.zero; int count = 0;
        foreach (var piece in pieces)
        {
            float offset = distance;
            for (int i = 0; i < piece.samples.childCount; i++)
            {
                var sample = piece.samples.GetChild(i);
                if (count > 0 && i == 0) continue;
                if (count > 0) distance += Vector3.Distance(previousPoint, sample.position);
                var node = new GameObject("Route " + count.ToString("D4")).transform; node.SetParent(nodes, false);
                node.SetPositionAndRotation(sample.position, sample.rotation); previousPoint = sample.position; count++;
            }
            if (piece.corner)
            {
                var marker = new GameObject(piece.name + " — turn range").AddComponent<RoundedCornerTransition>();
                marker.transform.SetParent(generated, false);
                marker.startDistance = offset + piece.corner.startDistance; marker.endDistance = offset + piece.corner.endDistance;
                corners.Add(marker);
            }
        }
        player.ConfigureRoute(nodes, corners.ToArray(), halfWidth);
    }
}
