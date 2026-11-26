using UnityEngine;
using TMPro;
using RedLoop;

// Oyuncunun üzerindeki toplama bileşeni.
// Eski hali: 2. ve 3. sahnede _text atanmadığı için ilk toplamada NullReferenceException veriyordu,
// sayaç her sahnede sıfırlanıyordu ve aynı parça iki kez sayılabiliyordu.
// Yeni hali: sayım GameManager'da tutulur, arayüzü GameUI çizer.
public class CoinCollection : MonoBehaviour
{
    private AudioSource click;
    [SerializeField] private TMP_Text _text;
    private int count = 0;

    private void Start()
    {
        click = GetComponent<AudioSource>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.gameObject.CompareTag("coin")) return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.Collect(other.gameObject);
            return;
        }

        // Yedek davranış (GameManager yoksa)
        var col = other.GetComponent<Collider>();
        if (col != null) col.enabled = false;
        count++;
        if (_text != null) _text.text = count.ToString();
        Destroy(other.gameObject);
        if (click != null) click.Play();
    }

    // Sahnedeki eski sayaç yazısını gizler (yerine yeni HUD geliyor).
    public void HideLegacyUI()
    {
        if (_text == null) return;
        var canvas = _text.GetComponentInParent<Canvas>();
        if (canvas != null) canvas.rootCanvas.gameObject.SetActive(false);
        else _text.gameObject.SetActive(false);
    }
}
