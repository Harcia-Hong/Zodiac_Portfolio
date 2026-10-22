using System.Collections.Generic;
using UnityEngine;

public enum SkillStatType
{
    Physical, // 물리 (칼질 등 -> 공격력 기반)
    Magical,  // 마법 (화염구 등 -> 주문력 기반)
    Fixed     // 고정 (스탯 영향 안 받음)
}

public enum SkillGrade { Common, Rare, Epic, Legendary }

/// <summary>
/// 스킬 카테고리 — 어느 스탠스(영혼)가 사용 가능한지 분류.
/// 
/// - Melee: 검(근거리) 스탠스 전용. 활 캐릭터일 때 발동 X.
/// - Ranged: 활(원거리) 스탠스 전용. 검 캐릭터일 때 발동 X.
/// - Common: 두 스탠스 모두 사용 가능 (회피, 폭탄 등 공용 유틸).
/// 
/// SkillManager.TryActivateSkill에서 활성 스탠스와 매칭 체크하여 필터링.
/// </summary>
public enum SkillCategory
{
    Melee,   // 검 전용
    Ranged,  // 활 전용
    Common   // 공용 (두 스탠스 모두 사용 가능)
}

/// <summary>
/// 콤보 단계 하나가 갖는 데이터.
/// 예: 분노 연참 1단(가로베기 25딜) / 2단(세로베기 40딜) / 3단(마무리 55딜) 같은 식으로 각 단계를 정의.
/// 
/// 단계별로 데미지·실행할 액션 리스트·애니메이션 트리거가 모두 따로 설정됨.
/// damageGrowth(SkillData 측 필드)는 모든 단계에 동일하게 더해지는 성장치임.
/// </summary>
[System.Serializable]
public class ComboStage
{
    [Tooltip("이 단계의 1레벨 기준 데미지 (예: 1단=25, 2단=40, 3단=55)")]
    public float damage;

    [Tooltip("이 단계에서 실행할 액션 리스트 (예: DashAction + DamagePathAction 조합).\n" +
             "단계마다 다른 액션·다른 이펙트를 넣어 시각적 차별화 가능.")]
    public List<SkillAction> actions = new List<SkillAction>();

    [Tooltip("(참고용) 이 단계의 식별 이름. 실제 애니메이션 트리거는 액션 내부에서 처리.")]
    public string stageName;
}

[CreateAssetMenu(fileName = "SD_", menuName = "Skill System/Skill Data")]
public class SkillData : ScriptableObject
{
    [Header("기본 정보")]
    public string skillName;
    public Sprite skillIcon;

    [Header("카테고리 분류 (스탠스 사용 제한)")]
    [Tooltip("어느 스탠스(검/활)가 사용 가능한지 분류.\n" +
             "Melee: 검 전용 / Ranged: 활 전용 / Common: 두 스탠스 모두 사용 가능.\n" +
             "기존 스킬은 모두 Common이 기본값 — 검/활 전용 분류는 인스펙터에서 수동 설정.")]
    public SkillCategory category = SkillCategory.Common;

    [Header("설명 텍스트")]
    [TextArea(3, 5)]
    [Tooltip("수치가 들어갈 자리에 {0}을 써주세요.\n예: '적에게 {0}의 피해를 줍니다.'")]
    public string description;

    [Header("데미지 공식 설정")]
    public SkillStatType statType = SkillStatType.Physical;
    public float damageRatio = 1.0f;

    [Header("성능 설정 (이 스킬만의 고유 수치)")]
    public float baseCooldown;      // 1레벨 쿨타임
    public float baseDamage;        // 1레벨 데미지 (콤보 스킬은 사용 안 함 — comboStages가 우선)

    [Header("공격 속도 설정")]
    [Tooltip("체크하면 쿨타임이 (기본쿨 / 공격속도)로 계산됩니다.")]
    public bool useAttackSpeed = false; // 기본 공격용 옵션

    [Header("성장 수치 (레벨업 당 변화량)")]
    public float cooldownReduction; // 레벨업 할 때마다 줄어드는 쿨타임 양
    public float damageGrowth;      // 레벨업 할 때마다 늘어나는 데미지 양 (콤보면 모든 단계에 동일 적용)

    [Header("기타")]
    public SkillGrade grade;
    public HitEffectType hitEffect;
    public bool blockMovement = false;
    public List<SkillAction> actions = new List<SkillAction>();

    // ========================================================================
    // ▼ [NEW] 콤보 시스템 — 일정 시간 안에 N단까지 재시전 가능한 스킬용
    // ========================================================================

    [Header("콤보 시스템 (선택)")]
    [Tooltip("체크하면 콤보 스킬로 동작 — 일정 시간 안에 N단까지 재시전 가능.\n" +
             "체크 시 위의 'baseDamage / actions' 필드는 무시되고, 아래 'comboStages'가 사용됨.")]
    public bool useCombo = false;

    [Tooltip("다음 단계 입력을 받을 수 있는 시간 (초).\n" +
             "이 시간 안에 다시 키를 누르면 다음 단계로 진행, 시간 초과 시 쿨타임 시작.\n" +
             "키를 누를 때마다 이 시간만큼 다시 카운트됨.")]
    public float comboWindow = 3.5f;

