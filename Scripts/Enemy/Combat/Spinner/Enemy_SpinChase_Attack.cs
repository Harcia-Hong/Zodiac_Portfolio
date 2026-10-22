// Enemy_SpinChase_Attack.cs — 회전 추격형 공격 (Tentacle / Totem 형 적용)

using System.Collections;
using UnityEngine;

/// <summary>
/// 회전하면서 일정 시간 동안 플레이어를 천천히 추격하며 광역 데미지를 주는 공격
///
/// [패턴]
/// - StartAttack 호출 → 스핀 애니메이션 진입 (루프)
/// - spinDuration 동안 NavMesh 속도를 줄여서 느리게 추격
/// - damageInterval 주기로 본체 주변 hitRadius 안의 플레이어에게 데미지
/// - 종료 후 NavMesh 속도 원복 → FSM이 attackCooldown만큼 대기 후 다음 공격
///
/// [밸런스 의도]
/// - 빠른 이동은 위협적이지만 회피 불가가 되기 쉬움
/// - 본 클래스는 "느리지만 끈질긴 압박"으로 설계됨
/// - 플레이어가 거리를 벌릴 수는 있지만, 가까이서 우물쭈물하면 도트로 갈림
///
/// [의존성]
/// - EnemyAttackBase 상속 (player/nav/anim/enemy 자동 캐싱)
/// - PlayerHealth.TakeDamage(int) 호출
/// </summary>
public class Enemy_SpinChase_Attack : EnemyAttackBase
{
    // =========================================================
    // 공격 설정
    // =========================================================

    [Header("공격 설정")]
    [SerializeField, Tooltip("공격 시작 사거리 (이 거리 안에서 StartAttack 호출됨)")]
    private float attackRange = 8f;

    [SerializeField, Tooltip("공격 쿨타임 (공격 종료 후 다음 공격까지 대기 시간)")]
    private float attackCooldown = 3f;

    // =========================================================
    // Spin Attack 세부
    // =========================================================

    [Header("Spin Attack 세부")]
    [SerializeField, Tooltip("스핀 지속 시간 (이 시간 동안 추격하며 데미지 줌)")]
    private float spinDuration = 4f;

    [SerializeField, Tooltip("스핀 중 추격 속도 (NavMeshAgent.speed 임시 변경값)")]
    private float spinChaseSpeed = 1.5f;

    [SerializeField, Tooltip("데미지 판정 범위 (본체 위치 기준 구체)")]
    private float hitRadius = 1.8f;

    [SerializeField, Tooltip("1회 데미지량 (도트성 광역)")]
    private int damagePerTick = 8;

    [SerializeField, Tooltip("데미지 판정 주기 (초). 너무 짧으면 즉사 위험")]
    private float damageInterval = 0.5f;

    [SerializeField, Tooltip("플레이어 경로 갱신 주기 (초). 짧을수록 정확한 추적, 길수록 가벼움")]
    private float repathInterval = 0.2f;

    [SerializeField, Tooltip("스핀 애니메이션 트리거 이름 (Animator와 일치 필수)")]
    private string spinAnimTrigger = "doSpinAttack";

    // =========================================================
    // 레이어
    // =========================================================

    [Header("레이어")]
    [SerializeField, Tooltip("플레이어 레이어 (히트 판정 대상)")]
    private LayerMask playerLayer;

    // =========================================================
    // 내부 상태
    // =========================================================

    /// <summary>스핀 시작 전 NavMesh 속도 (종료 시 원복용)</summary>
    private float originalNavSpeed = 0f;

    // =========================================================
    // IEnemyAttack — abstract 오버라이드
    // =========================================================

    /// <summary>
    /// 공격 시작 — FSM의 AttackState에서 호출
    /// 이미 공격 중이면 무시, 플레이어가 죽었으면 skip
    /// </summary>
    public override void StartAttack()
    {
        if (isAttacking || !gameObject.activeInHierarchy) return;
        if (!IsPlayerValid()) return;

        StartCoroutine(SpinAttackRoutine());
    }

