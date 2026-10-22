// Enemy_Spider_Attack.cs — 거미형 적의 하이브리드 공격 (근거리 Stab + 원거리 직선 투사체)

using System.Collections;
using UnityEngine;

/// <summary>
/// 거미형 적의 하이브리드 공격 컴포넌트
///
/// [패턴]
/// - 가까우면 근거리 Stab Attack (찌르기)
/// - 멀면 원거리 Projectile Attack (직선 투사체 — Kill Knight, Windblown 스타일)
/// - 거리 전환 기준: EnemySO.hybridSwitchDistance (없으면 fallbackSwitchDistance)
///
/// [의존성]
/// - EnemyAttackBase 상속 (player/nav/anim/enemy 자동 캐싱, NavMesh 제어, LookAtPlayer 등)
/// - 적이 발사하는 직선 투사체 → Projectile 컴포넌트(Tag 기반) 사용
///   (LinearProjectile은 IDamageable 기반이라 플레이어 → 적 용도임)
/// </summary>
public class Enemy_Spider_Hybrid_Attack : EnemyAttackBase
{
    // =========================================================
    // 공통 설정
    // =========================================================

    [Header("공통 설정")]
    [SerializeField, Tooltip("근거리 공격 사거리 (이 거리 안에서 Stab)")]
    private float meleeRange = 3f;

    [SerializeField, Tooltip("원거리 공격 사거리 (이 거리 안에서 투사체 발사)")]
    private float rangedRange = 12f;

    [SerializeField, Tooltip("EnemySO.hybridSwitchDistance가 없을 때 사용할 기본 전환 거리")]
    private float fallbackSwitchDistance = 5f;

    [SerializeField, Tooltip("공격 쿨타임 (다음 공격까지의 대기 시간)")]
    private float attackCooldown = 2.0f;

    // =========================================================
    // 근거리 — Stab Attack
    // =========================================================

    [Header("근거리 — Stab Attack")]
    [SerializeField, Tooltip("근거리 공격 데미지")]
    private int meleeDamage = 15;

    [SerializeField, Tooltip("근거리 데미지 판정 범위 (자기 위치 기준 구체)")]
    private float meleeHitRadius = 1.5f;

    [SerializeField, Tooltip("근거리 애니메이션 트리거 이름 (Animator에서 동일하게 설정)")]
    private string meleeAnimTrigger = "doStabAttack";

    [SerializeField, Tooltip("애니메이션 시작 후 데미지 판정 시작 시점 (초)")]
    private float meleeDamageStart = 0.25f;

    [SerializeField, Tooltip("데미지 판정 종료 시점 (초)")]
    private float meleeDamageEnd = 0.5f;

    [SerializeField, Tooltip("근거리 애니메이션 전체 길이 (초)")]
    private float meleeAnimLength = 0.8f;

    // =========================================================
    // 원거리 — 직선 투사체
    // =========================================================

    [Header("원거리 — 직선 투사체")]
    [SerializeField, Tooltip("원거리 공격 데미지")]
    private int rangedDamage = 12;

    [SerializeField, Tooltip("투사체 프리팹 (Projectile 컴포넌트 필수)")]
    private GameObject projectilePrefab;

    [SerializeField, Tooltip("발사 위치 (없으면 자기 위치에서 발사)")]
    private Transform firePoint;

    [SerializeField, Tooltip("투사체 이동 속도")]
    private float projectileSpeed = 12f;

    [SerializeField, Tooltip("투사체 수명 (자동 파괴까지의 시간)")]
    private float projectileLifetime = 4f;

    [SerializeField, Tooltip("원거리 애니메이션 트리거 이름")]
    private string rangedAnimTrigger = "doProjectileAttack";

    [SerializeField, Tooltip("애니메이션 시작 후 실제 발사 타이밍 (초)")]
    private float rangedFireDelay = 0.4f;

    [SerializeField, Tooltip("발사 후 후딜레이 (초)")]
    private float rangedRecovery = 0.5f;

    // =========================================================
    // 레이어
    // =========================================================

    [Header("레이어")]
    [SerializeField, Tooltip("플레이어 레이어 (근거리 히트 판정 대상)")]
    private LayerMask playerLayer;

    // =========================================================
    // 내부 상태
    // =========================================================

    /// <summary>현재 공격이 원거리 모드인지 (StartAttack 호출 시점에 결정)</summary>
    private bool isRangedMode = false;

