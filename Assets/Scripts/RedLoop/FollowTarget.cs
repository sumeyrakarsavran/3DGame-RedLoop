using UnityEngine;

namespace RedLoop
{
    // Atmosfer parçacıklarını oyuncunun etrafında tutar (parçacıklar dünya uzayında kalır, kaynak oyuncuyu izler).
    public class FollowTarget : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset;

        private void LateUpdate()
        {
            if (target != null) transform.position = target.position + offset;
        }
    }
}
