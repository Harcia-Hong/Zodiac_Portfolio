using UnityEngine;

/// <summary>
/// 보스 패턴 선택 + 실행 상태
/// - 보스가 공격 범위에 진입했을 때 진입
/// - 글로벌 쿨타임 대기 → 패턴 선택 → 실행 → 완료 대기 → 복귀
/// 
/// 흐름:
///   Enter: 이동 정지, 플레이어 방향 회전
///   Update: 글로벌 쿨타임 대기 → 패턴 선택 → 실행 → 완료 감지 → 쿨타임 설정 → 복귀
///   Exit: 패턴 쿨타임 기록
/// </summary>
public class BossPatternSelectState : EnemyBaseState
{
    private BossStateMachine bossStateMachine;

    // 상태 내부 진행 단계
    private enum Phase
    {
        WaitingGlobalCooldown,  // 글로벌 쿨타임 대기
        SelectingPattern,       // 패턴 선택
        ExecutingPattern,       // 패턴 실행 중 (완료 대기)
        Finished                // 완료 → 상태 전환 대기
    }

    private Phase currentPhase;
    private float globalCooldownEndTime;
    private BossAttackPatternSO selectedPattern;

    // 패턴 선택 실패 시 재시도 간격
    private float retryInterval = 0.5f;
    private float lastRetryTime;

    public BossPatternSelectState(BossStateMachine stateMachine) : base(stateMachine)
    {
        bossStateMachine = stateMachine;
    }

    #region 상태 진입/종료

    /// <summary>
    /// 상태 진입
    /// - 이동 정지 + 글로벌 쿨타임 계산
    /// </summary>
    public override void Enter()
    {
        base.Enter();

        // 초기화
        selectedPattern = null;
        lastRetryTime = 0f;

        // 글로벌 쿨타임 설정
        float globalCooldown = bossStateMachine.BossData != null
            ? bossStateMachine.BossData.globalCooldown
            : 1.0f;

        globalCooldownEndTime = Time.time + globalCooldown;
        currentPhase = Phase.WaitingGlobalCooldown;

        // 쿨타임 대기 중에는 이동 유지 (걸으면서 추적)
        Animator anim = bossStateMachine.Enemy.GetComponentInChildren<Animator>();
        if (anim != null)
            anim.SetBool("Walk", true);

        Debug.Log($"[BossPatternSelect] 진입 - 글로벌 쿨타임 {globalCooldown}초 대기");
    }

    /// <summary>
    /// 상태 종료
    /// - 선택된 패턴의 개별 쿨타임 기록
    /// </summary>
    public override void Exit()
    {
        base.Exit();

        // Walk 애니메이션 끄기
        Animator anim = bossStateMachine.Enemy.GetComponentInChildren<Animator>();
        if (anim != null)
            anim.SetBool("Walk", false);

        // 사용한 패턴의 쿨타임 등록
        if (selectedPattern != null)
        {
            bossStateMachine.SetPatternCooldown(selectedPattern);
        }

        selectedPattern = null;
    }

    #endregion

    #region 상태 업데이트

    /// <summary>
    /// 매 프레임 업데이트
    /// - 단계별 로직 분기 처리
    /// </summary>
    public override void Update()
    {
        // 페이즈 전환 중이면 아무것도 안 함 (PhaseController가 강제 상태 전환함)
        if (bossStateMachine.PhaseController != null && bossStateMachine.PhaseController.IsTransitioning)
            return;

        switch (currentPhase)
        {
            case Phase.WaitingGlobalCooldown:
                UpdateWaitingCooldown();
                break;

            case Phase.SelectingPattern:
                UpdateSelectingPattern();
                break;

            case Phase.ExecutingPattern:
                UpdateExecutingPattern();
                break;

            case Phase.Finished:
                TransitionToChasing();
                break;
        }
    }

    #endregion

    #region 단계별 로직

