using DG.Tweening;
using UnityEngine;
using UnityEngine.AI;

/// ============================================================
/// EnemyChasingState.cs — Enemy 추적 상태 (Layout 간 점프 추격 포함)
/// ============================================================
/// 플레이어를 NavMesh로 추적하되, Layout 사이 gap으로 경로가 끊기면
/// EnemySO.canJumpGap 설정에 따라 DOJump로 점프 추격을 시도한다.
///
/// 핵심 흐름:
///   1. NavMeshAgent.SetDestination으로 경로 추적
///   2. 경로 계산 완료 후 pathStatus 확인
///      - PathComplete: 정상 추격 (NavMesh 이동)
///      - PathPartial/PathInvalid: 경로 끊김 → TryGapJump()
///   3. canJumpGap = true:
///      NavMeshAgent 비활성 → DOJump → 착지 후 NavMesh 복귀
///   4. canJumpGap = false:
///      제자리 대기 (원거리 타입은 AttackState에서 공격)
///
/// 점프 중 안전장치:
///   - isJumping = true 동안 모든 상태 전환/이동 로직 스킵
///   - Exit 시 DOKill로 강제 종료 + 복구
/// ============================================================
public class EnemyChasingState : EnemyBaseState
{
    // ============================================================
    // 상수
    // ============================================================

    /// <summary>NavMeshAgent 목적지 갱신 간격 (초)</summary>
    private const float TARGET_UPDATE_INTERVAL = 0.2f;

    /// <summary>점프 착지 후 NavMesh 위치 보정 탐색 반경</summary>
    private const float NAVMESH_SAMPLE_RADIUS = 5f;

    /// <summary>경로 실패 누적 횟수가 이 값 이상이면 점프 시도 (일시적 경로 실패 무시)</summary>
    private const int PATH_FAIL_THRESHOLD = 2;

    // ============================================================
    // 런타임 상태
    // ============================================================

    /// <summary>마지막 목적지 갱신 시각</summary>
    private float lastTargetUpdateTime;

    /// <summary>점프 중 여부 (true 동안 모든 상태 전환 로직 무시)</summary>
    private bool isJumping = false;

    /// <summary>마지막 점프 시각 (쿨다운 기준)</summary>
    private float lastJumpTime = -999f;

    /// <summary>연속 경로 실패 카운트 (일시적 실패 무시용)</summary>
    private int consecutivePathFails = 0;

    // ============================================================
    // 생성자
    // ============================================================

    public EnemyChasingState(EnemyStateMachine stateMachine) : base(stateMachine)
    {
    }

    // ============================================================
    // State Lifecycle
    // ============================================================

    public override void Enter()
    {
        // 첫 조우 시 후퇴 판정
        if (stateMachine.IsFirstEncounter)
        {
            bool canRetreat = stateMachine.Enemy.Data != null
                              && stateMachine.Enemy.Data.canRetreatOnFirstEncounter;

            if (canRetreat && Random.value < 0.5f)
            {
                stateMachine.ChangeState(stateMachine.RetreatState);
                return;
            }

            stateMachine.IsFirstEncounter = false;
        }

        base.Enter();

        stateMachine.MovementSpeedModifier = 1f;
        isJumping = false;
        consecutivePathFails = 0;

        if (stateMachine.Enemy.AnimationData != null)
            StartAnimation(stateMachine.Enemy.AnimationData.WalkParameterHash);

        lastTargetUpdateTime = 0f;
    }

    public override void Exit()
    {
        base.Exit();

        if (stateMachine.Enemy.AnimationData != null)
            StopAnimation(stateMachine.Enemy.AnimationData.WalkParameterHash);

        // 점프 중이었다면 강제 종료 + 상태 복구
        if (isJumping)
        {
            ForceEndJump();
        }

        NavMeshAgent nav = stateMachine.Enemy.nav;
        if (nav != null && nav.enabled && nav.isOnNavMesh)
            nav.ResetPath();
    }

