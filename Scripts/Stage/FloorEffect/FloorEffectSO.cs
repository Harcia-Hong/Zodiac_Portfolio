using UnityEngine;

/// <summary>
/// 효과 지속 타입
/// - 기존: UntilChapterEnd, Permanent
/// - 추가: UntilNextFloor, UntilKillCount
/// </summary>
public enum EffectDurationType
{
    UntilChapterEnd,    // 챕터 끝까지
    Permanent,          // 영구 (런 끝까지)
    UntilFloorCount,    // n층 클리어 시 해제 (최소 2, 최대 4)
    UntilKillCount      // 일정 마리수 처치 시 해제
}

/// <summary>
/// 변경할 스탯 종류
/// - 추가: AttackSpeed (공격 속도)
/// </summary>
public enum StatType
{
    MaxHP,
    AttackPower,
    SpellPower,
    AttackSpeed,        // 공격 속도 (떨리는 손 디버프 등에 사용)
    CriticalRate,
    MoveSpeed,
    CooldownReduction
}

/// <summary>
/// 스탯 변화 데이터
/// </summary>
[System.Serializable]
public class StatModifier
{
    [Tooltip("변경할 스탯")]
    public StatType statType;

    [Tooltip("변경 방식: true = 퍼센트, false = 고정값")]
    public bool isPercent = false;

    [Tooltip("변경 수치 (음수 = 감소)")]
    public float value;
}

/// <summary>
/// 제단에서 획득하는 버프/디버프 데이터
/// </summary>
[CreateAssetMenu(fileName = "FloorEffect_", menuName = "Game/Floor Effect")]
public class FloorEffectSO : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("효과 이름")]
    public string effectName = "새 효과";

    [Tooltip("효과 설명")]
    [TextArea(2, 4)]
    public string description = "효과 설명을 입력하세요.";

    [Tooltip("UI 아이콘")]
    public Sprite icon;

    [Header("효과 타입")]
    [Tooltip("디버프 여부 (true = 디버프)")]
    public bool isDebuff = false;

    [Tooltip("지속 타입")]
    public EffectDurationType durationType = EffectDurationType.UntilChapterEnd;

    [Header("층 수 해제 조건 (UntilFloorCount 전용)")]
    [Tooltip("이 효과가 해제되기까지 클리어해야 할 층 수 (2~4)")]
    [Range(2, 4)]
    public int requiredFloorCount = 2;

    [Header("처치 수 해제 조건 (UntilKillCount 전용)")]
    [Tooltip("이 효과가 해제되기 위한 처치 수")]
    public int requiredKillCount = 30;

    [Header("스탯 변화")]
    [Tooltip("적용할 스탯 변화 목록")]
    public StatModifier[] statModifiers;

    /// <summary>
    /// 효과 요약 텍스트 생성
    /// </summary>
    public string GetSummaryText()
    {
        string summary = "";

        foreach (var mod in statModifiers)
        {
            string sign = mod.value >= 0 ? "+" : "";
            string unit = mod.isPercent ? "%" : "";
            string statName = GetStatName(mod.statType);

            summary += $"{statName} {sign}{mod.value}{unit}\n";
        }

        return summary.TrimEnd('\n');
    }

    /// <summary>
    /// 스탯 이름 반환
    /// </summary>
    private string GetStatName(StatType type)
    {
        switch (type)
        {
            case StatType.MaxHP: return "최대 HP";
            case StatType.AttackPower: return "공격력";
            case StatType.SpellPower: return "주문력";
            case StatType.AttackSpeed: return "공격 속도";
            case StatType.CriticalRate: return "치명타 확률";
            case StatType.MoveSpeed: return "이동속도";
            case StatType.CooldownReduction: return "쿨타임 감소";
            default: return type.ToString();
        }
    }
}
