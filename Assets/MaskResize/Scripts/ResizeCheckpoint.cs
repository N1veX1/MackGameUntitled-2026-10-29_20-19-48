using UnityEngine;

namespace MaskResize
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ResizeCheckpoint : MonoBehaviour
    {
        [SerializeField] Vector3 respawnFeet;
        [SerializeField] bool finish;
        [Tooltip("Optional movable spawn marker. Used by trial prefabs; follows their position and rotation.")]
        [SerializeField] Transform respawnPoint;
        public void Configure(Vector3 feet, bool completed = false) { respawnFeet = feet; respawnPoint = null; finish = completed; }
        public void Configure(Transform point, bool completed = false) { respawnPoint = point; finish = completed; }
        void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<ResizeMaskPlayer>();
            if (!player) return;
            player.SetCheckpoint(respawnPoint ? respawnPoint.position : respawnFeet,
                respawnPoint ? respawnPoint.rotation : Quaternion.identity);
            if (finish) player.CompleteTutorial();
        }
    }
}
