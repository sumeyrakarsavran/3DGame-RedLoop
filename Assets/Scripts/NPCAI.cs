using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using RedLoop;

// Kızıl Muhafız (robot / zombi) yapay zekâsı: devriye -> takip -> kızıl küre fırlatma.
// Düzeltilenler:
//  - Sahnedeki 4 robottan birinin _player alanı boştu -> takipte NullReferenceException. Artık "Player" etiketiyle bulunuyor.
//  - NavMesh dışındaki ajan için SetDestination hata spam'i yapıyordu -> korunuyor.
//  - LookAt oyuncu merdivendeyken robotu öne/arkaya yatırıyordu -> sadece yatay döner.
//  - Fırlatılan küreler hiç silinmiyordu (sonsuz birikme) -> HostileOrb ile ömürleri var.
//  - Menü, diyalog, oyun sonu sırasında da saldırıyordu -> GameManager durumuna bakıyor.
//  - Fırlatma/çarpma ses efektleri eklendi.
public class NPCAI : MonoBehaviour
{
    public NavMeshAgent _agent;
    [SerializeField] Transform _player;
    public LayerMask ground, player;

    public Vector3 destinationPoint;
    private bool destinationPointSet;
    public float walkPointRange;

    public float timeBetweenAttacks;
    private bool alreadyAttacked;
    public GameObject sphere;

    public float sightRange, attackRange;
    public bool playerInsightRange, playerAttackRange;

    private Animator _animator;
    private bool beingHandled = false;
    private float _searchCooldown;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
    }

    private void Start()
    {
        if (_player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _player = p.transform;
        }
        if (timeBetweenAttacks <= 0f) timeBetweenAttacks = 2.5f;

        // Fırlatılan küre şablonu robotun elindeki çocuk nesne. Yerçekimli rigidbody olduğu için
        // oyun başlar başlamaz elden düşüp yerde yuvarlanıyor ve oyuncuya hasar veriyordu.
        // Şablonu gizliyoruz; atışta kopyası açılıyor.
        if (sphere != null)
        {
            _sphereWorldScale = sphere.transform.lossyScale;
            if (sphere.scene.IsValid()) sphere.SetActive(false);
        }
    }

    private Vector3 _sphereWorldScale = Vector3.one * 0.1f;

    private bool AgentReady
    {
        get { return _agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh; }
    }

    private void Update()
    {
        if (_player == null || !AgentReady) return;

        if (!GameManager.IsGameplayActive)
        {
            if (_agent.hasPath) _agent.ResetPath();
            SetAnim(false, false);
            return;
        }

        playerInsightRange = Physics.CheckSphere(transform.position, sightRange, player);
        playerAttackRange = Physics.CheckSphere(transform.position, attackRange, player);

        // Devriye / Takip / Saldırı
        if (!playerInsightRange && !playerAttackRange) Patroling();
        if (playerInsightRange && !playerAttackRange) ChasePlayer();
        if (playerInsightRange && playerAttackRange) AttackPlayer();
    }

    private void SetAnim(bool isFar, bool isAttacking)
    {
        if (_animator == null) return;
        _animator.SetBool("isFar", isFar);
        _animator.SetBool("isAttacking", isAttacking);
    }

    void Patroling()
    {
        if (!destinationPointSet)
        {
            SearchWalkPoint();
        }

        if (destinationPointSet)
        {
            _agent.SetDestination(destinationPoint);
        }

        Vector3 distanceToDestinationPoint = transform.position - destinationPoint;
        distanceToDestinationPoint.y = 0f;
        if (distanceToDestinationPoint.magnitude < 1.0f)
        {
            destinationPointSet = false;
        }
    }

    void SearchWalkPoint()
    {
        SetAnim(true, false);

        // Her karede yeni nokta denemesin
        if (Time.time < _searchCooldown) return;
        _searchCooldown = Time.time + 0.25f;

        float randomX = Random.Range(-walkPointRange, walkPointRange);
        float randomZ = Random.Range(-walkPointRange, walkPointRange);

        Vector3 candidate = new Vector3(transform.position.x + randomX, transform.position.y, transform.position.z + randomZ);

        NavMeshHit hit;
        if (NavMesh.SamplePosition(candidate, out hit, 2.0f, NavMesh.AllAreas))
        {
            destinationPoint = hit.position;
            destinationPointSet = true;
        }
        else if (Physics.Raycast(candidate + Vector3.up, Vector3.down, 3.0f, ground))
        {
            destinationPoint = candidate;
            destinationPointSet = true;
        }
    }

    void ChasePlayer()
    {
        SetAnim(true, false);
        _agent.SetDestination(_player.position);
    }

    void AttackPlayer()
    {
        if (_animator != null) _animator.SetBool("isFar", false);
        _agent.SetDestination(transform.position);

        Vector3 look = _player.position;
        look.y = transform.position.y;
        transform.LookAt(look);

        if (!alreadyAttacked && !beingHandled)
        {
            StartCoroutine(HandleIt());
        }
    }

    IEnumerator ChangeTag(GameObject go)
    {
        yield return new WaitForSeconds(2);
        if (go != null) go.tag = "Respawn"; // yere düşmüş küre artık zarar vermez
    }

    void ResetAttack()
    {
        alreadyAttacked = false;
    }

    private IEnumerator HandleIt()
    {
        beingHandled = true;
        if (_animator != null) _animator.SetBool("isAttacking", true);
        yield return new WaitForSeconds(0.82f);

        if (sphere != null && _player != null && GameManager.IsGameplayActive)
        {
            var pos = new Vector3(transform.position.x, transform.position.y + 1.2f, transform.position.z);
            var go = Instantiate(sphere, pos, Quaternion.identity);
            go.transform.localScale = _sphereWorldScale;
            go.tag = "sphere";
            go.SetActive(true);
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 target = _player.position + Vector3.up * 1.0f;
                Vector3 dir = (target - pos).normalized;
                rb.AddForce(dir * 22f, ForceMode.Impulse);
                rb.AddForce(Vector3.up * 2.5f, ForceMode.Impulse);
            }

            var orb = go.GetComponent<HostileOrb>();
            if (orb == null) orb = go.AddComponent<HostileOrb>();
            orb.lifeTime = 6f;
            orb.destroyOnImpact = false;

            if (SoundFX.Instance != null) SoundFX.Instance.PlayAt(Sfx.Throw, pos, 0.9f, Random.Range(0.9f, 1.1f));

            StartCoroutine(ChangeTag(go));
        }

        alreadyAttacked = true;
        Invoke(nameof(ResetAttack), timeBetweenAttacks);
        beingHandled = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);
    }
}
