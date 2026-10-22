using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 활성화된 디버프 정보 (복구용)
/// </summary>
[System.Serializable]
public class ActiveDebuff
{
    public DebuffEntrySO debuffData;
    public int appliedChapter;
    public EffectDurationType durationType;

    // 디버프 타입별 적용된 값
    public int appliedDamageOnHit;
    public List<KeyCode> sealedSkills;
    public float appliedDamageTakenIncrease;
    public float appliedDamageDealtReduction;
    public float appliedCooldownIncrease;
    public float appliedBattleStartDamage;
    public float appliedGoldGainReduction;

    public int remainingFloorCount;
    public int remainingKillCount;

    public ActiveDebuff(DebuffEntrySO data, int chapter)
    {
        debuffData = data;
        appliedChapter = chapter;
        durationType = data.durationType;
        appliedDamageOnHit = 0;
        sealedSkills = new List<KeyCode>();
        appliedDamageTakenIncrease = 0f;
        appliedDamageDealtReduction = 0f;
        appliedCooldownIncrease = 0f;
        appliedBattleStartDamage = 0f;
        appliedGoldGainReduction = 0f;

        remainingFloorCount = data.requiredFloorCount;
        remainingKillCount = data.requiredKillCount;
    }
}

/// <summary>
/// 보상/디버프 관리
/// - 보상 풀에서 보상 적용
/// - 디버프 등록/추적/해제
/// - 다른 시스템에서 Get 메서드로 현재 디버프 수치 조회
/// 
/// 조회 방식 예시:
///   PlayerStats.TakeDamage → RewardManager.GetDamageTakenMultiplier()
///   Weapon.GetDamage → RewardManager.GetDamageDealtMultiplier()
///   SkillManager.StartCooldown → RewardManager.GetCooldownMultiplier()
///   PlayerStats.AddCoin → RewardManager.GetGoldGainMultiplier()
/// </summary>
public class RewardManager : MonoBehaviour
{
    public static RewardManager Instance { get; private set; }

    #region Serialized Fields
    [Header("활성 디버프 목록 (디버깅용)")]
    [SerializeField] private List<ActiveDebuff> activeDebuffs = new List<ActiveDebuff>();

    // === 기존 ===
    [Header("피격 추가 데미지")]
    [SerializeField] private int totalDamageOnHit = 0;

    [Header("봉인된 스킬")]
    [SerializeField] private List<KeyCode> sealedSkillSlots = new List<KeyCode>();

    // === 금서의 잔재 전용 누적 수치 ===
    [Header("받는 피해 증가 (%)")]
    [SerializeField] private float totalDamageTakenIncrease = 0f;

    [Header("주는 피해 감소 (%)")]
    [SerializeField] private float totalDamageDealtReduction = 0f;

    [Header("쿨타임 증가 (%)")]
    [SerializeField] private float totalCooldownIncrease = 0f;

    [Header("전투 시작 피해 — 최대 체력 대비 (%)")]
    [SerializeField] private float totalBattleStartDamage = 0f;

    [Header("골드 획득 감소 (%)")]
    [SerializeField] private float totalGoldGainReduction = 0f;
    #endregion

    #region Properties
    public int TotalDamageOnHit => totalDamageOnHit;
    public List<KeyCode> SealedSkillSlots => sealedSkillSlots;
    #endregion

    // 지속 피해 코루틴 참조 (중복 방지)
    private Coroutine periodicDamageCoroutine;
    private float periodicInterval;
    private float periodicDamagePercent;
    private int periodicDamageStackCount = 0;

    #region Unity Lifecycle
    /// <summary>
    /// 싱글톤 초기화 + DontDestroyOnLoad 처리
    /// - 첫 인스턴스: Instance 등록 + 씬 전환 시 유지
    /// - 중복 인스턴스: 자기 자신 파괴
    ///   → 첫 RewardManager가 살아남아 보상/디버프 데이터 보존
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

