using UnityEngine;

/// <summary>
/// 폭발 화살 — 마우스 방향으로 직선 이동하다 적/벽 충돌 시 광역 폭발.
/// 
/// 동작 흐름:
///   1. Initialize()로 방향/속도/사거리/데미지/레이어 주입받음
///   2. SphereCast로 매 프레임 이동 경로 검사 (빠른 발사체도 안 놓침)
///   3. 적 직격 → 폭발 (직격 대상은 1.5배 강제 크리)
///   4. 벽 충돌 → 폭발 (광역만)
///   5. 사거리 도달 → 그냥 소멸 (폭발 X)
/// 
/// 폭발 = 광역 데미지 + 스턴 디버프 + (옵션) 폭발 VFX.
/// 
/// 확장 옵션(explosionRadius, stunDuration 등)은 프리팹 자체의 인스펙터 필드.
/// 불씨 시스템에서 이 수치들을 모디파이어로 조절할 수 있도록 향후 확장 가능.
/// </summary>
public class ExplosiveArrow : ProjectileBase
{
    // ========================================================================
    // 인스펙터 옵션 (프리팹 자체에 설정)
    // ========================================================================

    [Header("폭발 옵션")]
    [Tooltip("폭발 광역 반경 (유닛).")]
    [SerializeField] private float explosionRadius = 2f;

    [Tooltip("폭발에 휘말린 적에게 적용할 스턴 지속 시간 (초).")]
    [SerializeField] private float stunDuration = 0.5f;

    [Tooltip("폭발 시 인스턴스화할 VFX 프리팹. 비워두면 폭발 VFX 없음.")]
    [SerializeField] private GameObject explosionVFX;

    [Header("이동/충돌 옵션")]
    [Tooltip("화살의 SphereCast 반경 (충돌 검사용). 0.3 정도가 적당.")]
    [SerializeField] private float castRadius = 0.3f;

    [Tooltip("폭발/소멸 후 GameObject가 자동 파괴되기까지 딜레이.\n잔여 트레일 파티클이 자연 소멸하도록 약간 두는 게 자연스러움.")]
    [SerializeField] private float destroyDelay = 0.15f;

    [Header("SFX")]
    [Tooltip("폭발 순간 재생할 효과음 (펑)")]
    [SerializeField] private AudioClip explosionSfx;

    [Tooltip("적이 폭발 데미지를 입을 때 재생할 효과음")]
    [SerializeField] private AudioClip hitSfx;

    // ========================================================================
    // 불씨 효과 (ApplyEmberModifiers에서 설정)
    // ========================================================================

    private bool hasDistanceScaling = false;
    private float distanceScalingMaxBonus = 0f;

    // ========================================================================
    // 런타임 설정값 (Initialize로 주입)
    // ========================================================================

    private Vector3 moveDirection;
    private float moveSpeed;
    private float maxRange;
    private int baseDamage;
    private LayerMask enemyLayer;
    private LayerMask obstacleLayer;
    private HitEffectType hitEffectType;

    // ========================================================================
    // 내부 상태
    // ========================================================================

    private float traveledDistance = 0f;
    private bool isFinished = false;  // 폭발 또는 소멸로 활동 종료
    private bool isInitialized = false;

    // ========================================================================
    // 초기화 (ProjectileBase 구현)
    // ========================================================================

    /// <summary>
    /// 발사체 공통 초기화 — LaunchAction에서 호출.
    /// 폭발 옵션(explosionRadius, stunDuration 등)은 프리팹 인스펙터에서 미리 설정됨.
    /// </summary>
    public override void Initialize(
        Vector3 direction,
        float speed,
        float range,
        int damage,
        LayerMask eLayer,
        LayerMask oLayer,
        HitEffectType effect)
    {
        moveDirection = direction;
        moveDirection.y = 0f;
        if (moveDirection.sqrMagnitude < 0.0001f) moveDirection = transform.forward;
        moveDirection.Normalize();

        moveSpeed = speed;
        maxRange = range;
        baseDamage = damage;
        enemyLayer = eLayer;
        obstacleLayer = oLayer;
        hitEffectType = effect;

        // 화살이 진행 방향을 보도록 회전 — 트레일 VFX도 자연스럽게 따라옴
        transform.rotation = Quaternion.LookRotation(moveDirection);

        isInitialized = true;
    }

