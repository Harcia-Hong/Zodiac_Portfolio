using UnityEngine;
using UnityEngine.AI;

/**
 * Enemy 후퇴 상태 (1회성 패턴)
 * - 최초 조우 시 일정 확률로 뒤로 물러남 (간보기)
 * - 플레이어를 바라보며 뒷걸음질 침
 */
public class EnemyRetreatState : EnemyBaseState
{
    private float retreatDuration = 1.5f; // 후퇴 지속 시간
    private float retreatDistance = 4.0f; // 후퇴 거리
    private float retreatSpeedMultiplier = 1.5f; // 후퇴 시 이동 속도 배율 (빠르게)

    private float retreatStartTime;
    private Vector3 retreatTargetPos;

    public EnemyRetreatState(EnemyStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override void Enter()
    {
        base.Enter();

        // 최초 조우 플래그 해제 (이제 다시는 후퇴 안 함)
        stateMachine.IsFirstEncounter = false;

        // 1. 후퇴 방향 계산 (플레이어 반대 방향)
        Vector3 directionToPlayer = (stateMachine.Target.position - stateMachine.Enemy.transform.position).normalized;
        Vector3 retreatDir = -directionToPlayer;

        // 2. 목표 지점 설정 (NavMesh 위인지 체크는 NavMeshAgent가 알아서 처리하도록 시도)
        retreatTargetPos = stateMachine.Enemy.transform.position + retreatDir * retreatDistance;

        // 3. 이동 속도 증가 (슉 빠지는 느낌)
        stateMachine.MovementSpeedModifier = retreatSpeedMultiplier;

        // 4. NavMesh 이동 명령
        var nav = stateMachine.Enemy.GetComponent<NavMeshAgent>();
        if (nav != null && nav.enabled)
        {
            nav.ResetPath();
            nav.speed = stateMachine.MovementSpeed * retreatSpeedMultiplier;
            nav.SetDestination(retreatTargetPos);
            nav.isStopped = false;
        }

        // 5. 애니메이션 (걷기)
        // 뒤로 가더라도 다리는 움직여야 하므로 Walk 재생
        if (stateMachine.Enemy.AnimationData != null)
        {
            StartAnimation(stateMachine.Enemy.AnimationData.GroundParameterHash);
            StartAnimation(stateMachine.Enemy.AnimationData.WalkParameterHash);
        }

        retreatStartTime = Time.time;
        // Debug.Log($"[Enemy] {stateMachine.Enemy.name} 쫄아서 후퇴 중!");
    }

    public override void Exit()
    {
        base.Exit();

        // 속도 및 애니메이션 원상복구
        stateMachine.MovementSpeedModifier = 1f;

        if (stateMachine.Enemy.AnimationData != null)
        {
            StopAnimation(stateMachine.Enemy.AnimationData.WalkParameterHash);
        }

        var nav = stateMachine.Enemy.GetComponent<NavMeshAgent>();
        if (nav != null && nav.enabled)
        {
            nav.ResetPath();
        }
    }

    public override void Update()
    {
        // 1. 계속 플레이어를 쳐다보기 (주시)
        RotateTowardsTarget();

        // 2. 종료 조건 체크
        // - 시간이 다 되었거나
        // - 목표 지점에 거의 도달했거나
        bool timeOut = Time.time - retreatStartTime >= retreatDuration;

        var nav = stateMachine.Enemy.GetComponent<NavMeshAgent>();
        bool reached = nav != null && !nav.pathPending && nav.remainingDistance <= 0.5f;

        if (timeOut || reached)
        {
            // 후퇴 끝났으니 다시 추적 시작
            stateMachine.ChangeState(stateMachine.ChasingState);
        }
    }
}
