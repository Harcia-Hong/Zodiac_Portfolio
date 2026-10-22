// Enemy_Charge_Roll_Attack.cs — 직선 돌진 공격 (Telegraph + Roll 애니 통굴림)

using System.Collections;
using UnityEngine;

/// <summary>
/// 직선 돌진 공격 컴포넌트 (Roll 애니메이션 활용)
///
/// [패턴]
/// - 사거리 안 들어오면 → 플레이어 조준
/// - Roll 애니 시작 (제자리에서 통굴림 — 준비 동작)
/// - Telegraph(Arrow) 표시로 회피 기회 제공
/// - Telegraph 끝나면 즉시 직선 돌진
/// - 돌진 중에도 Roll 계속 굴러감
/// - 도착 후 Roll 종료 + 후딜
///
/// [참고 게임]
/// - Kill Knight Pursuer, Windblown Worker Saw (Pizza Cutter)
///
/// [핵심 트릭]
/// - Roll 애니는 Telegraph 시작 시점부터 ON, 돌진 끝날 때 OFF
/// - 따라서 제자리에서 굴러가다가 빵 튀어나가는 임팩트 연출
///
/// [의존성]
/// - EnemyAttackBase (player/nav/anim 자동 캐싱)
/// - AttackTelegraph 컴포넌트 (Arrow 타입 사용)
/// - Animator에 isRolling Bool 파라미터 + Roll State 필요
/// </summary>
public class Enemy_Charge_Roll_Attack : EnemyAttackBase
{
    // =========================================================
    // 공격 설정
    // =========================================================

    [Header("공격 설정")]
    [SerializeField, Tooltip("공격 사거리 (이 거리 안에 들어오면 돌진 시작)")]
    private float attackRange = 8f;

    [SerializeField, Tooltip("공격 쿨타임 (다음 돌진까지 대기)")]
    private float attackCooldown = 3.5f;

    [SerializeField, Tooltip("돌진 데미지")]
    private int chargeDamage = 20;

    // =========================================================
    // Telegraph 설정 (준비 동작)
    // =========================================================

    [Header("Telegraph (준비 동작)")]
    [SerializeField, Tooltip("Telegraph 표시 시간 (이 동안 제자리에서 굴러감)")]
    private float telegraphDuration = 1.2f;

    [SerializeField, Tooltip("Telegraph 길이 = 돌진 거리와 동일")]
    private float telegraphLength = 8f;

    // =========================================================
    // 돌진 설정
    // =========================================================

    [Header("돌진")]
    [SerializeField, Tooltip("돌진 거리 (Telegraph 길이와 동일하게 권장)")]
    private float chargeDistance = 8f;

    [SerializeField, Tooltip("돌진하는 데 걸리는 시간 (짧을수록 빠르고 임팩트 큼)")]
    private float chargeDuration = 0.5f;

    [SerializeField, Tooltip("플레이어 명중 판정 반경 (돌진 중 이 반경 안에 들어오면 데미지)")]
    private float hitRadius = 1.5f;

    [SerializeField, Tooltip("후딜레이 (돌진 끝난 후 가만히 있는 시간)")]
    private float recoveryTime = 1.0f;

    // =========================================================
    // 애니메이션 파라미터
    // =========================================================

    [Header("애니메이션")]
    [SerializeField, Tooltip("Roll 애니메이션 Bool 파라미터 이름 (Animator와 일치 필수)")]
    private string rollAnimBool = "isRolling";

    // =========================================================
    // 컴포넌트 캐싱
    // =========================================================

    /// <summary>Telegraph 컴포넌트 — Awake에서 자동 캐싱</summary>
    private AttackTelegraph telegraph;

    // =========================================================
    // 내부 상태
    // =========================================================

    /// <summary>이번 돌진에서 이미 히트했는지 (다중 히트 방지)</summary>
    private bool hasHit = false;

    // =========================================================
    // 초기화
    // =========================================================

    /// <summary>
    /// 컴포넌트 캐싱
    /// base.Awake()에서 player, nav, anim, enemy 자동 캐싱됨
    /// 이 클래스에서는 AttackTelegraph만 추가로 캐싱
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        telegraph = GetComponent<AttackTelegraph>();

