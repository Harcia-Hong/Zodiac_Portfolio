using UnityEngine;

/// <summary>
/// Enemy 전용 상태머신
/// Enemy의 모든 상태와 상태 전환을 관리
/// </summary>
public class EnemyStateMachine : StateMachine
{
    public Enemy Enemy { get; }
    public Transform Target { get; protected set; }
    public PlayerHealth TargetHealth { get; private set; }

    // 이동 관련
    public float MovementSpeed { get; private set; }
    public float RotationDamping { get; private set; }
    public float MovementSpeedModifier { get; set; } = 1f;

    // 공격 쿨타임 관리
    private float attackCooldownEndTime = 0f;

    // 상태 인스턴스들
    public EnemyIdleState IdleState { get; }
    public EnemyChasingState ChasingState { get; }
    public EnemyAttackState AttackState { get; }

    // 후퇴 상태 및 최초 조우 플래그
    public EnemyRetreatState RetreatState { get; }
    public bool IsFirstEncounter { get; set; } = true;

    public EnemyStateMachine(Enemy enemy)
    {
        Enemy = enemy;

        if (Enemy.Data != null)
        {
            MovementSpeed = Enemy.Data.baseSpeed;
            RotationDamping = Enemy.Data.baseRotationDamping;
        }
        else
        {
            MovementSpeed = 3.5f;
            RotationDamping = 10f;
        }

        FindPlayerTarget();

        IdleState = new EnemyIdleState(this);
        ChasingState = new EnemyChasingState(this);
        AttackState = new EnemyAttackState(this);
        RetreatState = new EnemyRetreatState(this);
    }

    private void FindPlayerTarget()
    {
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");

        if (playerGO != null)
        {
            Target = playerGO.transform;
            TargetHealth = playerGO.GetComponent<PlayerHealth>();
        }
    }


    public bool IsTargetValid()
    {
        if (Target == null) return false;
        if (TargetHealth != null && TargetHealth.isDead) return false;

        if (Enemy.Data != null)
        {
            float distance = Vector3.Distance(Enemy.transform.position, Target.position);
            return distance <= Enemy.Data.leashRange;
        }

        return true;
    }

    public bool IsPlayerInDetectRange()
    {
        if (!IsTargetValid()) return false;

        if (Enemy.Data != null)
        {
            float distance = Vector3.Distance(Enemy.transform.position, Target.position);
            return distance <= Enemy.Data.detectRadius;
        }

        return false;
    }

    public bool IsPlayerInAttackRange()
    {
        if (!IsTargetValid()) return false;

        IEnemyAttack attackComponent = Enemy.GetComponent<IEnemyAttack>();
        float attackRange = attackComponent?.GetAttackRange() ?? 2f;

        float distance = Vector3.Distance(Enemy.transform.position, Target.position);
        return distance <= attackRange;
    }

    /// <summary>
    /// 공격 쿨타임 설정
    /// </summary>
    public void SetAttackCooldown(float cooldown)
    {
        attackCooldownEndTime = Time.time + cooldown;
    }

    /// <summary>
    /// 공격 쿨타임 중인지 확인
    /// </summary>
    public bool IsAttackOnCooldown()
    {
        return Time.time < attackCooldownEndTime;
    }
}