    public override void Update()
    {
        // 점프 중에는 모든 로직 스킵 (착지 콜백에서 복구됨)
        if (isJumping) return;

        // 타겟 무효 시 Idle
        if (!stateMachine.IsTargetValid())
        {
            stateMachine.ChangeState(stateMachine.IdleState);
            return;
        }

        // 공격 범위 내 + 쿨타임 아닐 때 공격
        if (stateMachine.IsPlayerInAttackRange() && !stateMachine.IsAttackOnCooldown())
        {
            stateMachine.ChangeState(stateMachine.AttackState);
            return;
        }

        // 타겟 위치 갱신
        if (Time.time - lastTargetUpdateTime >= TARGET_UPDATE_INTERVAL)
        {
            UpdateTargetTracking();
            lastTargetUpdateTime = Time.time;
        }

        RotateTowardsTarget();
    }

    // ============================================================
    // NavMesh 추적 + 경로 실패 감지
    // ============================================================

    #region Target Tracking

    /// <summary>
    /// NavMeshAgent 목적지 갱신 + 경로 상태 확인
    /// - 경로 정상(PathComplete): NavMesh 이동 유지
    /// - 경로 끊김(Partial/Invalid): TryGapJump() 시도
    /// </summary>
    private void UpdateTargetTracking()
    {
        if (!stateMachine.IsTargetValid()) return;

        NavMeshAgent nav = stateMachine.Enemy.nav;
        if (nav == null || !nav.enabled || !nav.isOnNavMesh) return;

        // 이동 속도 적용
        float finalSpeed = stateMachine.MovementSpeed * stateMachine.MovementSpeedModifier;
        nav.speed = finalSpeed;

        // 목적지 설정
        nav.SetDestination(stateMachine.Target.position);

        // 경로 계산 중이면 아직 판단하지 않음
        if (nav.pathPending) return;

        // 경로 상태 확인
        if (nav.pathStatus == NavMeshPathStatus.PathComplete)
        {
            // 정상 경로 — 실패 카운트 초기화
            consecutivePathFails = 0;
        }
        else
        {
            // 경로 끊김 (Partial 또는 Invalid)
            consecutivePathFails++;

            // 일시적 실패 무시 (NavMesh 리베이크 직후 등)
            if (consecutivePathFails >= PATH_FAIL_THRESHOLD)
            {
                TryGapJump();
            }
        }
    }

    #endregion

    // ============================================================
    // Gap 점프 로직
    // ============================================================

    #region Gap Jump

    /// <summary>
    /// Layout 간 gap 점프 시도
    /// - canJumpGap = false면 무시 (원거리 타입은 제자리 공격)
    /// - 쿨다운 중이면 무시
    /// - 플레이어 위치 근처의 NavMesh 포인트를 착지 목표로 설정
    /// - NavMeshAgent 비활성 → DOJump → 착지 후 NavMesh 복귀
    /// </summary>
    private void TryGapJump()
    {
        EnemySO data = stateMachine.Enemy.Data;
        if (data == null || !data.canJumpGap) return;

        // 쿨다운 체크
        if (Time.time < lastJumpTime + data.gapJumpCooldown) return;

        // 착지 목표 = 플레이어 위치 근처의 NavMesh 위 포인트
        Vector3 targetPos = stateMachine.Target.position;
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(targetPos, out hit, NAVMESH_SAMPLE_RADIUS, NavMesh.AllAreas))
        {
            Debug.Log("[EnemyChasingState] 착지 가능한 NavMesh 포인트를 찾지 못함 → 점프 취소");
            return;
        }

