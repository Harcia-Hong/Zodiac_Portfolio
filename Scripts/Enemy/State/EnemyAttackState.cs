using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Enemy 공격 상태
/// 공격 1회 실행 후 쿨타임 설정하고 다른 상태로 전환
/// </summary>
public class EnemyAttackState : EnemyBaseState
{
    private IEnemyAttack attackComponent;
    private AttackTelegraph telegraph;
    private bool attackStarted;
    private bool waitingForTelegraph;

    public EnemyAttackState(EnemyStateMachine stateMachine) : base(stateMachine)
    {
    }

    public override void Enter()
    {
        base.Enter();

        // 초기화
        attackStarted = false;
        waitingForTelegraph = false;
        attackComponent = stateMachine.Enemy.GetComponent<IEnemyAttack>();
        telegraph = stateMachine.Enemy.GetComponent<AttackTelegraph>();

        // Charge 타입은 자체적으로 이동을 제어하므로 멈추지 않음
        bool shouldStop = stateMachine.Enemy.enemyType != EnemyType.Charge;

        if (shouldStop)
        {
            // 이동 중지 (일반/원거리/혼합형만)
            stateMachine.MovementSpeedModifier = 0f;

            var nav = stateMachine.Enemy.GetComponent<NavMeshAgent>();
            if (nav != null && nav.enabled)
            {
                nav.ResetPath();
                nav.velocity = Vector3.zero;
            }
        }

        // 텔레그래프가 있으면 먼저 표시, 없으면 바로 공격
        if (telegraph != null && attackComponent != null)
        {
            ShowTelegraph();
        }
        else
        {
            StartActualAttack();
        }
    }

    public override void Exit()
    {
        base.Exit();

        // 공격 애니메이션 중지
        if (stateMachine.Enemy.AnimationData != null)
        {
            StopAnimation(stateMachine.Enemy.AnimationData.AttackParameterHash);
            StopAnimation(stateMachine.Enemy.AnimationData.BaseAttackParameterHash);
        }

        // 공격 중단
        if (attackComponent != null && attackComponent.IsAttacking())
        {
            attackComponent.StopAttack();
        }

        // 텔레그래프 강제 취소
        if (telegraph != null)
        {
            telegraph.Cancel();
        }
    }

    public override void Update()
    {
        // 플레이어 방향으로 회전
        if (!attackStarted)
            RotateTowardsTarget();

        // 텔레그래프 대기 중이면 아직 공격 안 함
        if (waitingForTelegraph) return;

        if (attackComponent == null)
        {
            stateMachine.ChangeState(stateMachine.IdleState);
            return;
        }

        // 공격이 끝났으면 다음 상태로 전환
        if (attackStarted && !attackComponent.IsAttacking())
        {
            TransitionAfterAttack();
        }
    }

    private void InitializeAttack()
    {
        attackComponent = stateMachine.Enemy.GetComponent<IEnemyAttack>();

        if (attackComponent != null)
        {
            attackComponent.StartAttack();
            attackStarted = true;

            // 쿨타임 설정 (여기서 설정!)
            stateMachine.SetAttackCooldown(attackComponent.GetCooldownTime());

            Debug.Log($"[EnemyAttack] {stateMachine.Enemy.name} 공격! 쿨타임: {attackComponent.GetCooldownTime()}초");
        }
    }

    /// <summary>
    /// 텔레그래프 표시 (공격 예고)
    /// </summary>
    private void ShowTelegraph()
    {
        waitingForTelegraph = true;

        Vector3 enemyPos = stateMachine.Enemy.transform.position;
        Vector3 targetPos = stateMachine.Target != null ? stateMachine.Target.position : enemyPos + stateMachine.Enemy.transform.forward;
        Vector3 direction = (targetPos - enemyPos).normalized;
        float range = attackComponent.GetAttackRange();

        // 몬스터 타입에 따라 텔레그래프 형태 결정
        EnemyType enemyType = stateMachine.Enemy.Data != null
            ? stateMachine.Enemy.Data.enemyType
            : EnemyType.Standard;

        switch (enemyType)
        {
            case EnemyType.Standard:
            case EnemyType.Charge:
                telegraph.ShowCircle(enemyPos, range, OnTelegraphComplete);
                break;

            case EnemyType.Stationary:
                telegraph.ShowArrow(enemyPos, direction, range, OnTelegraphComplete);
                break;

            case EnemyType.Hybrid:
                // 거리에 따라 원형 또는 화살표
                float distance = Vector3.Distance(enemyPos, targetPos);
                float switchDist = stateMachine.Enemy.Data != null
                    ? stateMachine.Enemy.Data.hybridSwitchDistance
                    : 5f;

                if (distance <= switchDist)
                    telegraph.ShowCone(enemyPos, direction, range, OnTelegraphComplete);
                else
                    telegraph.ShowArrow(enemyPos, direction, range, OnTelegraphComplete);
                break;
        }
    }

    /// <summary>
    /// 텔레그래프 완료 콜백 → 실제 공격 시작
    /// </summary>
    private void OnTelegraphComplete()
    {
        waitingForTelegraph = false;
        StartActualAttack();
    }

    /// <summary>
    /// 실제 공격 시작
    /// </summary>
    private void StartActualAttack()
    {
        // 공격 애니메이션 시작
        if (stateMachine.Enemy.AnimationData != null)
        {
            StartAnimation(stateMachine.Enemy.AnimationData.AttackParameterHash);
            StartAnimation(stateMachine.Enemy.AnimationData.BaseAttackParameterHash);
        }

        if (attackComponent != null)
        {
            attackComponent.StartAttack();
            attackStarted = true;

            stateMachine.SetAttackCooldown(attackComponent.GetCooldownTime());
        }
    }

    /// <summary>
    /// 공격 완료 후 상태 전환
    /// </summary>
    private void TransitionAfterAttack()
    {
        if (attackComponent != null)
        {
            stateMachine.SetAttackCooldown(attackComponent.GetCooldownTime());
        }

        if (!stateMachine.IsTargetValid())
        {
            stateMachine.ChangeState(stateMachine.IdleState);
            return;
        }

        stateMachine.ChangeState(stateMachine.ChasingState);
    }
}