    /// <summary>이번 근거리 공격에서 이미 히트했는지 (다중 히트 방지)</summary>
    private bool hasHit = false;

    /// <summary>EnemySO 캐시 — hybridSwitchDistance 접근용</summary>
    private EnemySO data;

    // =========================================================
    // 초기화
    // =========================================================

    /// <summary>
    /// 컴포넌트 캐싱
    /// base.Awake()에서 player, nav, anim, enemy가 자동으로 캐싱됨
    /// 이 클래스에서는 firePoint 기본값만 추가 처리
    /// </summary>
    protected override void Awake()
    {
        base.Awake(); // EnemyAttackBase의 자동 캐싱 실행 (반드시 호출)

        // firePoint 미설정 시 자기 위치에서 발사
        if (firePoint == null)
            firePoint = transform;
    }

    /// <summary>
    /// EnemySO 캐싱 (Enemy.Data가 Awake 이후 설정될 수 있어 Start에서 수행)
    /// </summary>
    private void Start()
    {
        if (enemy != null && enemy.Data != null)
            data = enemy.Data;
    }

    // =========================================================
    // IEnemyAttack — abstract 오버라이드
    // =========================================================

    /// <summary>
    /// 공격 시작 — 거리에 따라 근거리/원거리 자동 선택
    /// FSM의 AttackState에서 호출
    /// </summary>
    public override void StartAttack()
    {
        // 이미 공격 중이거나 비활성 상태면 무시
        if (isAttacking || !gameObject.activeInHierarchy) return;

        // 플레이어 유효성 확인 (죽은 플레이어한테 헛스윙 방지)
        if (!IsPlayerValid()) return;

        // 거리 기반으로 공격 방식 결정
        isRangedMode = ShouldUseRangedAttack();

        if (isRangedMode)
            StartCoroutine(RangedAttackRoutine());
        else
            StartCoroutine(MeleeAttackRoutine());
    }

    /// <summary>
    /// 현재 공격 방식의 사거리 반환
    /// FSM이 사거리 안에 있는지 판단할 때 매번 호출됨
    /// </summary>
    public override float GetAttackRange()
    {
        // 매 호출 시점의 거리로 판단해 더 정확한 모드 선택
        return ShouldUseRangedAttack() ? rangedRange : meleeRange;
    }

    /// <summary>공격 쿨타임 반환</summary>
    public override float GetCooldownTime() => attackCooldown;

    // =========================================================
    // 거리 판단
    // =========================================================

    /// <summary>
    /// 플레이어와의 거리 기반으로 원거리 공격 사용 여부 결정
    /// 전환 거리는 EnemySO에서 가져오되, 없으면 fallbackSwitchDistance 사용
    /// </summary>
    private bool ShouldUseRangedAttack()
    {
        if (player == null) return false;

        float distance = GetDistanceToPlayer();
        float switchDist = (data != null) ? data.hybridSwitchDistance : fallbackSwitchDistance;

        return distance > switchDist;
    }

    // =========================================================
    // 근거리 — Stab Attack 루틴
    // =========================================================

    /// <summary>
    /// 근거리 찌르기 공격 루틴
    /// 1. 플레이어 조준 + NavMesh 끄기 (미끄러짐 방지)
    /// 2. Stab 애니메이션 트리거
    /// 3. 데미지 윈도우 동안 매 프레임 히트 체크
    /// 4. 애니 종료 후 NavMesh 복구
    /// </summary>
    private IEnumerator MeleeAttackRoutine()
    {
        isAttacking = true;
        hasHit = false;

        // 1. 플레이어 조준 + NavMesh 끄기
        LookAtPlayer();
        DisableNavMesh();

        // 2. Stab 애니메이션 재생
        if (anim != null)
            anim.SetTrigger(meleeAnimTrigger);

        // 3. 데미지 판정 시작 시점까지 대기
        yield return new WaitForSeconds(meleeDamageStart);

        // 4. 데미지 윈도우 동안 매 프레임 히트 체크 (다중 히트는 hasHit으로 차단)
        float elapsed = 0f;
        float window = meleeDamageEnd - meleeDamageStart;

        while (elapsed < window)
        {
            if (!hasHit)
                CheckMeleeHit();

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 5. 애니메이션 잔여 시간 대기 (모션 자연스럽게 마무리)
        float remaining = meleeAnimLength - meleeDamageEnd;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        // 6. NavMesh 복구 + 종료
        RestoreNavMesh();
        isAttacking = false;
    }

    /// <summary>
    /// 자기 위치 기준 OverlapSphere로 플레이어 감지 → 데미지 적용
    /// hasHit 플래그로 한 공격당 1회만 데미지 들어가도록 보장
    /// </summary>
    private void CheckMeleeHit()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, meleeHitRadius, playerLayer);

