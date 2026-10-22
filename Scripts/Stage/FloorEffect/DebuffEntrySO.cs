using UnityEngine;

/// <summary>
/// 개별 디버프 데이터
/// - 기존: StatDebuff, DamageOnHit, SkillSeal, CurrencyLoss
/// - 추가: DamageTakenIncrease, DamageDealtReduction, PeriodicDamage,
///         CooldownIncrease, BattleStartDamage, GoldGainReduction
/// </summary>
[CreateAssetMenu(fileName = "Debuff_", menuName = "Game/Debuff Entry")]
public class DebuffEntrySO : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("디버프 이름")]
    public string debuffName = "새 디버프";

    [Tooltip("디버프 설명")]
    [TextArea(2, 4)]
    public string description = "디버프 설명을 입력하세요.";

    [Tooltip("UI 아이콘")]
    public Sprite icon;

    [Header("디버프 타입")]
    public DebuffType debuffType = DebuffType.StatDebuff;

    [Header("지속 타입")]
    [Tooltip("챕터 끝까지 / 영구 / n층 / n마리 처치")]
    public EffectDurationType durationType = EffectDurationType.UntilChapterEnd;

    [Header("층 수 해제 조건 (UntilFloorCount 전용)")]
    [Tooltip("해제까지 클리어해야 할 층 수 (2~4)")]
    [Range(2, 4)]
    public int requiredFloorCount = 2;

    [Header("처치 수 해제 조건 (UntilKillCount 전용)")]
    [Tooltip("해제까지 처치해야 할 몬스터 수")]
    public int requiredKillCount = 30;

    // ============================================================
    // 기존 디버프 필드
    // ============================================================

    [Header("스탯 감소 (StatDebuff)")]
    [Tooltip("적용할 스탯 효과 (FloorEffectSO, 음수값)")]
    public FloorEffectSO statEffect;

    [Header("피격 추가 데미지 (DamageOnHit)")]
    [Tooltip("피격 시 추가 고정 데미지")]
    public int damageOnHit = 5;

    [Header("스킬 봉인 (SkillSeal)")]
    [Tooltip("봉인할 스킬 개수 (Q/E/R 중 랜덤)")]
    [Range(1, 3)]
    public int sealSlotCount = 1;

    [Header("재화 손실 (CurrencyLoss)")]
    [Tooltip("손실 재화 종류")]
    public CurrencyType currencyLossType = CurrencyType.Gold;

    [Tooltip("손실 비율 (%, 예: 30 = 현재 보유량의 30%)")]
    [Range(0, 100)]
    public int lossPercent = 30;

    // ============================================================
    // 금서의 잔재 전용 디버프 필드
    // ============================================================

    [Header("받는 피해 증가 (DamageTakenIncrease) — 종이 갑옷")]
    [Tooltip("받는 모든 피해 증가율 (%)")]
    public float damageTakenIncreasePercent = 15f;

    [Header("주는 피해 감소 (DamageDealtReduction) — 바랜 의지")]
    [Tooltip("주는 모든 피해 감소율 (%)")]
    public float damageDealtReductionPercent = 10f;

    [Header("지속 피해 (PeriodicDamage) — 부식하는 문장")]
    [Tooltip("피해 적용 간격 (초)")]
    public float periodicInterval = 5f;

    [Tooltip("현재 체력 대비 피해 비율 (%)")]
    public float periodicDamagePercent = 1f;

    [Header("쿨타임 증가 (CooldownIncrease) — 얼어붙은 잉크")]
    [Tooltip("스킬 쿨타임 증가율 (%)")]
    public float cooldownIncreasePercent = 15f;

    [Header("전투 시작 피해 (BattleStartDamage) — 금이 간 표지")]
    [Tooltip("전투 시작 시 최대 체력 대비 피해 비율 (%)")]
    public float battleStartDamagePercent = 3f;

    [Header("골드 획득 감소 (GoldGainReduction) — 빚진 지식")]
    [Tooltip("골드 획득량 감소율 (%)")]
    public float goldGainReductionPercent = 15f;

    /// <summary>
    /// 디버프 적용 — 타입별 분기 처리
    /// </summary>
    public void Apply()
    {
        switch (debuffType)
        {
            // === 기존 ===
            case DebuffType.StatDebuff:
                ApplyStatDebuff();
                break;
            case DebuffType.DamageOnHit:
                ApplyDamageOnHit();
                break;
            case DebuffType.SkillSeal:
                ApplySkillSeal();
                break;
            case DebuffType.CurrencyLoss:
                ApplyCurrencyLoss();
                break;

            // === 금서의 잔재 전용 ===
            case DebuffType.DamageTakenIncrease:
                RewardManager.Instance?.AddDamageTakenIncrease(damageTakenIncreasePercent);
                break;
            case DebuffType.DamageDealtReduction:
                RewardManager.Instance?.AddDamageDealtReduction(damageDealtReductionPercent);
                break;
            case DebuffType.PeriodicDamage:
                RewardManager.Instance?.AddPeriodicDamage(periodicInterval, periodicDamagePercent);
                break;
            case DebuffType.CooldownIncrease:
                RewardManager.Instance?.AddCooldownIncrease(cooldownIncreasePercent);
                break;
            case DebuffType.BattleStartDamage:
                RewardManager.Instance?.AddBattleStartDamage(battleStartDamagePercent);
                break;
            case DebuffType.GoldGainReduction:
                RewardManager.Instance?.AddGoldGainReduction(goldGainReductionPercent);
                break;
        }

        Debug.Log($"[DebuffEntry] 디버프 적용: {debuffName}");
    }

    #region 기존 Apply 메서드
    private void ApplyStatDebuff()
    {
        if (statEffect == null)
        {
            Debug.LogWarning($"[DebuffEntry] {debuffName}: statEffect가 없습니다.");
            return;
        }

        FloorEffectManager.Instance?.ApplyEffect(statEffect);
    }

    private void ApplyDamageOnHit()
    {
        RewardManager.Instance?.AddDamageOnHit(damageOnHit, durationType);
        Debug.Log($"[DebuffEntry] 피격 시 추가 데미지: +{damageOnHit}");
    }

    private void ApplySkillSeal()
    {
        RewardManager.Instance?.SealRandomSkills(sealSlotCount, durationType);
        Debug.Log($"[DebuffEntry] 스킬 {sealSlotCount}개 봉인");
    }

    private void ApplyCurrencyLoss()
    {
        if (PlayerStats.Instance == null) return;

        int currentAmount = 0;
        int lossAmount = 0;

        switch (currencyLossType)
        {
            case CurrencyType.Gold:
                currentAmount = PlayerStats.Instance.Coin;
                lossAmount = Mathf.RoundToInt(currentAmount * (lossPercent / 100f));
                PlayerStats.Instance.UseCoin(lossAmount);
                Debug.Log($"[DebuffEntry] 골드 -{lossAmount} ({lossPercent}%)");
                break;

            case CurrencyType.Stardust:
                currentAmount = PlayerStats.Instance.Stardust;
                lossAmount = Mathf.RoundToInt(currentAmount * (lossPercent / 100f));
                PlayerStats.Instance.UseStardust(lossAmount);
                Debug.Log($"[DebuffEntry] 별가루 -{lossAmount} ({lossPercent}%)");
                break;
        }
    }
    #endregion

    /// <summary>
    /// 디버프 요약 텍스트
    /// </summary>
    public string GetSummaryText()
    {
        switch (debuffType)
        {
            case DebuffType.StatDebuff:
                return statEffect != null ? statEffect.GetSummaryText() : "스탯 감소";
            case DebuffType.DamageOnHit:
                return $"피격 시 +{damageOnHit} 추가 데미지";
            case DebuffType.SkillSeal:
                return $"스킬 {sealSlotCount}개 봉인";
            case DebuffType.CurrencyLoss:
                string currencyName = currencyLossType == CurrencyType.Gold ? "골드" : "별가루";
                return $"{currencyName} -{lossPercent}%";
            case DebuffType.DamageTakenIncrease:
                return $"받는 피해 +{damageTakenIncreasePercent}%";
            case DebuffType.DamageDealtReduction:
                return $"주는 피해 -{damageDealtReductionPercent}%";
            case DebuffType.PeriodicDamage:
                return $"{periodicInterval}초마다 현재 체력의 {periodicDamagePercent}% 피해";
            case DebuffType.CooldownIncrease:
                return $"스킬 쿨타임 +{cooldownIncreasePercent}%";
            case DebuffType.BattleStartDamage:
                return $"전투 시작 시 최대 체력의 {battleStartDamagePercent}% 피해";
            case DebuffType.GoldGainReduction:
                return $"골드 획득량 -{goldGainReductionPercent}%";
            default:
                return description;
        }
    }
}
