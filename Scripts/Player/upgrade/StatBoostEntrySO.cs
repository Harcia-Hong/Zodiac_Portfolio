using UnityEngine;

/// <summary>
/// 흩어진 서판 — 개별 스탯 강화 선택지 데이터
/// 인스펙터에서 등급별 수치를 배열로 설정
/// 
/// 사용 예:
///   "꺾이지 않는 서약" → StatType.MaxHP, values = [15, 30, 50, 80]
///   "날카로운 필체"     → StatType.AttackPower, values = [3, 6, 10, 16]
/// </summary>
[CreateAssetMenu(fileName = "StatBoost_", menuName = "Game/Stat Boost Entry")]
public class StatBoostEntrySO : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("선택지 이름 (예: 꺾이지 않는 서약)")]
    public string boostName = "새 서판";

    [Tooltip("선택지 설명")]
    [TextArea(1, 3)]
    public string description = "";

    [Tooltip("UI 아이콘")]
    public Sprite icon;

    [Header("스탯 설정")]
    [Tooltip("강화할 스탯 종류")]
    public StatType statType = StatType.MaxHP;

    [Tooltip("고정값인지 퍼센트인지")]
    public bool isPercent = false;

    [Header("등급별 수치 (Common/Uncommon/Rare/Legendary 순서)")]
    [Tooltip("배열 크기 4 — [흰색, 초록, 보라, 빨강]")]
    public float[] valuesPerGrade = new float[4];

    /// <summary>
    /// 등급에 해당하는 수치 반환
    /// </summary>
    public float GetValue(StatBoostGrade grade)
    {
        int index = (int)grade;
        if (valuesPerGrade == null || index >= valuesPerGrade.Length)
            return 0f;

        return valuesPerGrade[index];
    }

    /// <summary>
    /// 등급에 해당하는 수치로 스탯 적용
    /// </summary>
    public void Apply(StatBoostGrade grade)
    {
        if (PlayerStats.Instance == null) return;

        float value = GetValue(grade);
        PlayerStats player = PlayerStats.Instance;

        // isPercent일 경우 현재 스탯 기준으로 실제 증가량 계산
        float actualValue = value;
        if (isPercent)
        {
            float currentStat = GetCurrentStat(player);
            actualValue = currentStat * (value / 100f);
        }

        switch (statType)
        {
            case StatType.MaxHP:
                player.AddMaxHP(Mathf.RoundToInt(actualValue));
                player.Heal(Mathf.RoundToInt(actualValue));
                break;
            case StatType.AttackPower:
                player.AddAttackPower(Mathf.RoundToInt(actualValue));
                break;
            case StatType.SpellPower:
                player.AddSpellPower(Mathf.RoundToInt(actualValue));
                break;
            case StatType.AttackSpeed:
                player.AddAttackSpeed(actualValue);
                break;
            case StatType.CriticalRate:
                player.AddCriticalRate(actualValue);
                break;
            case StatType.MoveSpeed:
                player.AddMoveSpeed(actualValue);
                break;
            case StatType.CooldownReduction:
                player.AddCooldownReduction(actualValue);
                break;
        }

        Debug.Log($"[StatBoost] '{boostName}' 적용 — {statType} +{actualValue} ({grade})");
    }

    private float GetCurrentStat(PlayerStats player)
    {
        switch (statType)
        {
            case StatType.MaxHP: return player.MaxHP;
            case StatType.AttackPower: return player.AttackPower;
            case StatType.SpellPower: return player.SpellPower;
            case StatType.AttackSpeed: return player.AttackSpeed;
            case StatType.CriticalRate: return player.CriticalRate;
            case StatType.MoveSpeed: return player.MoveSpeed;
            case StatType.CooldownReduction: return player.CooldownReduction;
            default: return 0f;
        }
    }


    /// <summary>
    /// 표시용 텍스트 생성
    /// </summary>
    public string GetDisplayText(StatBoostGrade grade)
    {
        float value = GetValue(grade);
        string sign = value >= 0 ? "+" : "";
        string unit = isPercent ? "%" : "";
        string statName = GetStatName();

        return $"{statName} {sign}{value}{unit}";
    }

    /// <summary>
    /// 스탯 이름 반환
    /// </summary>
    private string GetStatName()
    {
        switch (statType)
        {
            case StatType.MaxHP: return "최대 HP";
            case StatType.AttackPower: return "공격력";
            case StatType.SpellPower: return "주문력";
            case StatType.AttackSpeed: return "공격 속도";
            case StatType.CriticalRate: return "치명타 확률";
            case StatType.MoveSpeed: return "이동속도";
            case StatType.CooldownReduction: return "쿨타임 감소";
            default: return statType.ToString();
        }
    }

    /// <summary>
    /// 등급별 색상 반환 (UI용)
    /// </summary>
    public static Color GetGradeColor(StatBoostGrade grade)
    {
        switch (grade)
        {
            case StatBoostGrade.Common: return Color.white;
            case StatBoostGrade.Uncommon: return new Color(0.2f, 0.8f, 0.2f);
            case StatBoostGrade.Rare: return new Color(0.6f, 0.3f, 0.9f);
            case StatBoostGrade.Legendary: return new Color(1f, 0.2f, 0.2f);
            default: return Color.white;
        }
    }
}