        if (telegraph == null)
            Debug.LogWarning($"[{name}] AttackTelegraph 컴포넌트가 없습니다. Telegraph 표시가 동작하지 않을 수 있습니다.");
    }

    // =========================================================
    // IEnemyAttack — abstract 오버라이드
    // =========================================================

    /// <summary>
    /// 공격 시작 — FSM에서 호출
    /// 사거리 검증 후 돌진 시퀀스 시작
    /// </summary>
    public override void StartAttack()
    {
        if (isAttacking || !gameObject.activeInHierarchy) return;
        if (!IsPlayerValid()) return;

        StartCoroutine(ChargeRollRoutine());
    }

    /// <summary>공격 사거리</summary>
    public override float GetAttackRange() => attackRange;

    /// <summary>공격 쿨타임</summary>
    public override float GetCooldownTime() => attackCooldown;

    /// <summary>
    /// 공격 강제 종료 — 베이스의 기본 동작 + Roll 애니 OFF 추가
    /// 피격이나 외부 요인으로 공격 끊길 때 안전하게 정리
    /// </summary>
    public override void StopAttack()
    {
        // Roll 애니메이션 OFF (베이스가 NavMesh 복구는 처리해줌)
        if (anim != null)
            anim.SetBool(rollAnimBool, false);

        base.StopAttack();
    }

    // =========================================================
    // 돌진 시퀀스 본체
    // =========================================================

    /// <summary>
    /// 돌진 공격 메인 루틴
    ///
    /// [3단계 구성]
    /// Phase 1: 준비 — 조준 + Roll 애니 ON + Telegraph 표시 (제자리 굴림)
    /// Phase 2: 돌진 — 직선 Lerp 이동 + 충돌 체크 (계속 굴림)
    /// Phase 3: 후딜 — Roll 애니 OFF + NavMesh 복구 + 후딜
    /// </summary>
    private IEnumerator ChargeRollRoutine()
    {
        isAttacking = true;
        hasHit = false;

        // ============= Phase 1: 준비 (제자리 통굴림 + Telegraph) =============

        // 1-1. 플레이어 조준 (방향 잠금)
        LookAtPlayer();

        // 1-2. NavMesh 끄기 (제자리 고정 + 추적 정지)
        DisableNavMesh();

        // 1-3. Roll 애니 시작 — 여기서부터 제자리에서 굴러감
        if (anim != null)
            anim.SetBool(rollAnimBool, true);

        // 1-4. Telegraph 표시 (Arrow 타입, 직선 방향)
        // ShowTelegraph는 즉시 반환되므로 별도로 텔레그래프 시간만큼 대기 필요
        if (telegraph != null)
        {
            telegraph.ShowArrow(
                transform.position,    // origin: 자기 위치에서 시작
                transform.forward,     // direction: 앞 방향
                telegraphLength        // length: Inspector에서 설정한 길이
            );
        }

        // 1-5. Telegraph 시간 동안 제자리에서 계속 굴러가며 대기
        yield return new WaitForSeconds(telegraphDuration);

        // 취소 체크 (StopAttack으로 끊겼을 경우)
        if (!isAttacking) yield break;

        // ============= Phase 2: 돌진 (Roll 굴림 + 직선 이동) =============

        // 2-1. 돌진 시작/끝 위치 계산 (텔레그래프 보여준 그 방향 그대로)
        Vector3 startPos = transform.position;
        Vector3 dir = transform.forward; // LookAtPlayer로 이미 플레이어 방향
        Vector3 targetPos = startPos + (dir * chargeDistance);

        // 2-2. Lerp로 직선 이동 + 매 프레임 충돌 체크
        float elapsed = 0f;
        while (elapsed < chargeDuration)
        {
            // 위치 보간 (정규화된 진행도 사용)
            float t = elapsed / chargeDuration;
            transform.position = Vector3.Lerp(startPos, targetPos, t);

            // 충돌 체크 (1회만 데미지)
            if (!hasHit)
                CheckChargeHit();

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 2-3. 마지막 위치 확정 + 마지막 충돌 체크
        transform.position = targetPos;
        if (!hasHit)
            CheckChargeHit();

        // ============= Phase 3: 후딜 (정리 + 회복 시간) =============

        // 3-1. Roll 애니 종료
        if (anim != null)
            anim.SetBool(rollAnimBool, false);

        // 3-2. NavMesh 복구 (Warp 포함)
        RestoreNavMesh();

        // 3-3. 후딜레이 (회피 보상 + 다음 행동까지의 텀)
        yield return new WaitForSeconds(recoveryTime);

        isAttacking = false;
    }

    /// <summary>
    /// 돌진 중 플레이어 충돌 판정
    /// 자기 위치 기준 OverlapSphere로 플레이어 감지 → 데미지 적용
    /// hasHit 플래그로 한 돌진당 1회만 데미지
    /// </summary>
    private void CheckChargeHit()
    {
        if (player == null) return;

        // XZ 평면에서의 거리 (높이 무시)
        float dist = GetDistanceToPlayer();

        if (dist <= hitRadius)
        {
            PlayerHealth ph = player.GetComponent<PlayerHealth>();
            if (ph != null && !ph.isDead)
            {
                hasHit = true;
                ph.TakeDamage(chargeDamage);
            }
        }
    }

    // =========================================================
    // 디버그 (씬 뷰 시각화)
    // =========================================================

    /// <summary>
    /// 씬 뷰에서 사거리 / 충돌 반경 / 돌진 경로 시각화
    /// 빨강: 공격 사거리 / 파랑: 돌진 경로 / 주황: 충돌 반경
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 공격 사거리
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // 돌진 경로 (앞 방향)
        Gizmos.color = Color.blue;
        Vector3 chargeEnd = transform.position + transform.forward * chargeDistance;
        Gizmos.DrawLine(transform.position, chargeEnd);
        Gizmos.DrawWireSphere(chargeEnd, 0.3f);

        // 충돌 반경
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f); // 주황
        Gizmos.DrawWireSphere(transform.position, hitRadius);
    }
}
