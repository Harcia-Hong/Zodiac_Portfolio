using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 활성화된 효과 정보 (복구용)
/// </summary>
[System.Serializable]
public class ActiveEffect
{
    public FloorEffectSO effectData;
    public int appliedChapter;
    public int appliedFloor;
    public List<AppliedStat> appliedStats;

    /// <summary>UntilFloorCount 전용: 남은 층 수</summary>
    public int remainingFloorCount;

    /// <summary>UntilKillCount 전용: 남은 처치 수</summary>
    public int remainingKillCount;

    public ActiveEffect(FloorEffectSO data, int chapter, int floor)
    {
        effectData = data;
        appliedChapter = chapter;
        appliedFloor = floor;
        appliedStats = new List<AppliedStat>();
        remainingFloorCount = data.requiredFloorCount;
        remainingKillCount = data.requiredKillCount;
    }
}

/// <summary>
/// 실제 적용된 스탯 수치 (복구용)
/// </summary>
[System.Serializable]
public class AppliedStat
{
    public StatType statType;
    public float appliedValue;

    public AppliedStat(StatType type, float value)
    {
        statType = type;
        appliedValue = value;
    }
}

/// <summary>
/// 층 효과(버프/디버프) 관리
/// - 효과 적용 및 추적
/// - 챕터/층/처치 수 기반 만료 처리
/// </summary>
public class FloorEffectManager : MonoBehaviour
{
    public static FloorEffectManager Instance { get; private set; }

    [Header("활성 효과 목록 (디버깅용)")]
    [SerializeField] private List<ActiveEffect> activeEffects = new List<ActiveEffect>();

    #region Unity Lifecycle
    /// <summary>
    /// 싱글톤 초기화 + DontDestroyOnLoad 처리
    /// - 첫 인스턴스: Instance 등록 + 씬 전환 시 유지
    /// - 중복 인스턴스: 자기 자신 파괴
    ///   → 첫 FloorEffectManager가 살아남아 층 효과 데이터 보존
    /// </summary>
    private void Awake()
    {
        // 이미 인스턴스가 존재하면 자기 자신 파괴
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // 첫 인스턴스 등록
        Instance = this;

        // 씬 전환 시에도 유지 (층 효과 데이터 보존)
        DontDestroyOnLoad(gameObject);
    }
    #endregion

    #region 효과 적용
    /// <summary>
    /// 효과 적용
    /// </summary>
    public void ApplyEffect(FloorEffectSO effectData)
    {
        if (effectData == null)
        {
            Debug.LogWarning("[FloorEffectManager] effectData가 null입니다.");
            return;
        }

        int currentChapter = StageManager.Instance?.GetCurrentChapter() ?? 1;
        int currentFloor = StageManager.Instance?.GetCurrentFloor() ?? 1;

        ActiveEffect activeEffect = new ActiveEffect(effectData, currentChapter, currentFloor);

        foreach (var mod in effectData.statModifiers)
        {
            float appliedValue = ApplyStat(mod);
            activeEffect.appliedStats.Add(new AppliedStat(mod.statType, appliedValue));
        }

        activeEffects.Add(activeEffect);

        Debug.Log($"[FloorEffectManager] 효과 적용: {effectData.effectName} (지속: {effectData.durationType})");
    }
    #endregion

    #region 만료 처리
    /// <summary>
    /// 챕터 종료 시 호출 — UntilChapterEnd 효과 해제
    /// </summary>
    public void OnChapterEnd(int endedChapter)
    {
        List<ActiveEffect> toRemove = new List<ActiveEffect>();

        foreach (var effect in activeEffects)
        {
            if (effect.effectData.durationType == EffectDurationType.UntilChapterEnd &&
                effect.appliedChapter == endedChapter)
            {
                toRemove.Add(effect);
            }
        }

        foreach (var effect in toRemove)
        {
            RemoveEffect(effect);
        }

        Debug.Log($"[FloorEffectManager] 챕터 {endedChapter} 종료 — {toRemove.Count}개 효과 만료");
    }

    /// <summary>
    /// 층 클리어 시 호출 — UntilFloorCount 효과 카운트 감소 및 해제
    /// StageManager.HandleStageCleared()에서 호출
    /// </summary>
    public void OnFloorCleared()
    {
        List<ActiveEffect> toRemove = new List<ActiveEffect>();

        foreach (var effect in activeEffects)
        {
            if (effect.effectData.durationType == EffectDurationType.UntilFloorCount)
            {
                effect.remainingFloorCount--;

                if (effect.remainingFloorCount <= 0)
                {
                    toRemove.Add(effect);
                }
            }
        }

        foreach (var effect in toRemove)
        {
            RemoveEffect(effect);
            Debug.Log($"[FloorEffectManager] '{effect.effectData.effectName}' 층 조건 달성 — 효과 해제!");
        }
    }

