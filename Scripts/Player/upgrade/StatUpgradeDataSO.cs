using UnityEngine;

/// <summary>
/// 영구 강화 항목 하나의 데이터를 정의하는 ScriptableObject.
/// "지식의 서고"에서 별가루를 소모해 영구적으로 플레이어 스탯을 강화할 때 사용.
/// Unity Inspector에서 각 항목(피해 증가, 최대 체력 등)마다 하나씩 생성.
/// </summary>
[CreateAssetMenu(fileName = "NewStatUpgrade", menuName = "KnowledgeArchive/Stat Upgrade Data")]
public class StatUpgradeDataSO : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("UI에 표시될 강화 항목 이름")]
    public string upgradeName = "새 강화";

    [Tooltip("강화 효과 설명. {0}에 현재 수치가 들어감")]
    [TextArea(2, 4)]
    public string description = "효과 설명을 입력하세요. 현재: {0}";

    [Tooltip("강화 항목이 속한 탭")]
    public UpgradeTabType tabType = UpgradeTabType.Stat;

    [Header("강화 단계")]
    [Tooltip("최대 강화 레벨")]
    public int maxLevel = 5;

    [Tooltip("레벨별 효과 수치 (배열 크기 = maxLevel). 예: [10, 20, 30, 40, 50]")]
    public float[] valuesPerLevel;

    [Tooltip("레벨별 강화 비용 (별가루). 배열 크기 = maxLevel")]
    public int[] stardustCostPerLevel;

    [Header("스탯 적용 설정")]
    [Tooltip("이 강화가 영향을 주는 스탯 타입")]
    public StatUpgradeType statType = StatUpgradeType.AttackPower;

    [Header("해금 조건")]
    [Tooltip("처음부터 보이는지 여부. false면 ???로 표시")]
    public bool isVisibleByDefault = true;

    [Tooltip("이 항목이 보이려면 필요한 총 강화 횟수 (다른 항목들의 강화 합산)")]
    public int requiredTotalUpgrades = 0;

    [Header("표시 순서")]
    [Tooltip("리스트에서의 정렬 순서 (낮을수록 위에 표시)")]
    public int sortOrder = 0;

    /// <summary>
    /// 특정 레벨의 효과 수치를 반환
    /// </summary>
    public float GetValueAtLevel(int level)
    {
        if (level <= 0 || valuesPerLevel == null || valuesPerLevel.Length == 0)
            return 0f;

        int index = Mathf.Clamp(level - 1, 0, valuesPerLevel.Length - 1);
        return valuesPerLevel[index];
    }

    /// <summary>
    /// 특정 레벨로 강화하는 데 필요한 별가루 비용을 반환
    /// </summary>
    public int GetCostForLevel(int level)
    {
        if (level <= 0 || stardustCostPerLevel == null || stardustCostPerLevel.Length == 0)
            return 0;

        int index = Mathf.Clamp(level - 1, 0, stardustCostPerLevel.Length - 1);
        return stardustCostPerLevel[index];
    }

    /// <summary>
    /// 설명 텍스트에 현재 수치를 대입하여 반환
    /// </summary>
    public string GetFormattedDescription(int currentLevel)
    {
        float currentValue = GetValueAtLevel(currentLevel);
        return string.Format(description, currentValue);
    }
}

/// <summary>
/// 강화가 적용되는 스탯 종류
/// </summary>
public enum StatUpgradeType
{
    AttackPower,        // 공격력
    MaxHP,              // 최대 체력
    SpellPower,         // 주문력
    AttackSpeed,        // 공격 속도
    CriticalRate,       // 치명타 확률
    CooldownReduction,  // 쿨타임 감소
    MoveSpeed,          // 이동 속도
    // --- 특수 능력 탭용 ---
    HealOnKill,         // 처치 시 HP 회복
    DashDistance,        // 대시 거리 증가
    InvincibleTime,     // 피격 시 무적 시간
    Revive,             // 부활
    BossDamage,         // 보스 추가 데미지
    GoldBonus,          // 골드 획득량
    DropRate,           // 드랍률 증가
    ExtraDash,          // 대시 횟수 추가
    ExtraSkillSlot,     // 시작 스킬 추가
}

/// <summary>
/// 강화 항목이 속하는 탭 종류
/// </summary>
public enum UpgradeTabType
{
    Stat,       // 기본 스탯 탭
    Special,    // 특수 능력 탭
    Skill       // 스킬 해금 탭
}