using UnityEngine;
using UnityEngine.InputSystem;

// NOT: Eskiden burada "using UnityEditor.Animations;" vardı. UnityEditor isim alanı
// oyun build'ine dahil edilmediği için proje build alınırken derleme hatası veriyordu.
// Ayrıca her karede Debug.Log çağrılıyordu (ciddi performans kaybı). İkisi de kaldırıldı.

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class RuntimeMovement : MonoBehaviour
{
    private Movement _input;
    private CharacterController _controller;
    private Animator _animator;
    [SerializeField] private float fraction = 1f;

    private void Start()
    {
        _controller = GetComponent<CharacterController>();
        _input = GetComponent<Movement>();
        _animator = GetComponent<Animator>();
    }

    public void Update()
    {
        Move();
    }

    private void Move()
    {
        if (_controller == null || _input == null) return;

        float div = Mathf.Approximately(fraction, 0f) ? 1f : fraction;
        _controller.Move(new Vector3((_input.moveVal.x * _input.moveSpeed) / div, 0f, (_input.moveVal.y * _input.moveSpeed) / div));

        if (_animator != null)
        {
            _animator.SetFloat("speed", Mathf.Abs(_controller.velocity.x) + Mathf.Abs(_controller.velocity.z));
        }
    }
}