    [Tooltip("콤보 단계별 데이터 (1단부터 순서대로 넣을 것).\n" +
             "리스트 길이 = 최대 단계 수. 예: 3개 넣으면 3단까지 시전 가능.")]
    public List<ComboStage> comboStages = new List<ComboStage>();

    [Header("차지 시스템 (선택)")]
    [Tooltip("체크하면 차지 스킬로 동작 — 최대 N발까지 저장 가능, 시간 지나면 자동 회복.\n" +
         "체크 시 baseCooldown은 무시되고 chargeRecoveryTime이 사용됨.\n" +
         "예: 폭렬 미사일 — 3발까지 저장, 6초마다 1발 회복.")]
    public bool useCharges = false;

    [Tooltip("최대로 저장 가능한 차지 수. 시전 1회당 1발 소모.")]
    public int maxCharges = 3;

    [Tooltip("차지 1발 회복에 걸리는 시간 (초).\n" +
             "PlayerStats의 쿨감(CDR) 자동 적용됨.")]
    public float chargeRecoveryTime = 6f;

    [Header("불씨 시스템 (레벨업 강화)")]
    [Tooltip("Lv2 강화 시 등장할 불씨 후보 리스트. 이 중 N장 랜덤 등장 → 1장 픽.\n비어있으면 Lv2 강화 시 카드 없음.")]
    public List<EmberData> level2EmberPool = new List<EmberData>();

    [Tooltip("Lv3 강화 시 등장할 불씨 후보 리스트.")]
    public List<EmberData> level3EmberPool = new List<EmberData>();

    [Tooltip("Lv4 강화 시 등장할 불씨 후보 리스트.")]
    public List<EmberData> level4EmberPool = new List<EmberData>();

    // ========================================================================
    // ▼ [핵심] 레벨에 따른 실제 수치를 계산해주는 함수들
    // ========================================================================

    /// <summary>
    /// "현재 레벨"에 맞는 데미지를 계산.
    /// 일반 스킬용 — baseDamage 기준.
    /// </summary>
    public float GetDamage(int level)
    {
        // 공식: 1레벨 데미지 + (성장치 * (레벨 - 1))
        return baseDamage + (damageGrowth * (level - 1));
    }

    /// <summary>
    /// "현재 레벨"에 맞는 쿨타임을 계산.
    /// </summary>
    public float GetCooldown(int level)
    {
        float currentCool = baseCooldown - (cooldownReduction * (level - 1));

        // [수정] 공속 옵션이 켜져있으면 공속으로 나눔
        if (useAttackSpeed && PlayerStats.Instance != null)
        {
            // 예: 쿨 0.5초 / 공속 2.0 = 0.25초 쿨타임
            return Mathf.Max(0.1f, currentCool / PlayerStats.Instance.AttackSpeed);
        }

        // 일반 스킬은 쿨감(CDR) 적용
        if (PlayerStats.Instance != null)
        {
            return PlayerStats.Instance.GetReducedCooldown(currentCool);
        }

        return Mathf.Max(0.1f, currentCool);
    }

    // ========================================================================
    // ▼ [NEW] 콤보 스킬용 헬퍼 함수들
    // ========================================================================

    /// <summary>
    /// 콤보 스킬의 최대 단계 수 (콤보 아니면 0).
    /// </summary>
    public int ComboStageCount => useCombo ? comboStages.Count : 0;

    /// <summary>
    /// 지정한 단계의 데미지를 레벨 성장치까지 반영해서 계산.
    /// 콤보 스킬이 아니거나 stageIndex가 잘못된 경우 일반 GetDamage로 폴백.
    /// </summary>
    /// <param name="stageIndex">0부터 시작하는 단계 인덱스 (0=1단, 1=2단, ...)</param>
    /// <param name="level">현재 스킬 레벨</param>
    public float GetComboStageDamage(int stageIndex, int level)
    {
        if (!useCombo || stageIndex < 0 || stageIndex >= comboStages.Count)
            return GetDamage(level); // 안전 폴백

        // 콤보 단계 데미지 + 레벨 성장치 (모든 단계 동일 적용)
        return comboStages[stageIndex].damage + (damageGrowth * (level - 1));
    }

    /// <summary>
    /// 지정한 단계에서 실행할 액션 리스트.
    /// 콤보 스킬이 아니거나 stageIndex가 잘못된 경우 일반 actions로 폴백.
    /// </summary>
    public List<SkillAction> GetComboStageActions(int stageIndex)
    {
        if (!useCombo || stageIndex < 0 || stageIndex >= comboStages.Count)
            return actions; // 안전 폴백

        return comboStages[stageIndex].actions;
    }

    /// <summary>
    /// 차지 1발 회복에 걸리는 시간을 레벨/쿨감 반영해 계산.
    /// 차지 스킬이 아니면 baseCooldown 폴백.
    /// </summary>
    public float GetChargeRecoveryTime(int level)
    {
        if (!useCharges) return GetCooldown(level);

        float time = chargeRecoveryTime - (cooldownReduction * (level - 1));

        // PlayerStats의 쿨감 적용 (일반 쿨다운과 동일 로직)
        if (PlayerStats.Instance != null)
        {
            return PlayerStats.Instance.GetReducedCooldown(time);
        }

        return Mathf.Max(0.1f, time);
    }
}
