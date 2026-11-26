using UnityEngine;

namespace RedLoop
{
    // Toplanabilir nesnenin türü. Etiketi "coin" olan ve bu bileşeni olmayan nesneler zaman parçası sayılır.
    public class Collectible : MonoBehaviour
    {
        public CollectibleKind kind = CollectibleKind.Shard;
        public int index;
    }
}
