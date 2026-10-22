using UnityEngine;

/// <summary>
/// 토네이도 본체 — 스킬에 의해 스폰되어 자체적으로 행동하는 지속형 장판 컴포넌트.
/// 
/// 동작 흐름:
///   1. Initialize()로 이동 방향·속도·거리·지속시간·데미지 수치 주입받음
///   2. moveSpeed 속도로 moveDirection 방향으로 전진
///   3. maxTravelDistance에 도달하면 정지 (회전 모션은 VFX 측에서 항상 재생)
///   4. 이동/정지 무관하게 damageInterval마다 범위 내 적 검사 → 지속 데미지
///   5. lifetime 만료 시 자동 파괴
/// 
/// 확장 포인트:
///   - pullEnemiesToCenter: 토네이도 범위 내 적을 중심으로 끌어당김 (불씨 변종 등에서 활용)
///   - 추후 디버프 부여, 다중 히트 쿨다운 등 추가 가능
/// 
/// 재사용성:
///   분노 토네이도뿐 아니라 불씨, 얼음 폭풍 등 "이동 후 정지 + 지속 장판" 계열 스킬에 그대로 활용.
///   수치만 다르게 Initialize하면 동작 완료.
/// </summary>
public class Tornado : MonoBehaviour
{
    // ========================================================================
    // 확장 옵션 (인스펙터에서 토글) — 변종 토네이도 만들 때 활용
    // ========================================================================

    [Header("확장 옵션")]
    [Tooltip("범위 내 적을 토네이도 중심으로 끌어당길지 여부.\n" +
             "체크 시 데미지 틱과 함께 매 프레임 중심 방향으로 흡인.")]
    [SerializeField] private bool pullEnemiesToCenter = false;

    [Tooltip("끌어당기는 강도 — 값이 클수록 빠르게 빨려들어옴.\n" +
             "Lerp 비율 기반이므로 0.1 ~ 0.5 사이가 적당.")]
    [SerializeField, Range(0f, 1f)] private float pullStrength = 0.2f;

    // ========================================================================
    // 런타임 설정값 (Initialize로 주입)
    // ========================================================================

    private Vector3 moveDirection;     // 전진 방향 (수평 평면)
    private float moveSpeed;           // 전진 속도 (유닛/초)
    private float maxTravelDistance;   // 최대 이동 거리 (이후 정지)
    private float lifetime;            // 총 지속 시간 (이후 자동 파괴)
    private float damageRadius;        // 데미지 판정 반경
    private float damageInterval;      // 데미지 틱 간격 (초)
    private int damage;                // 한 틱당 데미지
    private LayerMask enemyLayer;      // 적 레이어 마스크
    private HitEffectType hitEffectType;

    // ========================================================================
    // 내부 상태
    // ========================================================================

    private float traveledDistance = 0f;    // 누적 이동 거리
    private float damageTimer = 0f;         // 다음 틱까지 누적 시간
    private bool isMoving = true;           // 이동 중인지 (false면 정지 상태)
    private bool isInitialized = false;     // Initialize 호출 전엔 Update 차단

    // ========================================================================
    // 초기화
    // ========================================================================