    /// <summary>
    /// 글로벌 쿨타임 대기
    /// - 대기 중 플레이어 방향으로 부드럽게 회전
    /// </summary>
    private void UpdateWaitingCooldown()
    {
        // 대기 중 플레이어 추적 (걸으면서)
        RotateTowardsTarget();

        // NavMesh로 이동 유지
        var nav = bossStateMachine.Enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (nav != null && nav.enabled && bossStateMachine.IsTargetValid())
        {
            float finalSpeed = bossStateMachine.GetBossMoveSpeed();
            nav.speed = finalSpeed;
            nav.SetDestination(bossStateMachine.Target.position);
        }

        if (Time.time >= globalCooldownEndTime)
        {
            // 쿨타임 끝 → 이동 멈추고 패턴 선택
            StopMovement();

            Animator anim = bossStateMachine.Enemy.GetComponentInChildren<Animator>();
            if (anim != null)
                anim.SetBool("Walk", false);

            currentPhase = Phase.SelectingPattern;
            Debug.Log("[BossPatternSelect] 글로벌 쿨타임 완료 → 패턴 선택 시작");
        }
    }

    /// <summary>
    /// 패턴 선택
    /// - 선택 실패 시 일정 간격으로 재시도
    /// - 계속 실패하면 추적 상태로 복귀
    /// </summary>
    private void UpdateSelectingPattern()
    {
        // 재시도 간격 체크
        if (Time.time - lastRetryTime < retryInterval) return;
        lastRetryTime = Time.time;

        // 패턴 선택 시도
        selectedPattern = bossStateMachine.SelectPattern();

        if (selectedPattern != null)
        {
            // 패턴 선택 성공 → 실행
            StartPatternExecution();
        }
        else
        {
            // 선택 실패 → 추적으로 복귀 (모든 패턴이 쿨타임이거나 거리 불만족)
            Debug.Log("[BossPatternSelect] 패턴 선택 실패 → 추적 복귀");
            currentPhase = Phase.Finished;
        }
    }

    /// <summary>
    /// 패턴 실행 중 완료 대기
    /// </summary>
    private void UpdateExecutingPattern()
    {
        // PatternExecutor가 실행 완료했는지 확인
        if (bossStateMachine.PatternExecutor == null || !bossStateMachine.PatternExecutor.IsAttacking())
        {
            Debug.Log($"[BossPatternSelect] 패턴 '{selectedPattern?.patternName}' 실행 완료");
            currentPhase = Phase.Finished;
        }
    }

    /// <summary>
    /// 완료 → 추적 상태로 전환
    /// </summary>
    private void TransitionToChasing()
    {
        bossStateMachine.ChangeState(bossStateMachine.BossChasingState);
    }

    #endregion

    #region 패턴 실행

    /// <summary>
    /// 선택된 패턴을 PatternExecutor에 전달하여 실행 시작
    /// </summary>
    private void StartPatternExecution()
    {
        if (bossStateMachine.PatternExecutor == null)
        {
            Debug.LogError("[BossPatternSelect] PatternExecutor가 없습니다!");
            currentPhase = Phase.Finished;
            return;
        }

        Debug.Log($"[BossPatternSelect] 패턴 실행 시작: '{selectedPattern.patternName}'");

        bossStateMachine.PatternExecutor.ExecutePattern(selectedPattern);
        currentPhase = Phase.ExecutingPattern;
    }

    #endregion

    #region 유틸리티

    /// <summary>
    /// NavMeshAgent 이동 정지
    /// </summary>
    private void StopMovement()
    {
        var nav = bossStateMachine.Enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (nav != null && nav.enabled)
        {
            nav.ResetPath();
            nav.velocity = Vector3.zero;
        }

        bossStateMachine.MovementSpeedModifier = 0f;
    }

    /// <summary>
    /// 플레이어 방향으로 즉시 회전
    /// </summary>
    private void LookAtPlayerInstant()
    {
        if (!bossStateMachine.IsTargetValid()) return;

        Vector3 dir = (bossStateMachine.Target.position - bossStateMachine.Enemy.transform.position);
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.001f)
        {
            bossStateMachine.Enemy.transform.rotation = Quaternion.LookRotation(dir.normalized);
        }
    }

    #endregion
}