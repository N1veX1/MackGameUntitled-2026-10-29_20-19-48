using UnityEngine;
namespace MaskTime
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class TimeCheckpoint : MonoBehaviour
    {
        [SerializeField] Transform spawn;
        [SerializeField] bool finish;
        public void Configure(Transform point,bool isFinish=false){spawn=point;finish=isFinish;}
        void OnTriggerEnter(Collider other)
        {
            var player=other.GetComponentInParent<TimeMaskPlayer>();if(!player)return;
            player.SetCheckpoint(spawn);if(finish)player.Complete();
        }
    }
}
