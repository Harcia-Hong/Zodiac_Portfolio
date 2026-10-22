using UnityEngine;

/// <summary>
/// 보스 전용 투사체 (검기, 에너지볼 등)
/// 
/// - 직선 이동 → 플레이어 충돌 시 데미지 + 넉백
/// - lifetime 후 자동 소멸
/// - 나중에 상태이상(기절, 슬로우 등) 확장 가능하도록 설계
/// 
/// 사용법:
///   프리팹에 Collider(isTrigger) + 이 스크립트 부착
///   BossPatternExecutor가 Initialize() 호출하여 방향/데미지 설정
/// </summary>
public class BossProjectile : MonoBehaviour
{
    [Header("이동 설정")]
    [SerializeField] private float speed = 15f;
    [SerializeField] private float lifetime = 5f;

    [Header("판정 설정")]
    [SerializeField] private float knockbackForce = 5f;

    [Header("VFX")]
    [SerializeField] private GameObject hitVFXPrefab;
    [SerializeField] private GameObject trailVFXPrefab;

    // 외부에서 설정되는 값
    private Vector3 moveDirection;
    private int damage;
    private bool isInitialized = false;

    // 내부 상태
    private float spawnTime;
    private GameObject trailInstance;

    /// <summary>
    /// 투사체 초기화 (BossPatternExecutor에서 호출)
    /// </summary>
    /// <param name="direction">이동 방향 (정규화됨)</param>
    /// <param name="damage">데미지</param>
    /// <param name="speed">이동 속도 (0이면 프리팹 기본값 사용)</param>
    /// <param name="knockback">넉백 힘 (0이면 프리팹 기본값 사용)</param>
    public void Initialize(Vector3 direction, int damage, float speed = 0f, float knockback = 0f)
    {
        moveDirection = direction.normalized;
        this.damage = damage;

        if (speed > 0f) this.speed = speed;
        if (knockback > 0f) this.knockbackForce = knockback;

        spawnTime = Time.time;
        isInitialized = true;

        // 이동 방향으로 회전
        if (moveDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(moveDirection);
        }

        // 트레일 VFX 생성
        if (trailVFXPrefab != null)
        {
            trailInstance = Instantiate(trailVFXPrefab, transform.position, transform.rotation, transform);
        }
    }

    private void Update()
    {
        if (!isInitialized) return;

        // 직선 이동
        transform.position += moveDirection * speed * Time.deltaTime;

        // 수명 초과 시 소멸
        if (Time.time - spawnTime >= lifetime)
        {
            DestroyProjectile();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isInitialized) return;

        // 플레이어 충돌 체크
        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth == null) return;

        // 데미지 적용
        playerHealth.TakeDamage(damage);

        // 넉백 적용
        if (knockbackForce > 0f)
        {
            Rigidbody targetRb = other.GetComponent<Rigidbody>();
            if (targetRb != null)
            {
                Vector3 knockbackDir = moveDirection;
                knockbackDir.y = 0f;
                targetRb.AddForce(knockbackDir * knockbackForce, ForceMode.Impulse);
            }
        }

        // 히트 VFX
        if (hitVFXPrefab != null)
        {
            Instantiate(hitVFXPrefab, transform.position, Quaternion.identity);
        }

        // 투사체 소멸
        DestroyProjectile();
    }

    /// <summary>
    /// 투사체 정리 및 소멸
    /// </summary>
    private void DestroyProjectile()
    {
        isInitialized = false;
        Destroy(gameObject);
    }
}