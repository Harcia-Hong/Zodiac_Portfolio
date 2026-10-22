using UnityEngine;

/// <summary>
/// 보스 페이즈 전환 연출 상태
/// - BossPhaseController가 페이즈 전환을 감지하면 강제 진입
/// - 무적 상태에서 전환 연출이 끝날 때까지 대기
/// - 모든 이동/공격 입력을 무시
/// - 전환 완료 시 BossStateMachine이 자동으로 BossChasingState로 복귀
/// 
/// 흐름:
///   PhaseController.OnPhaseTransitionStart 이벤트
///   → BossStateMachine.HandlePhaseTransitionStart()
///   → ChangeState(PhaseTransitionState)
///   → Enter: 이동 정지, 애니메이션 재생
///   → Update: 전환 완료 대기 (아무것도 안 함)
///   → PhaseController.OnPhaseTransitionEnd 이벤트
///   → BossStateMachine.HandlePhaseTransitionEnd()
///   → ChangeState(BossChasingState)
///   → Exit: 정리
/// </summary>
public class BossPhaseTransitionState : EnemyBaseState
{
    private BossStateMachine bossStateMachine;

    // 전환 진입 시간 (안전장치용)
    private float enterTime;
    private const float MAX_TRANSITION_TIME = 15f; // 최대 전환 시간 (무한 대기 방지)

    public BossPhaseTransitionState(BossStateMachine stateMachine) : base(stateMachine)
    {
        bossStateMachine = stateMachine;
    }

    #region 상태 진입/종료

    /// <summary>
    /// 상태 진입
    /// - 모든 행동 즉시 정지
    /// - 전환 연출 애니메이션 트리거
    /// </summary>
    public override void Enter()
    {
        base.Enter();

        enterTime = Time.time;

        // 1. 이동 완전 정지
        StopAllMovement();

        // 2. 진행 중인 패턴 중단
        if (bossStateMachine.PatternExecutor != null && bossStateMachine.PatternExecutor.IsAttacking())
        {
            bossStateMachine.PatternExecutor.StopAttack();
        }

        // 3. 페이즈 전환 애니메이션 트리거
        Animator anim = bossStateMachine.Enemy.animator;
        if (anim != null)
        {
            anim.SetTrigger("doPhaseTransition");
        }

        Debug.Log("[BossPhaseTransition] 전환 상태 진입 - 무적 + 연출 대기");
    }

    /// <summary>
    /// 상태 종료
    /// - 정리 작업
    /// </summary>
    public override void Exit()
    {
        base.Exit();

        // 애니메이션 복구 (혹시 전환 애니메이션이 남아있을 경우)
        Animator anim = bossStateMachine.Enemy.animator;
        if (anim != null)
        {
            anim.ResetTrigger("doPhaseTransition");
        }

        Debug.Log("[BossPhaseTransition] 전환 상태 종료 - 전투 재개");
    }

    #endregion

    #region 상태 업데이트

    /// <summary>
    /// 매 프레임 업데이트
    /// - 기본적으로 대기만 함 (BossPhaseController가 전환 완료 이벤트를 발행하면 자동 복귀)
    /// - 안전장치: 최대 시간 초과 시 강제 복귀
    /// </summary>
    public override void Update()
    {
        // 이동하지 않음 (base.Update() 호출 안 함)

        // 안전장치: 최대 전환 시간 초과 시 강제 복귀
        if (Time.time - enterTime > MAX_TRANSITION_TIME)
        {
            Debug.LogWarning($"[BossPhaseTransition] 최대 전환 시간 {MAX_TRANSITION_TIME}초 초과! 강제 복귀");
            ForceExitTransition();
        }
    }

    /// <summary>
    /// 물리 업데이트 - 전환 중 물리 무시
    /// </summary>
    public override void PhysicsUpdate()
    {
        // 전환 중 물리 처리 없음
    }

    /// <summary>
    /// 입력 처리 - 전환 중 입력 무시
    /// </summary>
    public override void HandleInput()
    {
        // 전환 중 모든 입력 무시
    }

    #endregion

    #region 유틸리티

    /// <summary>
    /// 모든 이동 즉시 정지
    /// </summary>
    private void StopAllMovement()
    {
        // NavMeshAgent 정지
        var nav = bossStateMachine.Enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (nav != null && nav.enabled)
        {
            nav.ResetPath();
            nav.velocity = Vector3.zero;
            nav.isStopped = true;
        }

        // Rigidbody 속도 초기화
        Rigidbody rb = bossStateMachine.Enemy.rigid;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 이동 속도 0
        bossStateMachine.MovementSpeedModifier = 0f;
    }

    /// <summary>
    /// 안전장치 - 강제 전환 종료
    /// </summary>
    private void ForceExitTransition()
    {
        // PhaseController의 전환 상태 강제 해제
        if (bossStateMachine.PhaseController != null && bossStateMachine.PhaseController.IsTransitioning)
        {
            Debug.LogWarning("[BossPhaseTransition] PhaseController 전환 상태 강제 해제");
        }

        // 추적 상태로 복귀
        bossStateMachine.ChangeState(bossStateMachine.BossChasingState);
    }

    #endregion
}