        // 점프 실행
        ExecuteGapJump(hit.position);
    }

    /// <summary>
    /// DOJump로 포물선 점프 실행
    /// 1. NavMeshAgent 비활성 (DOJump와 충돌 방지)
    /// 2. Rigidbody isKinematic = true (물리 간섭 방지)
    /// 3. DOJump 실행
    /// 4. 착지 시 NavMesh 위치 보정 → NavMeshAgent 복귀
    /// </summary>
    private void ExecuteGapJump(Vector3 landingTarget)
    {
        EnemySO data = stateMachine.Enemy.Data;
        Enemy enemy = stateMachine.Enemy;
        NavMeshAgent nav = enemy.nav;
        Rigidbody rb = enemy.rigid;
        Transform enemyTransform = enemy.transform;

        // 상태 설정
        isJumping = true;
        lastJumpTime = Time.time;
        consecutivePathFails = 0;

        // NavMeshAgent 비활성 (DOJump와 위치 제어 충돌 방지)
        if (nav != null && nav.enabled)
        {
            nav.ResetPath();
            nav.enabled = false;
        }

        // Rigidbody를 Kinematic으로 (물리 간섭 방지)
        bool wasKinematic = false;
        if (rb != null)
        {
            wasKinematic = rb.isKinematic;
            rb.isKinematic = true;
        }

        Debug.Log($"[EnemyChasingState] {enemy.Data.enemyName} → Layout 간 점프 시작 " +
                  $"(목표: {landingTarget})");

        // DOJump 실행 — 포물선 점프
        enemyTransform.DOJump(landingTarget, data.gapJumpHeight, 1, data.gapJumpDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                OnJumpLanded(nav, rb, wasKinematic);
            });

        // TODO: 점프 애니메이션 트리거 (AnimationData에 Jump 파라미터 추가 시)
        // StartAnimation(stateMachine.Enemy.AnimationData.JumpParameterHash);
    }

    /// <summary>
    /// 점프 착지 처리
    /// - NavMesh 위 최종 위치 보정
    /// - NavMeshAgent 재활성
    /// - Rigidbody 원복
    /// </summary>
    private void OnJumpLanded(NavMeshAgent nav, Rigidbody rb, bool wasKinematic)
    {
        Enemy enemy = stateMachine.Enemy;

        // 파괴된 오브젝트 안전 체크
        if (enemy == null || enemy.gameObject == null)
        {
            isJumping = false;
            return;
        }

        Transform enemyTransform = enemy.transform;

        // NavMesh 위 위치 보정 (착지 지점이 NavMesh 밖일 수 있으므로)
        NavMeshHit hit;
        if (NavMesh.SamplePosition(enemyTransform.position, out hit, NAVMESH_SAMPLE_RADIUS, NavMesh.AllAreas))
        {
            enemyTransform.position = hit.position;
        }

        // Rigidbody 원복
        if (rb != null)
            rb.isKinematic = wasKinematic;

        // NavMeshAgent 재활성
        if (nav != null)
        {
            nav.enabled = true;

            if (nav.isOnNavMesh)
            {
                nav.Warp(enemyTransform.position);
            }
            else
            {
                // NavMesh 위에 없으면 가장 가까운 NavMesh 포인트로 워프
                if (NavMesh.SamplePosition(enemyTransform.position, out hit,
                    NAVMESH_SAMPLE_RADIUS * 2f, NavMesh.AllAreas))
                {
                    enemyTransform.position = hit.position;
                    nav.Warp(hit.position);
                }
                else
                {
                    Debug.LogWarning($"[EnemyChasingState] {enemy.Data.enemyName} 착지 실패 " +
                                     "— NavMesh 위치를 찾을 수 없음");
                }
            }
        }

        isJumping = false;

        Debug.Log($"[EnemyChasingState] {enemy.Data.enemyName} → 점프 착지 완료 " +
                  $"(위치: {enemyTransform.position})");
    }

    /// <summary>
    /// 점프 강제 종료 — 상태 Exit 시 점프 중이었을 때 호출
    /// - DOTween 트윈 강제 Kill
    /// - NavMeshAgent 복구
    /// </summary>
    private void ForceEndJump()
    {
        Enemy enemy = stateMachine.Enemy;
        if (enemy == null) return;

        // DOTween 트윈 강제 종료
        enemy.transform.DOKill();

        // NavMeshAgent 복구
        NavMeshAgent nav = enemy.nav;
        if (nav != null && !nav.enabled)
        {
            nav.enabled = true;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(enemy.transform.position, out hit,
                NAVMESH_SAMPLE_RADIUS * 2f, NavMesh.AllAreas))
            {
                enemy.transform.position = hit.position;
                if (nav.isOnNavMesh)
                    nav.Warp(hit.position);
            }
        }

        // Rigidbody 복구 (Kinematic이 바뀌었을 수 있음)
        Rigidbody rb = enemy.rigid;
        if (rb != null)
            rb.isKinematic = false;

        isJumping = false;

        Debug.Log($"[EnemyChasingState] {enemy.Data.enemyName} → 점프 강제 종료 (상태 전환)");
    }

    #endregion
}
