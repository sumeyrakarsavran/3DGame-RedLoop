using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using RedLoop;

// Kızıl Muhafız (robot / zombi) yapay zekâsı: devriye -> takip -> kızıl küre fırlatma.
//
// Düzeltilenler:
//  - Bir robotun _player alanı boştu -> takipte NullReferenceException. Artık "Player" etiketiyle bulunuyor.
//  - NavMesh dışındaki ajan için SetDestination hata spam'i yapıyordu -> korunuyor.
//  - LookAt oyuncu merdivendeyken robotu yatırıyordu -> sadece yatay döner.
//  - Elindeki küre şablonu oyun başında düşüp yuvarlanıyordu -> gizleniyor, sadece kopyası atılıyor.
//  - Menü/diyalog sırasında da saldırıyordu -> GameManager durumuna bakıyor.
//
// Dengeleme (oyuncu geri bildirimi: "çok fazla top atıyorlar, kaçamıyorum"):
//  - Bölüm başında birkaç saniye saldırı yok.
//  - Atıştan önce robot kızarıp yanıp söner ve uyarı sesi çıkarır.
//  - Aynı anda en fazla 2 robot atış hazırlayabilir.
//  - Atışlar daha seyrek, toplar yavaş ve kavisli: hareket eden oyuncu kaçabilir.
//  - Görüş/saldırı mesafesi kısaldı; robotlar yuvalarından fazla uzaklaşmaz (kaçınca peşini bırakır).
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

    private const float StartGrace = 6f;      // bölüm başında saldırı yok (sn)
    private const float LeashRadius = 13f;    // yuvadan en fazla bu kadar uzaklaşır
    private const float ProjectileSpeed = 12f;
    private const int MaxSimultaneousThrowers = 2;
    private static int _activeThrowers;

    private Animator _animator;
    private Guardian _guardian;
    private bool beingHandled = false;
    private float _searchCooldown;
    private Vector3 _home;
    private bool _returningHome;
    private Vector3 _sphereWorldScale = Vector3.one * 0.1f;

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
        _home = transform.position;
        _guardian = GetComponent<Guardian>();

        timeBetweenAttacks = Mathf.Max(timeBetweenAttacks, 3.6f);
        sightRange = Mathf.Min(sightRange > 0f ? sightRange : 12f, 12f);
        attackRange = Mathf.Min(attackRange > 0f ? attackRange : 8f, 8f);
        if (walkPointRange <= 0f) walkPointRange = 5f;

        if (sphere != null)
        {
            _sphereWorldScale = sphere.transform.lossyScale;
            if (sphere.scene.IsValid()) sphere.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (beingHandled) { beingHandled = false; _activeThrowers = Mathf.Max(0, _activeThrowers - 1); }
    }

    // Domain reload kapalıysa statik sayaç takılı kalmasın
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { _activeThrowers = 0; }

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

        float distToPlayer = Vector3.Distance(transform.position, _player.position);
        float playerFromHome = Vector3.Distance(_home, _player.position);
        playerInsightRange = distToPlayer <= sightRange && Physics.CheckSphere(transform.position, sightRange, player);
        playerAttackRange = distToPlayer <= attackRange;

        // Oyuncu yuvadan çok uzaklaştıysa peşini bırak, eve dön
        if (playerFromHome > LeashRadius + attackRange) playerInsightRange = false;
        if (Vector3.Distance(transform.position, _home) > LeashRadius) _returningHome = true;

        if (_returningHome)
        {
            SetAnim(true, false);
            _agent.SetDestination(_home);
            if (Vector3.Distance(transform.position, _home) < 2f) _returningHome = false;
            return;
        }

        if (!playerInsightRange) Patroling();
        else if (!playerAttackRange) ChasePlayer();
        else AttackPlayer();
    }

    private void SetAnim(bool isFar, bool isAttacking)
    {
        if (_animator == null || !_animator.isActiveAndEnabled) return;
        _animator.SetBool("isFar", isFar);
        _animator.SetBool("isAttacking", isAttacking);
    }

    void Patroling()
    {
        if (!destinationPointSet) SearchWalkPoint();
        if (destinationPointSet) _agent.SetDestination(destinationPoint);

        Vector3 d = transform.position - destinationPoint;
        d.y = 0f;
        if (d.magnitude < 1.0f) destinationPointSet = false;
    }

    void SearchWalkPoint()
    {
        SetAnim(true, false);
        if (Time.time < _searchCooldown) return;
        _searchCooldown = Time.time + 0.25f;

        // Yuvanın etrafında devriye
        Vector3 candidate = _home + new Vector3(Random.Range(-walkPointRange, walkPointRange), 0f, Random.Range(-walkPointRange, walkPointRange));
        NavMeshHit hit;
        if (NavMesh.SamplePosition(candidate, out hit, 2.0f, NavMesh.AllAreas))
        {
            destinationPoint = hit.position;
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
        if (_animator != null && _animator.isActiveAndEnabled) _animator.SetBool("isFar", false);
        _agent.SetDestination(transform.position);

        Vector3 look = _player.position;
        look.y = transform.position.y;
        transform.LookAt(look);

        bool grace = GameManager.Instance != null && GameManager.Instance.LevelPlayTime < StartGrace;
        if (!alreadyAttacked && !beingHandled && !grace && _activeThrowers < MaxSimultaneousThrowers)
        {
            StartCoroutine(HandleIt());
        }
    }

    IEnumerator ChangeTag(GameObject go)
    {
        yield return new WaitForSeconds(2.5f);
        if (go != null) go.tag = "Respawn"; // yere düşmüş küre artık zarar vermez
    }

    void ResetAttack()
    {
        alreadyAttacked = false;
    }

    private IEnumerator HandleIt()
    {
        beingHandled = true;
        _activeThrowers++;
        if (_animator != null && _animator.isActiveAndEnabled) _animator.SetBool("isAttacking", true);

        // Uyarı: kızarma + şarj sesi
        const float windup = 0.9f;
        if (_guardian == null) _guardian = GetComponent<Guardian>();
        if (_guardian != null) _guardian.Telegraph(windup);
        if (SoundFX.Instance != null) SoundFX.Instance.PlayAt(Sfx.Charge, transform.position + Vector3.up * 1.2f, 0.8f, Random.Range(0.9f, 1.1f), 30f);
        yield return new WaitForSeconds(windup);

        if (sphere != null && _player != null && GameManager.IsGameplayActive)
        {
            var pos = transform.position + Vector3.up * 1.2f + transform.forward * 0.5f;
            var go = Instantiate(sphere, pos, Quaternion.identity);
            go.transform.localScale = _sphereWorldScale * 1.6f; // biraz büyük: daha kolay görünsün
            go.tag = "sphere";
            go.SetActive(true);

            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Oyuncunun ŞU ANKİ yerine kavisli atış: yerinde duran vurulur, hareket eden kaçar.
                Vector3 target = _player.position + Vector3.up * 0.9f;
                Vector3 delta = target - pos;
                Vector3 flat = new Vector3(delta.x, 0f, delta.z);
                float dist = Mathf.Max(0.5f, flat.magnitude);
                float time = dist / ProjectileSpeed;
                float g = Mathf.Abs(Physics.gravity.y);
                Vector3 v = flat / time;
                v.y = delta.y / time + 0.5f * g * time;
                rb.velocity = v;
                rb.angularVelocity = Vector3.zero;
            }

            var orb = go.GetComponent<HostileOrb>();
            if (orb == null) orb = go.AddComponent<HostileOrb>();
            orb.lifeTime = 6f;
            orb.destroyOnImpact = false;
            if (go.GetComponent<TrailRenderer>() == null) FX.Trail(go, new Color(1f, 0.2f, 0.1f), 0.3f, 0.12f);

            if (SoundFX.Instance != null) SoundFX.Instance.PlayAt(Sfx.Throw, pos, 0.9f, Random.Range(0.9f, 1.1f));
            StartCoroutine(ChangeTag(go));
        }

        alreadyAttacked = true;
        Invoke(nameof(ResetAttack), timeBetweenAttacks + Random.Range(0f, 1.2f));
        beingHandled = false;
        _activeThrowers = Mathf.Max(0, _activeThrowers - 1);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);
    }
}
