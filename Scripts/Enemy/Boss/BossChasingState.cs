using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 보스 전용 추적 상태
/// - 기존 EnemyChasingState와 유사하지만 보스 전용 로직 적용
/// - 공격 범위 진입 시 BossPatternSelectState로 전환
/// - 이동 속도에 페이즈 배율 적용
/// - 페이즈 전환 중에는 아무것도 하지 않음
/// </summary>
public class BossChasingState : EnemyBaseState
{
    private BossStateMachine bossStateMachine;

    // 타겟 위치 갱신 간격 (성능 최적화)
    private const float TARGET_UPDATE_INTERVAL = 0.15f;
    private float lastTargetUpdateTime;

    // 패턴 사용 가능 거리 (보스 데이터의 패턴 중 가장 긴 사거리 기준)
    private float maxPatternRange;

    public BossChasingState(BossStateMachine stateMachine) : base(stateMachine)
    {
        bossStateMachine = stateMachine;
    }

    #region 상태 진입/종료

    /// <summary>
    /// 추적 상태 진입
    /// - 이동 속도 설정 (페이즈 배율 적용)
    /// - 걷기 애니메이션 시작
    /// - 패턴 최대 사거리 계산
    /// </summary>
    public override void Enter()
    {
        base.Enter();

        // 이동 속도 설정
        bossStateMachine.MovementSpeedModifier = 1f;

        // 걷기 애니메이션 시작
        Animator anim = bossStateMachine.Enemy.GetComponentInChildren<Animator>();
        if (anim != null)
            anim.SetBool("Walk", true);

        // 패턴 최대 사거리 계산 (추적 → 공격 전환 거리 판단용)
        CalculateMaxPatternRange();

        // NavMeshAgent 활성화 확인
        NavMeshAgent nav = bossStateMachine.Enemy.GetComponent<NavMeshAgent>();
        if (nav != null && !nav.enabled)
        {
            nav.enabled = true;
            nav.Warp(bossStateMachine.Enemy.transform.position);
        }

        lastTargetUpdateTime = 0f;

        Debug.Log("[BossChasing] 추적 시작");
    }

    /// <summary>
    /// 추적 상태 종료
    /// - 걷기 애니메이션 정지
    /// - NavMesh 경로 초기화
    /// </summary>
    public override void Exit()
    {
        base.Exit();

        // 걷기 애니메이션 정지
        Animator anim = bossStateMachine.Enemy.GetComponentInChildren<Animator>();
        if (anim != null)
            anim.SetBool("Walk", false);

        // NavMesh 경로 초기화
        NavMeshAgent nav = bossStateMachine.Enemy.GetComponent<NavMeshAgent>();
        if (nav != null && nav.enabled)
        {
            nav.ResetPath();
        }
    }

    #endregion

    #region 상태 업데이트

    /// <summary>
    /// 매 프레임 업데이트
    /// - 페이즈 전환 체크
    /// - 타겟 유효성 체크
    /// - 패턴 사거리 진입 체크
    /// - 타겟 위치 갱신 및 이동
    /// </summary>
    public override void Update()
    {
        // 페이즈 전환 중이면 아무것도 안 함
        if (bossStateMachine.PhaseController != null && bossStateMachine.PhaseController.IsTransitioning)
            return;

        // 타겟 유효하지 않으면 Idle로
        if (!bossStateMachine.IsTargetValid())
        {
            bossStateMachine.ChangeState(bossStateMachine.IdleState);
            return;
        }

        // 플레이어와의 거리 계산
        float distanceToPlayer = Vector3.Distance(
            bossStateMachine.Enemy.transform.position,
            bossStateMachine.Target.position
        );

        // 패턴 사거리 안이고 + 글로벌 쿨타임이 아니면 → 패턴 선택 상태로
        if (distanceToPlayer <= maxPatternRange && !bossStateMachine.IsAttackOnCooldown())
        {
            bossStateMachine.ChangeState(bossStateMachine.PatternSelectState);
            return;
        }

        // 타겟 위치 갱신 (일정 간격으로)
        if (Time.time - lastTargetUpdateTime >= TARGET_UPDATE_INTERVAL)
        {
            UpdateTargetTracking();
            lastTargetUpdateTime = Time.time;
        }

        // 플레이어 방향으로 회전
        RotateTowardsTarget();
    }

    #endregion

    #region 이동 처리

    /// <summary>
    /// NavMeshAgent를 사용한 타겟 추적
    /// - 페이즈 배율이 적용된 이동 속도 사용
    /// </summary>
    private void UpdateTargetTracking()
    {
        if (!bossStateMachine.IsTargetValid()) return;

        NavMeshAgent nav = bossStateMachine.Enemy.GetComponent<NavMeshAgent>();
        if (nav == null || !nav.enabled || !nav.isOnNavMesh) return;

        // 페이즈 배율이 적용된 이동 속도
        float finalSpeed = bossStateMachine.GetBossMoveSpeed();
        nav.speed = finalSpeed;

        // 목적지 설정
        nav.SetDestination(bossStateMachine.Target.position);
    }

    #endregion

    #region 유틸리티

    /// <summary>
    /// 현재 페이즈의 패턴 중 가장 긴 사거리를 계산
    /// - 이 거리 안에 들어오면 패턴 선택 상태로 전환
    /// - 패턴이 없거나 maxRange가 0이면 기본값 사용
    /// </summary>
    private void CalculateMaxPatternRange()
    {
        maxPatternRange = 10f; // 기본값

        BossAttackPatternSO[] patterns = bossStateMachine.PhaseController?.GetCurrentPatterns();
        if (patterns == null || patterns.Length == 0) return;

        float longest = 0f;

        foreach (var pattern in patterns)
        {
            if (pattern == null) continue;

            // maxRange가 0이면 거리 무제한 → 큰 값으로 간주
            float range = pattern.maxRange > 0f ? pattern.maxRange : 30f;

            if (range > longest)
                longest = range;
        }

        if (longest > 0f)
            maxPatternRange = longest;

        Debug.Log($"[BossChasing] 패턴 최대 사거리: {maxPatternRange}");
    }

    #endregion
}