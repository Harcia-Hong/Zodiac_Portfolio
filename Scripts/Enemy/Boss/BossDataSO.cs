using UnityEngine;

/// <summary>
/// 보스 페이즈별 데이터
/// - 각 페이즈의 진입 조건, 사용 패턴, 강화 배율을 정의
/// - 인스펙터에서 배열로 관리 (페이즈 1, 2, 3...)
/// </summary>
[System.Serializable]
public class BossPhaseData
{
    [Header("페이즈 진입 조건")]
    [Tooltip("이 페이즈에 진입하는 체력 비율 (1.0 = 100%, 0.66 = 66%, 0.33 = 33%)")]
    [Range(0f, 1f)]
    public float healthThreshold = 1.0f;

    [Header("사용 가능한 공격 패턴")]
    [Tooltip("이 페이즈에서 사용할 공격 패턴 목록")]
    public BossAttackPatternSO[] attackPatterns;

    [Header("강화 배율")]
    [Tooltip("이 페이즈의 데미지 배율 (1.0 = 기본, 1.5 = 50% 증가)")]
    public float damageMultiplier = 1.0f;

    [Tooltip("이 페이즈의 공격 속도 배율 (1.0 = 기본, 1.3 = 30% 빨라짐)")]
    public float speedMultiplier = 1.0f;

    [Tooltip("이 페이즈의 이동 속도 배율")]
    public float moveSpeedMultiplier = 1.0f;

    [Header("페이즈 전환 연출")]
    [Tooltip("이 페이즈 진입 시 무적 상태에서 시전할 강력한 기술 패턴 (첫 페이즈는 null)")]
    public BossAttackPatternSO phaseTransitionPattern;

    [Tooltip("페이즈 전환 시 재생할 VFX 프리팹")]
    public GameObject transitionVFXPrefab;

    [Tooltip("무적 지속 시간 (전환 연출 동안)")]
    public float invincibleDuration = 3.0f;

    [Header("천벌 패턴 (Rain Attack)")]
    [Tooltip("이 페이즈 전환 시 천벌 패턴 사용 여부")]
    public bool useRainAttack = false;

    [Tooltip("그로기 지속 시간 (천벌 전 멈춤)")]
    public float groggyDuration = 1.5f;

    [Tooltip("보스 사라지는 VFX 프리팹")]
    public GameObject disappearVFXPrefab;

    [Tooltip("보스 나타나는 VFX 프리팹")]
    public GameObject appearVFXPrefab;

    [Tooltip("그로기 애니메이션 트리거")]
    public string groggyAnimTrigger = "doGroggy";
}

/// <summary>
/// 보스 전체 데이터 ScriptableObject
/// - 보스 1마리의 모든 설정을 에셋 하나로 관리
/// - 이름, 페이즈 구성, 전투 수치, 보상 등을 포함
/// - 인스펙터에서 직관적으로 세팅 가능
/// </summary>
[CreateAssetMenu(fileName = "BossData", menuName = "Boss/Boss Data")]
public class BossDataSO : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("보스 이름")]
    public string bossName = "보스";

    [Tooltip("보스 칭호 (예: 무상의 수호자, 조화의 반목자)")]
    public string bossTitle = "";

    [Header("페이즈 설정")]
    [Tooltip("페이즈 목록 (인덱스 0 = 1페이즈, 1 = 2페이즈...)")]
    public BossPhaseData[] phases;

    [Header("전투 기본값")]
    [Tooltip("기본 이동 속도")]
    public float baseMoveSpeed = 4.0f;

    [Tooltip("회전 속도")]
    public float rotationDamping = 10f;

    [Tooltip("플레이어 탐지 범위")]
    public float detectRadius = 20f;

    [Tooltip("플레이어 추적 범위")]
    public float chasingRange = 30f;

    [Tooltip("추적 포기 범위")]
    public float leashRange = 50f;

    [Header("체력 설정")]
    [Tooltip("최대 체력")]
    public int maxHealth = 5000;

    [Header("패턴 선택 설정")]
    [Tooltip("패턴 실행 후 다음 패턴까지 최소 대기 시간")]
    public float globalCooldown = 1.0f;

    [Tooltip("같은 패턴 연속 사용 방지 여부")]
    public bool preventSamePatternRepeat = true;

    [Header("보상 설정")]
    [Tooltip("처치 시 지급 경험치")]
    public int expReward = 500;

    [Tooltip("처치 시 지급 골드")]
    public int goldReward = 300;

    [Tooltip("처치 시 지급 별가루")]
    public int stardustReward = 50;

    #region 유틸리티 메서드

    /// <summary>
    /// 현재 체력 비율에 해당하는 페이즈 인덱스 반환
    /// - 체력이 높은 쪽부터 확인하여 가장 낮은 임계값을 만족하는 페이즈 선택
    /// </summary>
    /// <param name="healthRatio">현재 체력 비율 (0.0 ~ 1.0)</param>
    /// <returns>해당 페이즈 인덱스 (0부터 시작)</returns>
    public int GetPhaseIndex(float healthRatio)
    {
        if (phases == null || phases.Length == 0) return 0;

        // 뒤에서부터 확인 (가장 낮은 임계값부터)
        // 예: [1.0, 0.66, 0.33] → 체력 50%면 인덱스 1 (66% 페이즈)
        for (int i = phases.Length - 1; i >= 0; i--)
        {
            if (healthRatio <= phases[i].healthThreshold)
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>
    /// 특정 페이즈의 데이터 반환
    /// </summary>
    /// <param name="phaseIndex">페이즈 인덱스</param>
    /// <returns>해당 페이즈 데이터 (범위 초과 시 마지막 페이즈)</returns>
    public BossPhaseData GetPhaseData(int phaseIndex)
    {
        if (phases == null || phases.Length == 0) return null;

        phaseIndex = Mathf.Clamp(phaseIndex, 0, phases.Length - 1);
        return phases[phaseIndex];
    }

    /// <summary>
    /// 총 페이즈 수 반환
    /// </summary>
    public int PhaseCount => phases != null ? phases.Length : 0;

    #endregion

    #region 유효성 검증

    /// <summary>
    /// 인스펙터 값 변경 시 유효성 검증
    /// </summary>
    private void OnValidate()
    {
        // 전투 기본값 검증
        baseMoveSpeed = Mathf.Max(0.1f, baseMoveSpeed);
        rotationDamping = Mathf.Max(0.1f, rotationDamping);
        detectRadius = Mathf.Max(1f, detectRadius);
        chasingRange = Mathf.Max(detectRadius, chasingRange);
        leashRange = Mathf.Max(chasingRange, leashRange);

        // 체력 검증
        maxHealth = Mathf.Max(1, maxHealth);

        // 쿨타임 검증
        globalCooldown = Mathf.Max(0f, globalCooldown);

        // 보상 검증
        expReward = Mathf.Max(0, expReward);
        goldReward = Mathf.Max(0, goldReward);
        stardustReward = Mathf.Max(0, stardustReward);

        // 페이즈 배율 검증
        if (phases != null)
        {
            foreach (var phase in phases)
            {
                phase.damageMultiplier = Mathf.Max(0.1f, phase.damageMultiplier);
                phase.speedMultiplier = Mathf.Max(0.1f, phase.speedMultiplier);
                phase.moveSpeedMultiplier = Mathf.Max(0.1f, phase.moveSpeedMultiplier);
                phase.invincibleDuration = Mathf.Max(0f, phase.invincibleDuration);
            }
        }
    }

    #endregion
}