        // 씬 전환 시에도 유지 (보상/디버프 데이터 보존)
        DontDestroyOnLoad(gameObject);
    }
    #endregion

    #region 보상 적용
    /// <summary>
    /// 보상 풀에서 보상 + 디버프 적용
    /// </summary>
    public void ApplyRewardFromPool(RewardPoolSO pool)
    {
        if (pool == null)
        {
            Debug.LogWarning("[RewardManager] pool이 null입니다.");
            return;
        }

        RewardEntrySO reward = pool.GetRandomReward();
        if (reward != null)
        {
            reward.Apply();
        }

        DebuffEntrySO[] debuffs = pool.GetRandomDebuffs();
        foreach (var debuff in debuffs)
        {
            if (debuff != null)
            {
                ApplyDebuff(debuff);
            }
        }

        RewardResultUI.Instance?.ShowResults(reward, debuffs);

        Debug.Log($"[RewardManager] {pool.poolName} 보상 적용 완료");
    }

    /// <summary>
    /// 디버프 적용 및 추적 등록
    /// </summary>
    public void ApplyDebuff(DebuffEntrySO debuff)
    {
        if (debuff == null) return;

        int currentChapter = StageManager.Instance?.GetCurrentChapter() ?? 1;
        ActiveDebuff activeDebuff = new ActiveDebuff(debuff, currentChapter);

        // SkillSeal인 경우 이전 상태 저장
        List<KeyCode> previousSealed = debuff.debuffType == DebuffType.SkillSeal
            ? new List<KeyCode>(sealedSkillSlots)
            : null;

        // 디버프 적용 (DebuffEntrySO.Apply 호출)
        debuff.Apply();

        // 타입별 적용 값 추적
        switch (debuff.debuffType)
        {
            case DebuffType.DamageOnHit:
                activeDebuff.appliedDamageOnHit = debuff.damageOnHit;
                break;

            case DebuffType.SkillSeal:
                activeDebuff.sealedSkills = new List<KeyCode>();
                foreach (var slot in sealedSkillSlots)
                {
                    if (!previousSealed.Contains(slot))
                        activeDebuff.sealedSkills.Add(slot);
                }
                break;

            case DebuffType.DamageTakenIncrease:
                activeDebuff.appliedDamageTakenIncrease = debuff.damageTakenIncreasePercent;
                break;

            case DebuffType.DamageDealtReduction:
                activeDebuff.appliedDamageDealtReduction = debuff.damageDealtReductionPercent;
                break;

            case DebuffType.CooldownIncrease:
                activeDebuff.appliedCooldownIncrease = debuff.cooldownIncreasePercent;
                break;

            case DebuffType.BattleStartDamage:
                activeDebuff.appliedBattleStartDamage = debuff.battleStartDamagePercent;
                break;

            case DebuffType.GoldGainReduction:
                activeDebuff.appliedGoldGainReduction = debuff.goldGainReductionPercent;
                break;
        }

        // 추적 대상 디버프만 목록에 추가 (CurrencyLoss는 즉시 적용이라 추적 불필요)
        if (debuff.debuffType != DebuffType.CurrencyLoss)
        {
            activeDebuffs.Add(activeDebuff);
        }

        Debug.Log($"[RewardManager] 디버프 적용: {debuff.debuffName}");
    }
    #endregion

    #region 기존 디버프 등록
    /// <summary>
    /// 피격 시 추가 데미지 등록
    /// </summary>
    public void AddDamageOnHit(int damage, EffectDurationType duration)
    {
        totalDamageOnHit += damage;
        Debug.Log($"[RewardManager] 피격 추가 데미지: +{damage} (총: {totalDamageOnHit})");
    }

    /// <summary>
    /// 피격 시 추가 데미지 제거
    /// </summary>
    public void RemoveDamageOnHit(int damage)
    {
        totalDamageOnHit -= damage;
        if (totalDamageOnHit < 0) totalDamageOnHit = 0;
    }

    /// <summary>
    /// 랜덤 스킬 봉인
    /// </summary>
    public void SealRandomSkills(int count, EffectDurationType duration)
    {
        KeyCode[] availableSlots = { KeyCode.Q, KeyCode.E, KeyCode.R };
        List<KeyCode> canSeal = new List<KeyCode>();

        foreach (var slot in availableSlots)
        {
            if (!sealedSkillSlots.Contains(slot))
                canSeal.Add(slot);
        }

        count = Mathf.Min(count, canSeal.Count);

        for (int i = 0; i < count; i++)
        {
            int index = Random.Range(0, canSeal.Count);
            KeyCode slotToSeal = canSeal[index];
            sealedSkillSlots.Add(slotToSeal);
            canSeal.RemoveAt(index);
        }

        SkillManager.Instance?.OnSkillsSealed(sealedSkillSlots);
    }

    /// <summary>
    /// 스킬 봉인 해제
    /// </summary>
    public void UnsealSkills(List<KeyCode> slotsToUnseal)
    {
        foreach (var slot in slotsToUnseal)
        {
            sealedSkillSlots.Remove(slot);
        }

        SkillManager.Instance?.OnSkillsSealed(sealedSkillSlots);
    }

    /// <summary>
    /// 스킬이 봉인되었는지 확인
    /// </summary>
    public bool IsSkillSealed(KeyCode slot)
    {
        return sealedSkillSlots.Contains(slot);
    }

    /// <summary>
    /// 피격 시 추가 데미지 반환 (PlayerStats에서 호출)
    /// </summary>
    public int GetBonusDamageOnHit()
    {
        return totalDamageOnHit;
    }
    #endregion

    #region 금서의 잔재 전용 — 등록 메서드
    /// <summary>
    /// 받는 피해 증가 등록 (종이 갑옷)
    /// </summary>
    public void AddDamageTakenIncrease(float percent)
    {
        totalDamageTakenIncrease += percent;
        Debug.Log($"[RewardManager] 받는 피해 증가: +{percent}% (총: {totalDamageTakenIncrease}%)");
    }

    /// <summary>
    /// 주는 피해 감소 등록 (바랜 의지)
    /// </summary>
    public void AddDamageDealtReduction(float percent)
    {
        totalDamageDealtReduction += percent;
        Debug.Log($"[RewardManager] 주는 피해 감소: +{percent}% (총: {totalDamageDealtReduction}%)");
    }

    /// <summary>
    /// 지속 피해 등록 (부식하는 문장) — n초마다 현재 체력의 n% 피해
    /// </summary>
    public void AddPeriodicDamage(float interval, float damagePercent)
    {
        periodicDamageStackCount++;
        periodicInterval = interval;
        periodicDamagePercent += damagePercent;

        // 코루틴이 없으면 시작, 있으면 수치만 갱신 (중복 방지)
        if (periodicDamageCoroutine == null)
        {
            periodicDamageCoroutine = StartCoroutine(PeriodicDamageRoutine());
        }

        Debug.Log($"[RewardManager] 지속 피해 등록: {interval}초마다 {periodicDamagePercent}%");
    }

    /// <summary>
    /// 쿨타임 증가 등록 (얼어붙은 잉크)
    /// </summary>
    public void AddCooldownIncrease(float percent)
    {
        totalCooldownIncrease += percent;
        Debug.Log($"[RewardManager] 쿨타임 증가: +{percent}% (총: {totalCooldownIncrease}%)");
    }

    /// <summary>
    /// 전투 시작 피해 등록 (금이 간 표지)
    /// </summary>
    public void AddBattleStartDamage(float percent)
    {
        totalBattleStartDamage += percent;
        Debug.Log($"[RewardManager] 전투 시작 피해: +{percent}% (총: {totalBattleStartDamage}%)");
    }

    /// <summary>
    /// 골드 획득 감소 등록 (빚진 지식)
    /// </summary>
    public void AddGoldGainReduction(float percent)
    {
        totalGoldGainReduction += percent;
        Debug.Log($"[RewardManager] 골드 획득 감소: +{percent}% (총: {totalGoldGainReduction}%)");
    }
    #endregion

    #region 금서의 잔재 전용 — 조회 메서드 (다른 시스템에서 호출)
    /// <summary>
    /// 받는 피해 배율 반환 (PlayerStats.TakeDamage에서 호출)
    /// 예: 15% 증가 → 1.15 반환
    /// </summary>
    public float GetDamageTakenMultiplier()
    {
        return 1f + (totalDamageTakenIncrease / 100f);
    }

    /// <summary>
    /// 주는 피해 배율 반환 (Weapon.GetDamage 등에서 호출)
    /// 예: 10% 감소 → 0.9 반환
    /// </summary>
    public float GetDamageDealtMultiplier()
    {
        return 1f - (totalDamageDealtReduction / 100f);
    }

    /// <summary>
    /// 쿨타임 배율 반환 (SkillManager에서 호출)
    /// 예: 15% 증가 → 1.15 반환
    /// </summary>
    public float GetCooldownMultiplier()
    {
        return 1f + (totalCooldownIncrease / 100f);
    }

    /// <summary>
    /// 골드 획득 배율 반환 (PlayerStats.AddCoin 등에서 호출)
    /// 예: 15% 감소 → 0.85 반환
    /// </summary>
    public float GetGoldGainMultiplier()
    {
        return 1f - (totalGoldGainReduction / 100f);
    }

    /// <summary>
    /// 전투 시작 시 피해 적용 (StageManager.StartCombatStage에서 호출)
    /// </summary>
    public void ApplyBattleStartDamage()
    {
        if (totalBattleStartDamage <= 0f) return;
        if (PlayerStats.Instance == null) return;

        int damage = Mathf.RoundToInt(PlayerStats.Instance.MaxHP * (totalBattleStartDamage / 100f));
        if (damage > 0)
        {
            PlayerStats.Instance.TakeDamage(damage);
            Debug.Log($"[RewardManager] 전투 시작 피해: {damage} (최대 체력의 {totalBattleStartDamage}%)");
        }
    }
    #endregion

    #region 지속 피해 코루틴
    /// <summary>
    /// 지속 피해 코루틴 (부식하는 문장)
    /// </summary>
    private IEnumerator PeriodicDamageRoutine()
    {
        while (periodicDamageStackCount > 0)
        {
            yield return new WaitForSeconds(periodicInterval);

            if (PlayerStats.Instance != null && !PlayerStats.Instance.isDead)
            {
                int damage = Mathf.RoundToInt(
                    PlayerStats.Instance.CurrentHP * (periodicDamagePercent / 100f)
                );
                damage = Mathf.Max(1, damage);

                PlayerStats.Instance.TakeDamage(damage);
                Debug.Log($"[RewardManager] 지속 피해: {damage} (현재 체력의 {periodicDamagePercent}%)");
            }
        }

        periodicDamageCoroutine = null;
    }
    #endregion

    #region 챕터 종료 — 디버프 만료
    /// <summary>
    /// 챕터 종료 시 디버프 만료 처리
    /// </summary>
    public void OnChapterEnd(int endedChapter)
    {
        List<ActiveDebuff> toRemove = new List<ActiveDebuff>();

        foreach (var debuff in activeDebuffs)
        {
            if (debuff.durationType == EffectDurationType.UntilChapterEnd &&
                debuff.appliedChapter == endedChapter)
            {
                toRemove.Add(debuff);
            }
        }

        foreach (var debuff in toRemove)
        {
            RemoveDebuff(debuff);
        }

        Debug.Log($"[RewardManager] 챕터 {endedChapter} 종료 — {toRemove.Count}개 디버프 만료");
    }

    /// <summary>
    /// 층 클리어 시 호출 — UntilFloorCount 디버프 카운트 감소 및 해제
    /// StageManager.HandleStageCleared()에서 호출
    /// </summary>
    public void OnFloorCleared()
    {
        List<ActiveDebuff> toRemove = new List<ActiveDebuff>();

        foreach (var debuff in activeDebuffs)
        {
            if (debuff.durationType == EffectDurationType.UntilFloorCount)
            {
                debuff.remainingFloorCount--;

                if (debuff.remainingFloorCount <= 0)
                {
                    toRemove.Add(debuff);
                }
                else
                {
                    Debug.Log($"[RewardManager] '{debuff.debuffData.debuffName}' 남은 층: {debuff.remainingFloorCount}");
                }
            }
        }

        foreach (var debuff in toRemove)
        {
            RemoveDebuff(debuff);
            Debug.Log($"[RewardManager] '{debuff.debuffData.debuffName}' 층 조건 달성 — 디버프 해제!");
        }
    }

    /// <summary>
    /// 적 처치 시 호출 — UntilKillCount 디버프 카운트 감소 및 해제
    /// Enemy.HandleDeath()에서 호출
    /// </summary>
    public void OnEnemyKilled()
    {
        List<ActiveDebuff> toRemove = new List<ActiveDebuff>();

        foreach (var debuff in activeDebuffs)
        {
            if (debuff.durationType == EffectDurationType.UntilKillCount)
            {
                debuff.remainingKillCount--;

                if (debuff.remainingKillCount <= 0)
                {
                    toRemove.Add(debuff);
                }
            }
        }

        foreach (var debuff in toRemove)
        {
            RemoveDebuff(debuff);
            Debug.Log($"[RewardManager] '{debuff.debuffData.debuffName}' 처치 조건 달성 — 디버프 해제!");
        }
    }

    /// <summary>
    /// 디버프 제거 및 효과 복구
    /// </summary>
    private void RemoveDebuff(ActiveDebuff debuff)
    {
        switch (debuff.debuffData.debuffType)
        {
            case DebuffType.DamageOnHit:
                RemoveDamageOnHit(debuff.appliedDamageOnHit);
                break;
            case DebuffType.SkillSeal:
                UnsealSkills(debuff.sealedSkills);
                break;
            case DebuffType.StatDebuff:
                // FloorEffectManager에서 처리됨
                break;
            case DebuffType.DamageTakenIncrease:
                totalDamageTakenIncrease -= debuff.appliedDamageTakenIncrease;
                if (totalDamageTakenIncrease < 0f) totalDamageTakenIncrease = 0f;
                break;
            case DebuffType.DamageDealtReduction:
                totalDamageDealtReduction -= debuff.appliedDamageDealtReduction;
                if (totalDamageDealtReduction < 0f) totalDamageDealtReduction = 0f;
                break;
            case DebuffType.PeriodicDamage:
                periodicDamageStackCount--;
                periodicDamagePercent -= debuff.debuffData.periodicDamagePercent;
                if (periodicDamagePercent < 0f) periodicDamagePercent = 0f;
                break;
            case DebuffType.CooldownIncrease:
                totalCooldownIncrease -= debuff.appliedCooldownIncrease;
                if (totalCooldownIncrease < 0f) totalCooldownIncrease = 0f;
                break;
            case DebuffType.BattleStartDamage:
                totalBattleStartDamage -= debuff.appliedBattleStartDamage;
                if (totalBattleStartDamage < 0f) totalBattleStartDamage = 0f;
                break;
            case DebuffType.GoldGainReduction:
                totalGoldGainReduction -= debuff.appliedGoldGainReduction;
                if (totalGoldGainReduction < 0f) totalGoldGainReduction = 0f;
                break;
        }

        activeDebuffs.Remove(debuff);
        Debug.Log($"[RewardManager] 디버프 제거: {debuff.debuffData.debuffName}");
    }
    #endregion

    #region 전체 초기화
    /// <summary>
    /// 모든 디버프 제거 (런 종료 시)
    /// </summary>
    public void ClearAllDebuffs()
    {
        // 기존
        totalDamageOnHit = 0;
        sealedSkillSlots.Clear();

        // 금서의 잔재 전용
        totalDamageTakenIncrease = 0f;
        totalDamageDealtReduction = 0f;
        totalCooldownIncrease = 0f;
        totalBattleStartDamage = 0f;
        totalGoldGainReduction = 0f;

        // 지속 피해 초기화
        periodicDamageStackCount = 0;
        periodicDamagePercent = 0f;
        if (periodicDamageCoroutine != null)
        {
            StopCoroutine(periodicDamageCoroutine);
            periodicDamageCoroutine = null;
        }

        activeDebuffs.Clear();

        SkillManager.Instance?.OnSkillsSealed(sealedSkillSlots);

        Debug.Log("[RewardManager] 모든 디버프 제거됨");
    }
    #endregion

    #region Debug
    [ContextMenu("활성 디버프 확인")]
    public void LogActiveDebuffs()
    {
        Debug.Log("=== 활성 디버프 목록 ===");
        Debug.Log($"피격 추가 데미지: {totalDamageOnHit}");
        Debug.Log($"봉인된 스킬: {string.Join(", ", sealedSkillSlots)}");
        Debug.Log($"받는 피해 증가: {totalDamageTakenIncrease}%");
        Debug.Log($"주는 피해 감소: {totalDamageDealtReduction}%");
        Debug.Log($"쿨타임 증가: {totalCooldownIncrease}%");
        Debug.Log($"전투 시작 피해: {totalBattleStartDamage}%");
        Debug.Log($"골드 획득 감소: {totalGoldGainReduction}%");
        Debug.Log($"지속 피해: {periodicDamagePercent}% ({periodicDamageStackCount}스택)");

        if (activeDebuffs.Count == 0)
        {
            Debug.Log("추적 중인 디버프 없음");
            return;
        }

        foreach (var debuff in activeDebuffs)
        {
            Debug.Log($"- {debuff.debuffData.debuffName} (챕터 {debuff.appliedChapter}, {debuff.durationType})");
        }
    }
    #endregion
}
