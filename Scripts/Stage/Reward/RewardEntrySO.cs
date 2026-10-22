using UnityEngine;

/// <summary>
/// 개별 보상 데이터
/// - 스탯 버프, 재화, 스킬 드롭, 회복 등
/// </summary>
[CreateAssetMenu(fileName = "Reward_", menuName = "Game/Reward Entry")]
public class RewardEntrySO : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("보상 이름")]
    public string rewardName = "새 보상";

    [Tooltip("보상 설명")]
    [TextArea(2, 4)]
    public string description = "보상 설명을 입력하세요.";

    [Tooltip("UI 아이콘")]
    public Sprite icon;

    [Header("보상 타입")]
    public RewardType rewardType = RewardType.StatBuff;

    [Header("스탯 버프 (RewardType.StatBuff)")]
    [Tooltip("적용할 스탯 효과 (FloorEffectSO)")]
    public FloorEffectSO statEffect;

    [Header("재화 (RewardType.Currency)")]
    [Tooltip("재화 종류")]
    public CurrencyType currencyType = CurrencyType.Gold;

    [Tooltip("지급량 (고정값)")]
    public int currencyAmount = 100;

    [Tooltip("지급량 범위 사용 여부")]
    public bool useRandomAmount = false;

    [Tooltip("최소 지급량")]
    public int minAmount = 50;

    [Tooltip("최대 지급량")]
    public int maxAmount = 150;

    [Header("스킬 드롭 (RewardType.SkillDrop)")]
    [Tooltip("드롭할 스킬 등급")]
    public SkillGrade skillGrade = SkillGrade.Common;

    [Tooltip("스킬 루트 테이블 (해당 등급 스킬 풀)")]
    public SkillLootTable skillLootTable;

    [Header("회복 (RewardType.Heal)")]
    [Tooltip("회복량 (% 기준, 예: 30 = 최대HP의 30%)")]
    [Range(0, 100)]
    public int healPercent = 30;

    /// <summary>
    /// 보상 적용
    /// </summary>
    public void Apply()
    {
        switch (rewardType)
        {
            case RewardType.StatBuff:
                ApplyStatBuff();
                break;

            case RewardType.Currency:
                ApplyCurrency();
                break;

            case RewardType.SkillDrop:
                ApplySkillDrop();
                break;

            case RewardType.Heal:
                ApplyHeal();
                break;
        }

        Debug.Log($"[RewardEntry] 보상 적용: {rewardName}");
    }

    #region Private Methods
    private void ApplyStatBuff()
    {
        if (statEffect == null)
        {
            Debug.LogWarning($"[RewardEntry] {rewardName}: statEffect가 없습니다.");
            return;
        }

        FloorEffectManager.Instance?.ApplyEffect(statEffect);
    }

    private void ApplyCurrency()
    {
        int amount = useRandomAmount
            ? Random.Range(minAmount, maxAmount + 1)
            : currencyAmount;

        switch (currencyType)
        {
            case CurrencyType.Gold:
                PlayerStats.Instance?.AddCoin(amount);
                Debug.Log($"[RewardEntry] 골드 +{amount}");
                break;

            case CurrencyType.Stardust:
                PlayerStats.Instance?.AddStardust(amount);
                Debug.Log($"[RewardEntry] 별가루 +{amount}");
                break;
        }
    }

    private void ApplySkillDrop()
    {
        if (skillLootTable == null)
        {
            Debug.LogWarning($"[RewardEntry] {rewardName}: skillLootTable이 없습니다.");
            return;
        }

        SkillData skill = skillLootTable.GetRandomSkill();
        if (skill != null)
        {
            // 스킬 자동 장착 시도
            bool equipped = SkillManager.Instance?.AutoEquipSkill(skill) ?? false;

            if (!equipped)
            {
                // 장착 실패 시 바닥에 드롭 (TODO: 드롭 로직 연동)
                Debug.Log($"[RewardEntry] 스킬 슬롯 가득 참 - {skill.skillName} 드롭");
            }
            else
            {
                Debug.Log($"[RewardEntry] 스킬 장착: {skill.skillName}");
            }
        }
    }

    private void ApplyHeal()
    {
        if (PlayerStats.Instance == null) return;

        int maxHP = PlayerStats.Instance.MaxHP;
        int healAmount = Mathf.RoundToInt(maxHP * (healPercent / 100f));

        PlayerStats.Instance.Heal(healAmount);
        Debug.Log($"[RewardEntry] HP 회복: +{healAmount} ({healPercent}%)");
    }
    #endregion

    /// <summary>
    /// 보상 요약 텍스트
    /// </summary>
    public string GetSummaryText()
    {
        switch (rewardType)
        {
            case RewardType.StatBuff:
                return statEffect != null ? statEffect.GetSummaryText() : "스탯 버프";

            case RewardType.Currency:
                string currencyName = currencyType == CurrencyType.Gold ? "골드" : "별가루";
                if (useRandomAmount)
                    return $"{currencyName} +{minAmount}~{maxAmount}";
                else
                    return $"{currencyName} +{currencyAmount}";

            case RewardType.SkillDrop:
                return $"{skillGrade} 등급 스킬";

            case RewardType.Heal:
                return $"HP {healPercent}% 회복";

            default:
                return description;
        }
    }
}
