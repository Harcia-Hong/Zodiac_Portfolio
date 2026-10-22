using UnityEngine;
using UnityEngine.AI;

/**
 * Enemy 상태들의 기반 클래스
 * 모든 Enemy 상태가 공통으로 사용하는 기능들을 제공
 * - 이동, 회전, 애니메이션 제어 등
 */
public abstract class EnemyBaseState : IState
{
    protected EnemyStateMachine stateMachine;

    /// <summary>
    /// 생성자 - 모든 Enemy 상태는 StateMachine 참조를 받음
    /// </summary>
    /// <param name="stateMachine">이 상태가 속한 상태머신</param>
    protected EnemyBaseState(EnemyStateMachine stateMachine)
    {
        this.stateMachine = stateMachine;
    }

    /// <summary>
    /// 상태 진입 시 기본 처리
    /// 자식 클래스에서 오버라이드하여 추가 처리 가능
    /// </summary>
    public virtual void Enter()
    {
        Debug.Log($"[Enemy] {GetType().Name} 상태 진입");
    }

    /// <summary>
    /// 상태 종료 시 기본 처리
    /// </summary>
    public virtual void Exit()
    {
        Debug.Log($"[Enemy] {GetType().Name} 상태 종료");
    }

    /// <summary>
    /// 매 프레임 업데이트 - 기본 이동 처리
    /// </summary>
    public virtual void Update()
    {
        // 기본적으로 플레이어 쪽으로 이동
        MoveTowardsTarget();
    }

    /// <summary>
    /// 물리 업데이트 - 기본적으로 아무것도 하지 않음
    /// </summary>
    public virtual void PhysicsUpdate()
    {
        // 필요한 상태에서만 오버라이드
    }

    /// <summary>
    /// 입력 처리 - AI의 경우 보통 사용하지 않음
    /// </summary>
    public virtual void HandleInput()
    {
        // AI는 입력이 없으므로 기본적으로 비어둠
    }

    #region 공통 유틸리티 메서드들

    /// <summary>
    /// 애니메이션 파라미터 활성화
    /// </summary>
    /// <param name="animatorHash">애니메이션 파라미터 해시</param>
    protected void StartAnimation(int animatorHash)
    {
        if (stateMachine.Enemy.animator != null && animatorHash != 0)
        {
            stateMachine.Enemy.animator.SetBool(animatorHash, true);
        }
    }

    /// <summary>
    /// 애니메이션 파라미터 비활성화
    /// </summary>
    /// <param name="animatorHash">애니메이션 파라미터 해시</param>
    protected void StopAnimation(int animatorHash)
    {
        if (stateMachine.Enemy.animator != null && animatorHash != 0)
        {
            stateMachine.Enemy.animator.SetBool(animatorHash, false);
        }
    }

    /// <summary>
    /// 플레이어를 향해 이동
    /// NavMeshAgent를 사용한 경로 이동
    /// </summary>
    protected void MoveTowardsTarget()
    {
        if (!stateMachine.IsTargetValid()) return;

        NavMeshAgent nav = stateMachine.Enemy.GetComponent<NavMeshAgent>();
        if (nav != null && nav.enabled)
        {
            // 이동 속도 적용
            float finalSpeed = stateMachine.MovementSpeed * stateMachine.MovementSpeedModifier;
            nav.speed = finalSpeed;

            // 목표 지점 설정
            nav.SetDestination(stateMachine.Target.position);
        }
    }

    /// <summary>
    /// 플레이어를 향해 회전
    /// 부드러운 회전 처리
    /// </summary>
    protected void RotateTowardsTarget()
    {
        if (!stateMachine.IsTargetValid()) return;

        Vector3 direction = (stateMachine.Target.position - stateMachine.Enemy.transform.position).normalized;
        direction.y = 0f; // XZ 평면에서만 회전 (쿼터뷰)

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            stateMachine.Enemy.transform.rotation = Quaternion.Slerp(
                stateMachine.Enemy.transform.rotation,
                targetRotation,
                stateMachine.RotationDamping * Time.deltaTime
            );
        }
    }

    /// <summary>
    /// 애니메이션의 정규화된 시간 가져오기
    /// 공격 타이밍 등에서 사용
    /// </summary>
    /// <param name="animator">애니메이터</param>
    /// <param name="tag">애니메이션 태그</param>
    /// <returns>0~1 사이의 정규화된 시간</returns>
    protected float GetNormalizedTime(Animator animator, string tag)
    {
        if (animator == null) return 0f;

        AnimatorStateInfo currentInfo = animator.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo nextInfo = animator.GetNextAnimatorStateInfo(0);

        // 전환 중이고 다음 상태가 해당 태그면
        if (animator.IsInTransition(0) && nextInfo.IsTag(tag))
        {
            return nextInfo.normalizedTime;
        }
        // 전환 중이 아니고 현재 상태가 해당 태그면
        else if (!animator.IsInTransition(0) && currentInfo.IsTag(tag))
        {
            return currentInfo.normalizedTime;
        }

        return 0f;
    }

    #endregion
}
