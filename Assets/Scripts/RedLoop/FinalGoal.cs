using UnityEngine;

namespace RedLoop
{
    // Son bölümdeki hedef (Döngünün Kalbi). Oyuncu ulaşınca final başlar.
    public class FinalGoal : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other) { Check(other); }
        private void OnTriggerStay(Collider other) { Check(other); }

        private void Check(Collider other)
        {
            if (other.CompareTag("Player") && GameManager.Instance != null)
            {
                GameManager.Instance.ReachFinalGoal();
            }
        }
    }
}
