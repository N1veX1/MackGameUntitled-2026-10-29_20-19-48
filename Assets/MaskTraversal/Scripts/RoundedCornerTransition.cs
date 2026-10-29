using UnityEngine;
/// <summary>Axis changes only after the rounded route reaches this distance.</summary>
public sealed class RoundedCornerTransition:MonoBehaviour
{
 public string transitionName="Floor to wall";
 public string axisBefore="Y";
 public string axisAfter="Z";
 [Tooltip("Cumulative route distance at the end of this smooth curved corner.")]
 public float endDistance;
 [Tooltip("Start distance of the rounded section. The worn mask allows gravity changes here.")]
 public float startDistance;
}