    /// <summary>
    /// 토네이도 초기화 — 스폰 직후 SpawnTornadoAction에서 호출.
    /// 모든 수치를 외부에서 주입받아 SO 설정과 인스턴스 동작을 깔끔하게 분리.
    /// </summary>
    /// <param name="direction">전진 방향 (Y축은 자동 제거됨)</param>
    /// <param name="speed">전진 속도 (유닛/초)</param>
    /// <param name="distance">최대 이동 거리 (유닛)</param>
    /// <param name="life">총 지속 시간 (초)</param>
    /// <param name="radius">데미지 판정 반경</param>
    /// <param name="interval">데미지 틱 간격 (초)</param>
    /// <param name="dmg">한 틱당 데미지</param>
    /// <param name="layer">적 레이어 마스크</param>
    /// <param name="effect">피격 히트 이펙트 타입</param>
    public void Initialize(
        Vector3 direction,
        float speed,
        float distance,
        float life,
        float radius,
        float interval,
        int dmg,
        LayerMask layer,
        HitEffectType effect)
    {
        // 방향은 수평 평면으로만 정규화 (쿼터뷰 게임 — Y축 이동 없음)
        moveDirection = direction;
        moveDirection.y = 0f;
        if (moveDirection.sqrMagnitude < 0.0001f)
        {
            // 방향이 0벡터로 들어온 안전장치 — 캐릭터 forward 사용
            moveDirection = transform.forward;
            moveDirection.y = 0f;
        }
        moveDirection.Normalize();

        moveSpeed = speed;
        maxTravelDistance = distance;
        lifetime = life;
        damageRadius = radius;
        damageInterval = interval;
        damage = dmg;
        enemyLayer = layer;
        hitEffectType = effect;

        isInitialized = true;

        // 지속시간 만료 시 자동 파괴 예약
        Destroy(gameObject, lifetime);
    }

    // ========================================================================
    // 메인 루프
    // ========================================================================

    private void Update()
    {
        if (!isInitialized) return;

        HandleMovement();
        HandleDamageTick();
    }

    /// <summary>
    /// 목표 거리까지 직선 전진 — 도달 시 정지하여 그 자리에 머무름.
    /// 마지막 프레임은 step을 잘라서 거리 초과 없이 정확히 멈춤.
    /// </summary>
    private void HandleMovement()
    {
        if (!isMoving) return;

        float step = moveSpeed * Time.deltaTime;

        // 이번 프레임 이동이 목표 거리 초과 시 step을 잘라서 정확히 멈춤
        if (traveledDistance + step >= maxTravelDistance)
        {
            step = maxTravelDistance - traveledDistance;
            isMoving = false;
        }

        transform.position += moveDirection * step;
        traveledDistance += step;
    }

    /// <summary>
    /// 지속 데미지 처리 — damageInterval마다 범위 내 적 전부에게 피해.
    /// 이동 중에도 작동 (지속 딜 컨셉).
    /// </summary>
    private void HandleDamageTick()
    {
        damageTimer += Time.deltaTime;
        if (damageTimer < damageInterval) return;

        damageTimer = 0f;
        ApplyDamageToEnemiesInRange();
    }

    /// <summary>
    /// OverlapSphere로 범위 내 적 검색 → IDamageable에게 데미지 적용.
    /// pullEnemiesToCenter가 켜져 있으면 중심 끌어당김도 함께 처리.
    /// </summary>
    private void ApplyDamageToEnemiesInRange()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, damageRadius, enemyLayer);
        if (hits == null || hits.Length == 0) return;

        foreach (var col in hits)
        {
            // 데미지 적용
            IDamageable damageable = col.GetComponent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                damageable.ApplySkillDamage(damage, hitEffectType);
            }

            // 확장: 토네이도 중심으로 끌어당김
            if (pullEnemiesToCenter)
            {
                PullEnemyToCenter(col.transform);
            }
        }
    }

    /// <summary>
    /// 적을 토네이도 중심 방향으로 부드럽게 끌어당김.
    /// Lerp 기반 — pullStrength가 클수록 빠르게 빨려들어옴.
    /// 수평 평면에서만 이동 (Y축 보존).
    /// </summary>
    private void PullEnemyToCenter(Transform enemyTransform)
    {
        Vector3 targetPos = transform.position;
        targetPos.y = enemyTransform.position.y; // 적의 Y 높이 유지

        enemyTransform.position = Vector3.Lerp(
            enemyTransform.position,
            targetPos,
            pullStrength * Time.deltaTime * 5f
        );
    }

    // ========================================================================
    // 디버그
    // ========================================================================

    /// <summary>씬 뷰에서 토네이도 데미지 범위 시각화.</summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, damageRadius);
    }
}