        foreach (var hit in hits)
        {
            PlayerHealth ph = hit.GetComponent<PlayerHealth>();
            if (ph != null && !ph.isDead)
            {
                hasHit = true;
                ph.TakeDamage(meleeDamage);
                return; // 첫 히트 후 즉시 종료
            }
        }
    }

    // =========================================================
    // 원거리 — 직선 투사체 루틴
    // =========================================================

    /// <summary>
    /// 원거리 직선 투사체 공격 루틴
    /// 1. 플레이어 조준 + 발사 방향 잠금 (조준 후 플레이어가 움직여도 방향 안 바뀜)
    /// 2. Projectile 애니메이션 트리거
    /// 3. fireDelay 후 투사체 생성 + 발사
    /// 4. 후딜레이 후 종료
    /// </summary>
    private IEnumerator RangedAttackRoutine()
    {
        isAttacking = true;

        // 1. 플레이어 조준 + 발사 방향 미리 기록 (조준 잠금 효과)
        LookAtPlayer();
        Vector3 fireDirection = GetXZDirectionToPlayer();

        // 2. Projectile 애니메이션 재생
        if (anim != null)
            anim.SetTrigger(rangedAnimTrigger);

        // 3. 실제 발사 타이밍까지 대기
        yield return new WaitForSeconds(rangedFireDelay);

        // 취소 체크 (StopAttack 호출 시 isAttacking이 false로 바뀜)
        if (!isAttacking) yield break;

        // 4. 투사체 생성 + 발사
        FireLinearProjectile(fireDirection);

        // 5. 후딜레이 후 종료
        yield return new WaitForSeconds(rangedRecovery);

        isAttacking = false;
    }

    /// <summary>
    /// 직선 투사체 생성 + 지정 방향으로 발사
    /// </summary>
    /// <param name="direction">XZ 평면 정규화 방향 벡터</param>
    private void FireLinearProjectile(Vector3 direction)
    {
        if (projectilePrefab == null)
        {
            Debug.LogError($"[{name}] Spider Attack: projectilePrefab이 설정되지 않았습니다!");
            return;
        }

        // 발사 방향으로 회전된 투사체 생성
        Quaternion rotation = Quaternion.LookRotation(direction);
        GameObject projObj = Instantiate(projectilePrefab, firePoint.position, rotation);

        // Projectile 컴포넌트 초기화
        Projectile proj = projObj.GetComponent<Projectile>();
        if (proj == null)
        {
            Debug.LogError($"[{name}] 투사체 프리팹에 Projectile 컴포넌트가 없습니다!");
            Destroy(projObj);
            return;
        }

        proj.Initialize(direction, projectileSpeed, rangedDamage, projectileLifetime);
    }

    /// <summary>
    /// 자기 위치 → 플레이어 위치 방향 벡터 (XZ 평면, 정규화)
    /// 높이 차이 무시하므로 점프 중인 플레이어에게도 정확히 발사 가능
    /// </summary>
    private Vector3 GetXZDirectionToPlayer()
    {
        if (player == null) return transform.forward;

        Vector3 dir = player.position - transform.position;
        dir.y = 0f;

        return dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
    }

    // =========================================================
    // 디버그 (씬 뷰 시각화)
    // =========================================================

    /// <summary>
    /// 씬 뷰에서 사거리/판정 범위 시각화
    /// 빨강: 근거리 사거리 / 청록: 원거리 사거리 / 노랑: 모드 전환 거리 / 주황: 근거리 히트 범위
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 근거리 사거리
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeRange);

        // 원거리 사거리
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, rangedRange);

        // 모드 전환 거리
        Gizmos.color = Color.yellow;
        float switchDist = (data != null) ? data.hybridSwitchDistance : fallbackSwitchDistance;
        Gizmos.DrawWireSphere(transform.position, switchDist);

        // 근거리 히트 판정 범위
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f); // 주황
        Gizmos.DrawWireSphere(transform.position, meleeHitRadius);
    }
}
