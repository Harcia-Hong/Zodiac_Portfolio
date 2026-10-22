using UnityEngine;

/// <summary>
/// Enemy 데이터 ScriptableObject
/// 각 Enemy 타입별 설정값을 에셋으로 관리
/// Inspector에서 쉽게 조정 가능하고, 런타임에 변경되지 않음
/// </summary>
[CreateAssetMenu(fileName = "EnemyData", menuName = "Enemy/Enemy Data")]
public class EnemySO : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("적의 이름 (디버깅용)")]
    public string enemyName = "Basic Enemy";

    [Tooltip("적 타입 (Standard/Charge/Range/Hybrid)")]
    public EnemyType enemyType = EnemyType.Standard;

    [Header("AI 패턴 설정")]
    [Tooltip("최초 조우 시 후퇴할지 여부")]
    public bool canRetreatOnFirstEncounter = false;
    public bool enableHitStun = false;

    [Header("이동 설정")]
    [Tooltip("기본 이동 속도")]
    public float baseSpeed = 3.5f;

    [Tooltip("회전 속도 (높을수록 빠른 회전)")]
    public float baseRotationDamping = 10f;

    [Header("탐지 설정")]
    [Tooltip("플레이어 탐지 범위")]
    public float detectRadius = 10f;

    [Tooltip("플레이어 추적 범위 (탐지보다 약간 넓어야 함)")]
    public float chasingRange = 15f;

    [Tooltip("추적 포기 범위 (한번 추적 시작하면 이 범위까지 유지)")]
    public float leashRange = 50f;

    [Header("근거리 공격 설정")]
    [Tooltip("근거리 공격 사거리")]
    public float meleeAttackRange = 2f;

    [Tooltip("근거리 공격력")]
    public int meleeDamage = 10;

    [Tooltip("근거리 공격 쿨타임")]
    public float meleeAttackCooldown = 1.5f;

    [Header("원거리 공격 설정")]
    [Tooltip("원거리 공격 사거리")]
    public float rangedAttackRange = 12f;

    [Tooltip("원거리 공격력")]
    public int rangedDamage = 8;

    [Tooltip("원거리 공격 쿨타임")]
    public float rangedAttackCooldown = 2f;

    [Tooltip("투사체 속도")]
    public float projectileSpeed = 10f;

    [Header("공통 전투 설정")]
    [Tooltip("공격 시 가하는 넉백 힘")]
    public float attackForce = 5f;

    [Header("혼합형 전용 설정")]
    [Tooltip("근거리/원거리 전환 기준 거리 (이보다 가까우면 근거리 공격)")]
    public float hybridSwitchDistance = 5f;

    [Header("체력 설정")]
    [Tooltip("최대 체력")]
    public int maxHealth = 100;

    [Header("애니메이션 타이밍")]
    [Tooltip("공격 애니메이션 중 실제 힘이 가해지는 시점 (0~1)")]
    public float forceTransitionTime = 0.3f;

    [Tooltip("데미지 판정 시작 시점 (0~1)")]
    public float dealingStartTransitionTime = 0.5f;

    [Tooltip("데미지 판정 종료 시점 (0~1)")]
    public float dealingEndTransitionTime = 0.8f;

    [Header("특수 행동 패턴")]
    [Tooltip("피격 시 경직(애니메이션) 발생 여부")]
    public bool useHitReaction = false;

    [Tooltip("첫 조우 시 거리 벌리기(뒷걸음질) 사용 여부")]
    public bool useInitialRetreat = false;

    [Tooltip("거리 벌리기 확률 (0.0 ~ 1.0)")]
    public float retreatChance = 0.5f;

    // ============================================================
    // Layout 간 이동 능력 (누적형 아레나 전용)
    // ============================================================

    [Header("Layout 간 이동 능력")]
    [Tooltip("Layout 사이 gap을 점프로 넘을 수 있는지 여부.\n" +
             "true: NavMesh 경로 실패 시 DOJump로 플레이어 쪽 Layout으로 점프 추격.\n" +
             "false: 점프 불가. 원거리 공격으로 압박하거나 제자리 대기.\n\n" +
             "예시 — 거미/늑대: true, 골렘/원거리 전용: false")]
    public bool canJumpGap = true;

    [Tooltip("점프 높이 (DOJump의 jumpPower). 값이 클수록 높게 뜀")]
    public float gapJumpHeight = 3f;

    [Tooltip("점프 소요 시간 (초). 짧을수록 빠르게 착지")]
    public float gapJumpDuration = 0.6f;

    [Tooltip("점프 후 재사용 대기 시간 (초). 연속 점프 방지")]
    public float gapJumpCooldown = 2f;

    // ============================================================
    // 호환성 프로퍼티
    // ============================================================

    public float attackRange => meleeAttackRange;
    public int damage => meleeDamage;
    public float attackCooldown => meleeAttackCooldown;

    // ============================================================
    // 데이터 유효성 검증
    // ============================================================

    private void OnValidate()
    {
        // 이동 설정
        baseSpeed = Mathf.Max(0.1f, baseSpeed);
        baseRotationDamping = Mathf.Max(0.1f, baseRotationDamping);

        // 탐지 설정
        detectRadius = Mathf.Max(1f, detectRadius);
        chasingRange = Mathf.Max(detectRadius, chasingRange);
        leashRange = Mathf.Max(chasingRange, leashRange);

        // 근거리 공격 설정
        meleeAttackRange = Mathf.Max(0.5f, meleeAttackRange);
        meleeDamage = Mathf.Max(1, meleeDamage);
        meleeAttackCooldown = Mathf.Max(0.1f, meleeAttackCooldown);

        // 원거리 공격 설정    
        rangedAttackRange = Mathf.Max(1f, rangedAttackRange);
        rangedDamage = Mathf.Max(1, rangedDamage);
        rangedAttackCooldown = Mathf.Max(0.1f, rangedAttackCooldown);
        projectileSpeed = Mathf.Max(1f, projectileSpeed);

        // 공통 설정
        attackForce = Mathf.Max(0f, attackForce);
        hybridSwitchDistance = Mathf.Max(0.5f, hybridSwitchDistance);
        maxHealth = Mathf.Max(1, maxHealth);

        // 애니메이션 타이밍 (0~1 범위)
        forceTransitionTime = Mathf.Clamp01(forceTransitionTime);
        dealingStartTransitionTime = Mathf.Clamp01(dealingStartTransitionTime);
        dealingEndTransitionTime = Mathf.Clamp01(dealingEndTransitionTime);

        retreatChance = Mathf.Clamp01(retreatChance);

        // Layout 간 이동 능력
        gapJumpHeight = Mathf.Max(0.5f, gapJumpHeight);
        gapJumpDuration = Mathf.Max(0.1f, gapJumpDuration);
        gapJumpCooldown = Mathf.Max(0f, gapJumpCooldown);
    }
}
