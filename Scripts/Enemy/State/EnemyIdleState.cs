/**
 * Enemy 대기 상태
 * - 플레이어가 탐지 범위에 없을 때의 상태
 * - 기본 애니메이션 재생
 * - 플레이어 탐지 시 Chasing 상태로 전환
 */
public class EnemyIdleState : EnemyBaseState
{
    public EnemyIdleState(EnemyStateMachine stateMachine) : base(stateMachine)
    {
    }

    /// <summary>
    /// 대기 상태 진입
    /// - 이동 속도를 0으로 설정
    /// - 대기 애니메이션 시작
    /// </summary>
    public override void Enter()
    {
        base.Enter();

        // 이동 중지
        stateMachine.MovementSpeedModifier = 0f;

        // 대기 애니메이션 시작
        if (stateMachine.Enemy.AnimationData != null)
        {
            StartAnimation(stateMachine.Enemy.AnimationData.GroundParameterHash);
            StartAnimation(stateMachine.Enemy.AnimationData.IdleParameterHash);
        }

        // NavMeshAgent 정지
        var nav = stateMachine.Enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (nav != null && nav.enabled)
        {
            nav.ResetPath();
            nav.velocity = UnityEngine.Vector3.zero;
        }
    }

    /// <summary>
    /// 대기 상태 종료
    /// - 대기 애니메이션 중지
    /// </summary>
    public override void Exit()
    {
        base.Exit();

        // 대기 애니메이션 중지
        if (stateMachine.Enemy.AnimationData != null)
        {
            StopAnimation(stateMachine.Enemy.AnimationData.GroundParameterHash);
            StopAnimation(stateMachine.Enemy.AnimationData.IdleParameterHash);
        }
    }

    /// <summary>
    /// 대기 상태 업데이트
    /// - 플레이어 탐지 확인
    /// - 탐지 시 추적 상태로 전환
    /// </summary>
    public override void Update()
    {
        // 기본 이동은 하지 않음 (대기 상태이므로)
        // base.Update() 호출 안함

        // 플레이어가 탐지 범위에 들어왔는지 확인
        if (stateMachine.IsPlayerInDetectRange())
        {
            // 추적 상태로 전환
            stateMachine.ChangeState(stateMachine.ChasingState);
            return;
        }

        // 플레이어 쪽으로 회전 (탐지는 안됐지만 방향은 유지)
        if (stateMachine.IsTargetValid())
        {
            RotateTowardsTarget();
        }
    }
}
