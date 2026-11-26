using UnityEngine;
using UnityEngine.SceneManagement;
using RedLoop;

// Bölüm geçidi. Artık bölümün görevleri (zaman parçaları / hatıralar) tamamlanmadan açılmıyor,
// son bölümden sonra var olmayan bir sahneyi yüklemeye çalışıp hata vermiyor.
public class LoadScene : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        TryEnter(other);
    }

    // Oyuncu kapının içindeyken son parçayı toplarsa (ör. merdiven tepesi) tekrar girmesine gerek kalmasın.
    private void OnTriggerStay(Collider other)
    {
        TryEnter(other);
    }

    private void TryEnter(Collider other)
    {
        if (!other.gameObject.CompareTag("Player")) return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.TryUsePortal(this);
            return;
        }

        // GameManager yoksa eski davranış, ama güvenli şekilde.
        int next = SceneManager.GetActiveScene().buildIndex + 1;
        if (next < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(next);
        }
    }
}
