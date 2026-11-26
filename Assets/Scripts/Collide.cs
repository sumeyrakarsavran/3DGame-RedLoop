using UnityEngine;
using RedLoop;

// Oyuncunun hasar ve tetikleyici bileşeni.
// Eski hali: hasar kodu yorum satırındaydı (düşmanlar can azaltamıyordu), "UI" etiketli
// tetikleyicinin çocuğu yoksa hata veriyordu, oyun bitince Time.timeScale = 0 yapılıp
// oyuncu ekranda kilitli kalıyordu.
public class Collide : MonoBehaviour
{
    private int health = 3;
    [SerializeField] GameObject[] _healthUI;
    [SerializeField] private GameObject _gameOver;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("sphere"))
        {
            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.Damage(other.transform.position))
                {
                    // Bizi vuran küre yok olur, tekrar tekrar vurmasın.
                    Destroy(other.gameObject);
                }
            }
            else
            {
                LegacyDamage();
            }
            return;
        }

        if (other.gameObject.CompareTag("UI"))
        {
            // Son bölümdeki hedef noktasını kendi bileşeni (FinalGoal) halleder.
            if (other.GetComponent<FinalGoal>() != null) return;

            if (other.transform.childCount > 0)
            {
                other.transform.GetChild(0).gameObject.SetActive(true);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("UI"))
        {
            if (other.GetComponent<FinalGoal>() != null) return;

            if (other.transform.childCount > 0)
            {
                other.transform.GetChild(0).gameObject.SetActive(false);
            }
        }
    }

    // Sahnedeki eski kalp ikonlarını ve "GAME OVER" yazısını gizler (yerine yeni HUD geliyor).
    public void HideLegacyUI()
    {
        if (_healthUI != null)
        {
            foreach (var h in _healthUI)
            {
                if (h == null) continue;
                var canvas = h.GetComponentInParent<Canvas>();
                if (canvas != null) canvas.rootCanvas.gameObject.SetActive(false);
                else h.SetActive(false);
            }
        }
        if (_gameOver != null) _gameOver.SetActive(false);
    }

    private void LegacyDamage()
    {
        if (health <= 0) return;
        health--;
        if (_healthUI != null && health < _healthUI.Length && _healthUI[health] != null)
        {
            _healthUI[health].SetActive(false);
        }
        if (health == 0 && _gameOver != null)
        {
            _gameOver.SetActive(true);
        }
    }
}