    /// <summary>공격 시작 사거리 반환 (FSM 거리 판정용)</summary>
    public override float GetAttackRange() => attackRange;

    /// <summary>공격 쿨타임 반환 (FSM 다음 공격까지 대기 시간 계산용)</summary>
    public override float GetCooldownTime() => attackCooldown;

    /// <summary>
    /// 공격 중단 — 외부 요인(피격/사망/상태전환)으로 끊길 때 호출
    /// NavMesh 속도 원복 보장
    /// </summary>
    public override void StopAttack()
    {
        if (!isAttacking) return;

        StopAllCoroutines();
        isAttacking = false;

        // NavMesh 속도 원복 (Disable 안 했으니 RestoreNavMesh 대신 직접 처리)
        if (nav != null && nav.enabled && originalNavSpeed > 0f)
            nav.speed = originalNavSpeed;
    }

    // =========================================================
    // 공격 루틴
    // =========================================================

    /// <summary>
    /// Spin Attack 실행 코루틴
    /// 1. 스핀 애니메이션 트리거 + NavMesh 속도 변경 (느린 추격)
    /// 2. spinDuration 동안 루프:
    ///    - repathInterval 주기로 플레이어 위치로 경로 갱신
    ///    - damageInterval 주기로 본체 주변 데미지 판정
    /// 3. 종료 시 NavMesh 속도 원복
    /// </summary>
    private IEnumerator SpinAttackRoutine()
    {
        isAttacking = true;

        // 1. 스핀 애니메이션 진입
        if (anim != null)
            anim.SetTrigger(spinAnimTrigger);

        // 2. NavMesh 속도 변경 (느린 추격 모드)
        if (nav != null && nav.enabled)
        {
            originalNavSpeed = nav.speed;
            nav.speed = spinChaseSpeed;
        }

        // 3. 스핀 지속 루프
        float elapsed = 0f;
        float nextDamageTime = damageInterval; // 첫 데미지는 damageInterval 후
        float nextRepathTime = 0f;             // 첫 경로 갱신은 즉시

        while (elapsed < spinDuration)
        {
            // 3-1. 경로 갱신 (플레이어 추적)
            if (elapsed >= nextRepathTime && IsPlayerValid() && nav != null && nav.enabled)
            {
                nav.SetDestination(player.position);
                nextRepathTime = elapsed + repathInterval;
            }

            // 3-2. 데미지 판정 (주기적)
            if (elapsed >= nextDamageTime)
            {
                CheckSpinHit();
                nextDamageTime = elapsed + damageInterval;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 4. NavMesh 속도 원복
        if (nav != null && nav.enabled && originalNavSpeed > 0f)
            nav.speed = originalNavSpeed;

        isAttacking = false;
    }

    /// <summary>
    /// 본체 위치 기준 OverlapSphere로 플레이어 감지 → 데미지 적용
    /// 도트성 광역이라 hasHit 플래그 없음 (인터벌로 자연스럽게 분리됨)
    /// </summary>
    private void CheckSpinHit()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, hitRadius, playerLayer);

        foreach (var hit in hits)
        {
            PlayerHealth ph = hit.GetComponent<PlayerHealth>();
            if (ph != null && !ph.isDead)
            {
                ph.TakeDamage(damagePerTick);
                return; // 첫 히트 후 종료 (싱글 플레이어 가정)
            }
        }
    }

    // =========================================================
    // 디버그 (씬 뷰 시각화)
    // =========================================================

    /// <summary>
    /// 씬 뷰에서 사거리/판정 범위 시각화
    /// 노랑: 공격 시작 사거리 / 빨강: 히트 판정 범위
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 공격 시작 사거리
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // 히트 판정 범위
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, hitRadius);
    }
}
