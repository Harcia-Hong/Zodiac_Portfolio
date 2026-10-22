using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 보스 전용 Idle 상태
/// - BossDataSO의 detectRadius로 플레이어 탐지
/// - 발견 시 BossChasingState로 전환
/// </summary>
public class BossIdleState : EnemyBaseState
{
    private BossStateMachine bossStateMachine;

    private const float DETECT_INTERVAL = 0.2f;
    private float lastDetectTime;

    public BossIdleState(BossStateMachine stateMachine) : base(stateMachine)
    {
        bossStateMachine = stateMachine;
    }

    public override void Enter()
    {
        base.Enter();
        bossStateMachine.MovementSpeedModifier = 0f;

        // Animator 직접 제어로 변경
        Animator anim = bossStateMachine.Enemy.GetComponentInChildren<Animator>();
        if (anim != null)
            anim.SetBool("Walk", false);

        // NavMesh 정지
        NavMeshAgent nav = bossStateMachine.Enemy.GetComponent<NavMeshAgent>();
        if (nav != null && nav.enabled)
        {
            nav.ResetPath();
            nav.velocity = Vector3.zero;
        }

        lastDetectTime = 0f;
        Debug.Log("[BossIdle] Idle 상태 진입");
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Update()
    {
        if (Time.time - lastDetectTime < DETECT_INTERVAL) return;
        lastDetectTime = Time.time;

        if (TryDetectPlayer())
        {
            bossStateMachine.ChangeState(bossStateMachine.BossChasingState);
        }
    }

    private bool TryDetectPlayer()
    {
        if (bossStateMachine.BossData == null) return false;

        float detectRadius = bossStateMachine.BossData.detectRadius;
        Vector3 bossPos = bossStateMachine.Enemy.transform.position;

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null) return false;

        float distance = Vector3.Distance(bossPos, playerObj.transform.position);
        if (distance <= detectRadius)
        {
            bossStateMachine.SetTarget(playerObj.transform);
            Debug.Log($"[BossIdle] 플레이어 탐지! 거리: {distance:F1}");
            return true;
        }

        return false;
    }
}