    /// <summary>
    /// 적 처치 시 호출 — UntilKillCount 효과 카운트 감소 및 해제
    /// Enemy.HandleDeath()에서 호출
    /// </summary>
    public void OnEnemyKilled()
    {
        List<ActiveEffect> toRemove = new List<ActiveEffect>();

        foreach (var effect in activeEffects)
        {
            if (effect.effectData.durationType == EffectDurationType.UntilKillCount)
            {
                effect.remainingKillCount--;

                if (effect.remainingKillCount <= 0)
                {
                    toRemove.Add(effect);
                }
            }
        }

        foreach (var effect in toRemove)
        {
            RemoveEffect(effect);
            Debug.Log($"[FloorEffectManager] '{effect.effectData.effectName}' 처치 조건 달성 — 효과 해제!");
        }
    }
    #endregion

    #region 조회/초기화
    /// <summary>
    /// 현재 활성 효과 목록 반환
    /// </summary>
    public List<ActiveEffect> GetActiveEffects()
    {
        return activeEffects;
    }

    /// <summary>
    /// 모든 효과 제거 (런 종료 시)
    /// </summary>
    public void ClearAllEffects()
    {
        foreach (var effect in activeEffects)
        {
            RevertStats(effect);
        }

        activeEffects.Clear();
        Debug.Log("[FloorEffectManager] 모든 효과 제거됨");
    }
    #endregion

    #region 스탯 적용/복구
    /// <summary>
    /// 스탯 적용 및 적용된 수치 반환
    /// </summary>
    private float ApplyStat(StatModifier mod)
    {
        PlayerStats player = PlayerStats.Instance;
        if (player == null) return 0f;

        float appliedValue = 0f;

        switch (mod.statType)
        {
            case StatType.MaxHP:
                if (mod.isPercent)
                    appliedValue = Mathf.RoundToInt(player.MaxHP * (mod.value / 100f));
                else
                    appliedValue = mod.value;
                player.AddMaxHP(Mathf.RoundToInt(appliedValue));
                break;

            case StatType.AttackPower:
                if (mod.isPercent)
                    appliedValue = Mathf.RoundToInt(player.AttackPower * (mod.value / 100f));
                else
                    appliedValue = mod.value;
                player.AddAttackPower(Mathf.RoundToInt(appliedValue));
                break;

            case StatType.SpellPower:
                if (mod.isPercent)
                    appliedValue = Mathf.RoundToInt(player.SpellPower * (mod.value / 100f));
                else
                    appliedValue = mod.value;
                player.AddSpellPower(Mathf.RoundToInt(appliedValue));
                break;

            case StatType.AttackSpeed:
                appliedValue = mod.value;
                player.AddAttackSpeed(appliedValue);
                break;

            case StatType.CriticalRate:
                appliedValue = mod.value;
                player.AddCriticalRate(appliedValue);
                break;

            case StatType.MoveSpeed:
                appliedValue = mod.value;
                player.AddMoveSpeed(appliedValue);
                break;

            case StatType.CooldownReduction:
                appliedValue = mod.value;
                player.AddCooldownReduction(appliedValue);
                break;
        }

        return appliedValue;
    }

    /// <summary>
    /// 효과 제거 및 스탯 복구
    /// </summary>
    private void RemoveEffect(ActiveEffect effect)
    {
        RevertStats(effect);
        activeEffects.Remove(effect);

        Debug.Log($"[FloorEffectManager] 효과 제거: {effect.effectData.effectName}");
    }

    /// <summary>
    /// 적용된 스탯 복구 (적용 수치의 반대값으로 되돌림)
    /// </summary>
    private void RevertStats(ActiveEffect effect)
    {
        PlayerStats player = PlayerStats.Instance;
        if (player == null) return;

        foreach (var stat in effect.appliedStats)
        {
            float revertValue = -stat.appliedValue;

            switch (stat.statType)
            {
                case StatType.MaxHP:
                    player.AddMaxHP(Mathf.RoundToInt(revertValue));
                    break;
                case StatType.AttackPower:
                    player.AddAttackPower(Mathf.RoundToInt(revertValue));
                    break;
                case StatType.SpellPower:
                    player.AddSpellPower(Mathf.RoundToInt(revertValue));
                    break;
                case StatType.AttackSpeed:
                    player.AddAttackSpeed(revertValue);
                    break;
                case StatType.CriticalRate:
                    player.AddCriticalRate(revertValue);
                    break;
                case StatType.MoveSpeed:
                    player.AddMoveSpeed(revertValue);
                    break;
                case StatType.CooldownReduction:
                    player.AddCooldownReduction(revertValue);
                    break;
            }
        }
    }
    #endregion

    #region Debug
    [ContextMenu("활성 효과 확인")]
    public void LogActiveEffects()
    {
        Debug.Log("=== 활성 효과 목록 ===");

        if (activeEffects.Count == 0)
        {
            Debug.Log("활성 효과 없음");
            return;
        }

        foreach (var effect in activeEffects)
        {
            string durationInfo = "";
            switch (effect.effectData.durationType)
            {
                case EffectDurationType.UntilFloorCount:
                    durationInfo = $"남은 층: {effect.remainingFloorCount}";
                    break;
                case EffectDurationType.UntilKillCount:
                    durationInfo = $"남은 처치: {effect.remainingKillCount}";
                    break;
                default:
                    durationInfo = effect.effectData.durationType.ToString();
                    break;
            }

            Debug.Log($"- {effect.effectData.effectName} ({durationInfo})");
        }
    }
    #endregion
}