    /// <summary>
    /// 슬롯에 부여된 불씨 효과 적용.
    /// - RangeMultiplier: 폭발 반경 +N%
    /// - DistanceScaling: 비행 거리에 따라 데미지 누적 증가, 최대 N%까지
    /// </summary>
    public override void ApplyEmberModifiers(SkillSlot slot)
    {
        if (slot == null) return;

        // 폭발 반경 확장
        float rangeBonus = slot.GetEmberModifierSum(EmberEffectType.RangeMultiplier);
        if (rangeBonus > 0f)
        {
            explosionRadius *= 1f + rangeBonus / 100f;
        }

        // 비행 거리 스케일링 효과
        if (slot.HasEmberEffect(EmberEffectType.DistanceScaling))
        {
            hasDistanceScaling = true;
            distanceScalingMaxBonus = slot.GetEmberModifierSum(EmberEffectType.DistanceScaling);
        }
    }

    // ========================================================================
    // 메인 루프
    // ========================================================================

    private void Update()
    {
        if (!isInitialized || isFinished) return;

        float step = moveSpeed * Time.deltaTime;
        int combinedMask = enemyLayer | obstacleLayer;

        // 이동 경로 SphereCast — 적/벽 둘 다 검사
        if (Physics.SphereCast(transform.position, castRadius, moveDirection, out RaycastHit hit, step, combinedMask))
        {
            // 충돌 지점으로 이동
            transform.position = hit.point;

            // 적인지 벽인지 판정
            bool isEnemyHit = ((1 << hit.collider.gameObject.layer) & enemyLayer.value) != 0;
            Explode(isEnemyHit ? hit.collider : null);
            return;
        }

        // 충돌 없으면 직진
        transform.position += moveDirection * step;
        traveledDistance += step;

        // 사거리 도달 → 폭발 없이 그냥 소멸
        if (traveledDistance >= maxRange)
        {
            FadeOutAndDestroy();
        }
    }

    /// <summary>
    /// 폭발 처리 — 광역 데미지 + 스턴 + 폭발 VFX + 자체 파괴.
    /// directTarget이 null이 아니면 그 대상에 1.5배 강제 크리.
    /// </summary>
    private void Explode(Collider directTarget)
    {
        if (isFinished) return;
        isFinished = true;

        // [NEW] 비행 거리 스케일링 적용된 실효 데미지 계산
        int finalBaseDamage = baseDamage;
        if (hasDistanceScaling && maxRange > 0f)
        {
            // 진행도 = 비행 거리 / 최대 사거리 (0 ~ 1)
            float progress = Mathf.Clamp01(traveledDistance / maxRange);
            float bonusPercent = distanceScalingMaxBonus * progress;
            finalBaseDamage = Mathf.RoundToInt(baseDamage * (1f + bonusPercent / 100f));

            Debug.Log($"[ExplosiveArrow] 거리 스케일링 — 진행도 {progress:F2}, 보너스 +{bonusPercent:F0}%, 최종 {finalBaseDamage}");
        }

        // 광역 범위 내 적 일괄 처리
        // 광역 범위 내 적 일괄 처리
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, enemyLayer);
        bool anyEnemyHit = false;   // 데미지 SFX 1회 재생용

        foreach (var col in hits)
        {
            // 데미지 — 직격 대상은 1.5배 강제 크리
            IDamageable damageable = col.GetComponent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                bool isCritical = (directTarget != null && col == directTarget);
                int dmg = isCritical ? Mathf.RoundToInt(finalBaseDamage * 1.5f) : finalBaseDamage;

                damageable.ApplySkillDamage(dmg, hitEffectType);
                anyEnemyHit = true;   // 적어도 한 명 맞음

                if (isCritical)
                {
                    Debug.Log($"<color=orange><b>[직격 크리!]</b></color> {col.name} → {dmg}");
                }
            }

            // 스턴 적용 (Enemy 전용)
            Enemy enemy = col.GetComponent<Enemy>();
            if (enemy != null && !enemy.isDead)
            {
                enemy.ApplyStunDebuff(stunDuration);
            }
        }

        // 폭발 VFX
        if (explosionVFX != null)
        {
            Instantiate(explosionVFX, transform.position, Quaternion.identity);
        }

        // 폭발 SFX (펑 — 항상 재생)
        if (explosionSfx != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(explosionSfx);
        }

        // 데미지 SFX (적 맞았을 때만 1회 — 다수 적이어도 한 번)
        if (anyEnemyHit && hitSfx != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(hitSfx);
        }

        moveSpeed = 0f;
        Destroy(gameObject, destroyDelay);
    }

    /// <summary>
    /// 사거리 도달 시 폭발 없이 그냥 소멸.
    /// 적/벽에 안 닿고 끝까지 날아간 경우 — 데미지/VFX 없음.
    /// </summary>
    private void FadeOutAndDestroy()
    {
        if (isFinished) return;
        isFinished = true;

        // 그냥 소멸 — 잔여 트레일 자연 소멸 위해 약간 대기
        moveSpeed = 0f;
        Destroy(gameObject, destroyDelay);
    }

    // ========================================================================
    // 디버그
    // ========================================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
