using UnityEngine;

/// <summary>
/// 플레이어 공격 관리자 - 쿼터뷰 지상 전용
/// 공중 공격 및 연계 시스템 제거
/// </summary>
public class PlayerAttackManager : MonoBehaviour
{
    [Header("Attack System References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerGroundAttack groundAttack;        // 지상 공격 시스템
    //[SerializeField] private PlayerRushAttack rushAttack;            // 돌진 공격 시스템
    [SerializeField] private PlayerFlashSlash spinAttack;            // 스핀 어택 시스템
    [SerializeField] private PlayerLockOn lockOnSystem;              // 락온 시스템

    [Header("Target Detection Settings")]
    [SerializeField] private LayerMask enemyLayer = -1;              // 적 레이어

    /// <summary>현재 실행 중인 공격 타입</summary>
    public enum AttackType { None, Ground, Rush, Spin }
    private AttackType currentAttackType = AttackType.None;

    private void Awake()
    {
        InitializeComponents();
    }

    /// <summary>컴포넌트 자동 참조 설정</summary>
    private void InitializeComponents()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (groundAttack == null)
            groundAttack = GetComponent<PlayerGroundAttack>();

        /*if (rushAttack == null)
            rushAttack = GetComponent<PlayerRushAttack>();*/

        if (spinAttack == null)
            spinAttack = GetComponent<PlayerFlashSlash>();

        if (lockOnSystem == null)
            lockOnSystem = GetComponent<PlayerLockOn>();
    }

    void Update()
    {
        HandleAttackInput();
        UpdateCurrentAttackType();
    }

    /// <summary>좌클릭 입력 처리</summary>
    private void HandleAttackInput()
    {
        if (!CanPerformAttack()) return;

        if (Input.GetMouseButtonDown(0))
        {
            HandleNormalAttack();
        }
    }

    /// <summary>공격 실행 가능 여부 확인</summary>
    private bool CanPerformAttack()
    {
        if (playerController.isDodging || playerController.isSkillCasting || playerController.isCharging)
            return false;

        if (IsAnyAttackActive())
            return false;

        return true;
    }

    /// <summary>현재 어떤 공격이든 실행 중인지 확인</summary>
    private bool IsAnyAttackActive()
    {
        if (groundAttack != null && groundAttack.isGroundAttacking)
            return true;

        /*if (rushAttack != null && rushAttack.IsRushing())
            return true;*/

       // if (spinAttack != null && spinAttack.isSpinAttack())
       //     return true;

        return false;
    }

    /// <summary>일반 공격 분기 처리</summary>
    private void HandleNormalAttack()
    {
        Transform lockedTarget = GetCurrentLockedTarget();

        /*if (lockedTarget != null && IsValidRushTarget(lockedTarget))
        {
            // 락온된 적이 있고 돌진 가능한 거리면 돌진 공격
            ExecuteRushAttack(lockedTarget);
        }
        else
        {
            // 기본 지상 공격
            ExecuteGroundAttack();
        }*/

        ExecuteGroundAttack();
    }

    /// <summary>현재 락온된 타겟 반환</summary>
    private Transform GetCurrentLockedTarget()
    {
        if (lockOnSystem != null && lockOnSystem.isLockOn)
            return lockOnSystem.currentTarget;

        return null;
    }

    /// <summary>돌진 공격이 유효한 타겟인지 확인</summary>
    private bool IsValidRushTarget(Transform target)
    {
        if (target == null) return false;

        float distance = Vector3.Distance(transform.position, target.position);

        // 너무 가깝거나 너무 멀면 돌진하지 않음
        return distance >= 3f && distance <= 15f;
    }

    /// <summary>지상 공격 실행</summary>
    private void ExecuteGroundAttack()
    {
        if (groundAttack != null && groundAttack.CanGroundAttack())
        {
            groundAttack.StartGroundCombo();
            currentAttackType = AttackType.Ground;
        }
    }

    /// <summary>돌진 공격 실행</summary>
    /*private void ExecuteRushAttack(Transform target)
    {
        if (rushAttack != null && target != null)
        {
            rushAttack.StartRushAttack(target);
            currentAttackType = AttackType.Rush;
            Debug.Log($"돌진 공격 실행: {target.name}");
        }
    }*/

    /// <summary>현재 공격 타입 업데이트</summary>
    private void UpdateCurrentAttackType()
    {
        if (!IsAnyAttackActive())
        {
            currentAttackType = AttackType.None;
        }
    }


    /// <summary>현재 공격 타입 반환</summary>
    public AttackType GetCurrentAttackType() => currentAttackType;

    /// <summary>공격 강제 취소</summary>
    public void CancelAllAttacks()
    {
        groundAttack?.CancelGroundAttack();
        //rushAttack?.CancelRushAttack();
        currentAttackType = AttackType.None;
    }

    /// <summary>디버그 기즈모</summary>
    private void OnDrawGizmosSelected()
    {
        // 현재 락온된 타겟 표시
        Transform target = GetCurrentLockedTarget();
        if (target != null)
        {
            Gizmos.color = IsValidRushTarget(target) ? Color.red : Color.green;
            Gizmos.DrawLine(transform.position, target.position);
            Gizmos.DrawWireSphere(target.position, 1f);
        }
    }
}
