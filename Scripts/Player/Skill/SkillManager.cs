using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스킬 슬롯 1개의 모든 상태를 묶은 데이터 클래스.
/// 장착된 스킬·레벨·쿨다운·콤보·차지·불씨를 한 곳에 묶어 관리.
/// </summary>
[System.Serializable]
public class SkillSlot
{
    public KeyCode key;
    public SkillData skill = null;
    public int level = 1;
    public float cooldownEndTime = 0f;
    public int comboStageIndex = 0;
    public float comboWindowEndTime = 0f;

    public int currentCharges = 0;
    public float nextChargeReadyTime = 0f;

    // [NEW] 부여된 불씨 리스트 — 순서 = 픽한 레벨 순서 (Lv2 → Lv3 → Lv4)
    // 슬롯에 종속 — 스킬 스왑 시 따라가고, 버리면 같이 사라짐.
    public List<EmberData> appliedEmbers = new List<EmberData>();

    public SkillSlot(KeyCode k) { key = k; }

    public bool IsEmpty => skill == null;
    public bool IsOnCooldown => Time.time < cooldownEndTime;
    public bool HasPendingCombo => comboStageIndex > 0;
    public bool IsComboWindowAlive => Time.time < comboWindowEndTime;
    public bool IsComboInProgress => HasPendingCombo && IsComboWindowAlive;

    public bool HasCharge => currentCharges > 0;
    public bool IsChargeFull(int max) => currentCharges >= max;

    // ========================================================================
    // [NEW] 불씨 헬퍼
    // ========================================================================

    /// <summary>현재 부여된 불씨 개수 (0~3).</summary>
    public int EmberCount => appliedEmbers.Count;

    /// <summary>불씨가 최대 3개 부여되어 더 이상 강화 불가능한 상태.</summary>
    public bool IsEmberMax => appliedEmbers.Count >= 3;

    /// <summary>
    /// 부여된 불씨 중 지정 타입의 effectValue 합산.
    /// 같은 효과 중복 시 자동 누적 (예: 데미지 +30% × 2 = +60%).
    /// </summary>
    /// <param name="type">조회할 효과 타입</param>
    /// <returns>합산 수치 (없으면 0)</returns>
    public float GetEmberModifierSum(EmberEffectType type)
    {
        float sum = 0f;
        foreach (var ember in appliedEmbers)
        {
            if (ember != null && ember.effectType == type)
                sum += ember.effectValue;
        }
        return sum;
    }

    /// <summary>
    /// 지정 타입의 불씨를 보유했는지 (boolean 체크용).
    /// DistanceScaling 같은 "효과 존재 여부"만 중요한 경우 사용.
    /// </summary>
    public bool HasEmberEffect(EmberEffectType type)
    {
        foreach (var ember in appliedEmbers)
        {
            if (ember != null && ember.effectType == type) return true;
        }
        return false;
    }

    /// <summary>
    /// ExtraCharges 불씨 적용된 실효 최대 차지 수.
    /// 차지 스킬이 아니면 0 반환.
    /// </summary>
    public int GetEffectiveMaxCharges()
    {
        if (skill == null || !skill.useCharges) return 0;
        int extra = Mathf.RoundToInt(GetEmberModifierSum(EmberEffectType.ExtraCharges));
        return skill.maxCharges + extra;
    }

    // ========================================================================
    // 정리
    // ========================================================================

    public void Clear()
    {
        skill = null;
        level = 1;
        cooldownEndTime = 0f;
        ResetCombo();
        ResetCharges();
        appliedEmbers.Clear();      // [NEW] 불씨도 같이 정리
    }

    public void ResetCombo()
    {
        comboStageIndex = 0;
        comboWindowEndTime = 0f;
    }

    public void ResetCharges()
    {
        currentCharges = 0;
        nextChargeReadyTime = 0f;
    }
}
public class SkillManager : MonoBehaviour
{
    // ========================================================================
    // 슬롯 구조 — 근거리/원거리 분리
    // ========================================================================

    [Header("슬롯 키 설정")]
    public KeyCode slotQKey = KeyCode.Q;
    public KeyCode slotEKey = KeyCode.E;
    public KeyCode slotRKey = KeyCode.R;

    private SkillSlot[] meleeSlots;
    private SkillSlot[] rangedSlots;

    private SkillCategory activeCategory = SkillCategory.Melee;

    [Header("아이템 드랍 설정")]
    [SerializeField] private GameObject droppedSkillItemPrefab;

    public bool isSequenceBroken = false;
    private DroppedSkillItem pendingSkillToSwap;
    // 매번 최신 CombatUIManager.Instance를 조회 — 씬 전환 후에도 새 매니저를 자동 참조
    private CombatUIManager uiManager => CombatUIManager.Instance;

    private PlayerController playerController;

    public static SkillManager Instance { get; private set; }
    private bool isSubscribedToStance = false;

    public event System.Action<SkillCategory> OnActiveCategoryChanged;

    // ========================================================================
    // 씬 전환 보존 (정적 백업)
    // ========================================================================

    // 씬 전환 시 슬롯 데이터를 살리기 위한 정적 백업.
    // 게임 새 런 시작 시 ClearPersistedSlots()로 명시적으로 비워야 함.
    // (SkillData / EmberData는 ScriptableObject라 씬 전환에도 살아있어 참조 그대로 유효.)
    private static SkillSlot[] persistedMeleeSlots;
    private static SkillSlot[] persistedRangedSlots;
    private static bool hasPersistedData = false;

    /// <summary>
    /// 정적 백업을 비움. 새 런 시작/사망/메인 메뉴 복귀 시 호출해야 함.
    /// 안 부르면 다음 런에 이전 스킬이 그대로 남아 의도와 다른 시작.
    /// </summary>
    public static void ClearPersistedSlots()
    {
        persistedMeleeSlots = null;
        persistedRangedSlots = null;
        hasPersistedData = false;
    }

    // ========================================================================
    // 초기화
    // ========================================================================

    private void Awake()
    {
        Debug.Log($"<color=cyan>[SkillManager.Awake]</color> GO={gameObject.name}, Instance is null? {Instance == null}, hasPersistedData={hasPersistedData}");

        if (Instance == null) Instance = this;
        else
        {
            Debug.Log($"<color=red>[SkillManager.Awake]</color> 중복 인스턴스 → Destroy(gameObject) 호출: {gameObject.name}");
            Destroy(gameObject);
            return;
        }

        playerController = GetComponent<PlayerController>();
    }

    private void Start()
    {
        InitializeSlots();
    }

    private void OnEnable()
    {
        StartCoroutine(SubscribeToStanceSystem());
    }

    private void OnDisable()
    {
        if (isSubscribedToStance && StanceGaugeSystem.Instance != null)
        {
            StanceGaugeSystem.Instance.OnStanceSwapped -= HandleStanceSwapped;
        }
        isSubscribedToStance = false;
    }

    /// <summary>
    /// 씬 전환·게임 종료 시 호출 — 슬롯 데이터를 정적 백업에 저장.
    /// 중복 Awake로 Destroy된 인스턴스는 백업하지 않음(Instance != this 가드).
    /// </summary>
    private void OnDestroy()
    {
        bool isMainInstance = (Instance == this);
        bool hasSlots = (meleeSlots != null && rangedSlots != null);

        // 슬롯에 실제 데이터가 있는지 확인
        bool hasAnyEquipped = false;
        if (hasSlots)
        {
            foreach (var s in meleeSlots) if (s != null && !s.IsEmpty) { hasAnyEquipped = true; break; }
            if (!hasAnyEquipped)
                foreach (var s in rangedSlots) if (s != null && !s.IsEmpty) { hasAnyEquipped = true; break; }
        }

        Debug.Log($"<color=orange>[SkillManager.OnDestroy]</color> GO={gameObject.name}, Instance==this? {isMainInstance}, hasSlots={hasSlots}, hasAnyEquipped={hasAnyEquipped}");

        if (!isMainInstance) return;
        if (!hasSlots) return;
        if (!hasAnyEquipped)
        {
            Debug.Log($"<color=red>[SkillManager.OnDestroy]</color> 슬롯 비어있어 백업 스킵 (의미 없는 빈 백업 방지)");
            return;
        }

        persistedMeleeSlots = meleeSlots;
        persistedRangedSlots = rangedSlots;
        hasPersistedData = true;
        Debug.Log($"<color=green>[SkillManager.OnDestroy]</color> 정적 백업 완료");
    }

    /// <summary>
    /// 슬롯 초기화 — 정적 백업이 있으면 복원, 없으면 빈 슬롯 생성.
    /// 씬 전환 후 호출 시 이전 씬의 스킬·불씨 그대로 유지됨.
    /// </summary>
    private void InitializeSlots()
    {
        Debug.Log($"<color=cyan>[SkillManager.InitializeSlots]</color> hasPersistedData={hasPersistedData}, persistedMeleeSlots is null? {persistedMeleeSlots == null}");

        if (hasPersistedData && persistedMeleeSlots != null && persistedRangedSlots != null)
        {
            meleeSlots = persistedMeleeSlots;
            rangedSlots = persistedRangedSlots;
            Debug.Log($"<color=green>[SkillManager.InitializeSlots]</color> 정적 백업에서 복원 완료");
            return;
        }

        Debug.Log($"<color=yellow>[SkillManager.InitializeSlots]</color> 빈 슬롯 새로 생성 (첫 시작 또는 클리어 후)");
        meleeSlots = new SkillSlot[]
        {
        new SkillSlot(slotQKey),
        new SkillSlot(slotEKey),
        new SkillSlot(slotRKey)
        };
        rangedSlots = new SkillSlot[]
        {
        new SkillSlot(slotQKey),
        new SkillSlot(slotEKey),
        new SkillSlot(slotRKey)
        };
    }

    // ========================================================================
    // 입력 처리
    // ========================================================================

    void Update()
    {
        HandleSkillInput();
        HandleComboWindowExpiry();
        HandleChargeRecovery();              // [NEW] 차지 회복 처리
        if (pendingSkillToSwap != null) HandleSwapInput();
    }

    private void HandleSkillInput()
    {
        if (playerController != null && playerController.isSkillCasting) return;

        if (Input.GetKeyDown(slotQKey)) TryActivateSkill(slotQKey);
        if (Input.GetKeyDown(slotEKey)) TryActivateSkill(slotEKey);
        if (Input.GetKeyDown(slotRKey)) TryActivateSkill(slotRKey);
    }

    // ========================================================================
    // 활성 카테고리
    // ========================================================================

    public SkillCategory GetActiveCategory() => activeCategory;

    public void SetActiveCategory(SkillCategory category)
    {
        if (category == SkillCategory.Common) return;
        if (activeCategory == category) return;
        activeCategory = category;
        OnActiveCategoryChanged?.Invoke(activeCategory);
    }

    private IEnumerator SubscribeToStanceSystem()
    {
        while (StanceGaugeSystem.Instance == null) yield return null;

        StanceGaugeSystem.Instance.OnStanceSwapped += HandleStanceSwapped;
        isSubscribedToStance = true;

        HandleStanceSwapped(StanceGaugeSystem.Instance.ActiveStance, false);
    }

    private void HandleStanceSwapped(PlayerCharacterType newStance, bool isTagSwap)
    {
        SkillCategory cat = (newStance == PlayerCharacterType.Melee)
            ? SkillCategory.Melee
            : SkillCategory.Ranged;

        SetActiveCategory(cat);
    }

    // ========================================================================
    // 슬롯 조회 헬퍼
    // ========================================================================

    private SkillSlot[] GetActiveSlots()
        => activeCategory == SkillCategory.Melee ? meleeSlots : rangedSlots;

    private SkillSlot[] GetSlotsByCategory(SkillCategory category)
    {
        if (category == SkillCategory.Melee) return meleeSlots;
        if (category == SkillCategory.Ranged) return rangedSlots;
        return GetActiveSlots();
    }

    private SkillSlot FindSlot(SkillSlot[] slots, KeyCode key)
    {
        if (slots == null) return null;
        foreach (var s in slots) if (s != null && s.key == key) return s;
        return null;
    }

    private SkillSlot GetActiveSlot(KeyCode key) => FindSlot(GetActiveSlots(), key);

    // ========================================================================
    // 스킬 발동
    // ========================================================================

    /// <summary>
    /// 발동 시도 — 차지 → 콤보 → 일반 순으로 분기.
    /// </summary>
    private void TryActivateSkill(KeyCode key)
    {
        if (RewardManager.Instance != null && RewardManager.Instance.IsSkillSealed(key))
        {
            Debug.Log($"[SkillManager] {key} 스킬이 봉인되어 있습니다.");
            return;
        }

        SkillSlot slot = GetActiveSlot(key);
        if (slot == null || slot.IsEmpty) return;

        SkillData skillData = slot.skill;
        int currentLevel = slot.level;

        // [NEW] 차지 스킬 분기 — 차지가 있어야 발동
        if (skillData.useCharges)
        {
            if (!slot.HasCharge)
            {
                Debug.Log($"[SkillManager] {skillData.skillName} — 차지 없음 (무시)");
                return;
            }
            StartCoroutine(ProcessChargeSkillRoutine(skillData, currentLevel, slot));
            return;
        }

        // 콤보 스킬 분기
        if (skillData.useCombo && skillData.ComboStageCount > 0)
        {
            if (slot.IsComboInProgress)
            {
                StartCoroutine(ProcessComboStageRoutine(skillData, currentLevel, slot));
                return;
            }
            if (slot.IsOnCooldown) return;
            slot.ResetCombo();
            StartCoroutine(ProcessComboStageRoutine(skillData, currentLevel, slot));
            return;
        }

        // 일반 스킬
        if (slot.IsOnCooldown) return;
        StartCoroutine(ProcessSkillRoutine(skillData, currentLevel, slot));
    }

    /// <summary>일반 스킬 실행 — 액션 순차 실행 후 쿨다운 시작.</summary>
    private IEnumerator ProcessSkillRoutine(SkillData skillData, int level, SkillSlot slot)
    {
        bool shouldBlock = skillData.blockMovement;
        if (playerController != null && shouldBlock) playerController.isSkillCasting = true;

        isSequenceBroken = false;

        // [CHANGED] 헬퍼로 데미지·크리 일괄 계산
        float baseDmg = skillData.GetDamage(level);
        float totalDamage = CalculateFinalDamage(baseDmg, skillData, slot, out _, skillData.skillName);

        foreach (var action in skillData.actions)
        {
            if (action != null)
            {
                yield return StartCoroutine(action.Execute(this.gameObject, level, slot.key, totalDamage));
                if (isSequenceBroken) break;
            }
        }

        if (playerController != null && shouldBlock) playerController.isSkillCasting = false;

        if (!isSequenceBroken)
        {
            // [CHANGED] 헬퍼로 쿨다운 계산
            float finalCool = CalculateFinalCooldown(skillData.GetCooldown(level), slot);
            StartCooldown(slot, finalCool);
        }
    }

    // ========================================================================
    // 콤보 처리
    // ========================================================================

    private IEnumerator ProcessComboStageRoutine(SkillData skillData, int level, SkillSlot slot)
    {
        int currentStageIdx = slot.comboStageIndex;

        bool shouldBlock = skillData.blockMovement;
        if (playerController != null && shouldBlock) playerController.isSkillCasting = true;

        isSequenceBroken = false;

        // [CHANGED] 헬퍼로 데미지 계산 (콤보 단계별 baseDmg 사용)
        float baseDmg = skillData.GetComboStageDamage(currentStageIdx, level);
        string label = $"{skillData.skillName} {currentStageIdx + 1}단";
        float totalDamage = CalculateFinalDamage(baseDmg, skillData, slot, out _, label);

        var stageActions = skillData.GetComboStageActions(currentStageIdx);
        foreach (var action in stageActions)
        {
            if (action != null)
            {
                yield return StartCoroutine(action.Execute(this.gameObject, level, slot.key, totalDamage));
                if (isSequenceBroken) break;
            }
        }

        if (playerController != null && shouldBlock) playerController.isSkillCasting = false;

        if (isSequenceBroken)
        {
            slot.ResetCombo();
            yield break;
        }

        int nextStageIdx = currentStageIdx + 1;
        if (nextStageIdx >= skillData.ComboStageCount)
        {
            FinishComboAndStartCooldown(slot, skillData, level);
        }
        else
        {
            slot.comboStageIndex = nextStageIdx;
            slot.comboWindowEndTime = Time.time + skillData.comboWindow;
        }
    }

    private void HandleComboWindowExpiry()
    {
        CheckExpiryForSlots(meleeSlots);
        CheckExpiryForSlots(rangedSlots);
    }

    private void CheckExpiryForSlots(SkillSlot[] slots)
    {
        if (slots == null) return;
        foreach (var slot in slots)
        {
            if (slot == null || slot.IsEmpty) continue;
            if (slot.HasPendingCombo && !slot.IsComboWindowAlive)
            {
                if (slot.skill.useCombo)
                    FinishComboAndStartCooldown(slot, slot.skill, slot.level);
                else
                    slot.ResetCombo();
            }
        }
    }

    private void FinishComboAndStartCooldown(SkillSlot slot, SkillData skillData, int level)
    {
        float finalCool = CalculateFinalCooldown(skillData.GetCooldown(level), slot);
        StartCooldown(slot, finalCool);
        slot.ResetCombo();
    }

    // ========================================================================
    // [NEW] 차지 시스템 처리
    // ========================================================================

    /// <summary>
    /// 차지 스킬 실행 — 차지 1발 소모 + 액션 실행.
    /// 일반/콤보와 달리 끝나도 쿨다운 시작 X (회복 타이머가 자체 관리).
    /// </summary>
    private IEnumerator ProcessChargeSkillRoutine(SkillData skillData, int level, SkillSlot slot)
    {
        // [CHANGED] effectiveMax 사용 (ExtraCharges 불씨 반영)
        int effectiveMax = slot.GetEffectiveMaxCharges();
        bool wasFull = (slot.currentCharges >= effectiveMax);
        slot.currentCharges--;

        SkillCategory cat = GetCategoryOf(slot);

        if (wasFull)
        {
            // [CHANGED] 회복 시간에 CooldownReduction 적용
            float recovery = CalculateFinalCooldown(skillData.GetChargeRecoveryTime(level), slot);
            slot.nextChargeReadyTime = Time.time + recovery;
            uiManager?.StartSkillCoolDown(cat, slot.key, recovery);
        }

        NotifyChargeChange(cat, slot.key, slot.currentCharges, effectiveMax);

        // 액션 실행
        bool shouldBlock = skillData.blockMovement;
        if (playerController != null && shouldBlock) playerController.isSkillCasting = true;

        isSequenceBroken = false;

        // [CHANGED] 헬퍼로 데미지 계산
        float baseDmg = skillData.GetDamage(level);
        float totalDamage = CalculateFinalDamage(baseDmg, skillData, slot, out _, skillData.skillName);

        foreach (var action in skillData.actions)
        {
            if (action != null)
            {
                yield return StartCoroutine(action.Execute(this.gameObject, level, slot.key, totalDamage));
                if (isSequenceBroken) break;
            }
        }

        if (playerController != null && shouldBlock) playerController.isSkillCasting = false;
    }

    /// <summary>매 프레임 차지 회복 처리 — 양쪽 카테고리 슬롯 모두.</summary>
    private void HandleChargeRecovery()
    {
        CheckChargeRecoveryForSlots(meleeSlots);
        CheckChargeRecoveryForSlots(rangedSlots);
    }

    private void CheckChargeRecoveryForSlots(SkillSlot[] slots)
    {
        if (slots == null) return;
        foreach (var slot in slots)
        {
            if (slot == null || slot.IsEmpty) continue;
            if (!slot.skill.useCharges) continue;

            // [CHANGED] effectiveMax 사용
            int effectiveMax = slot.GetEffectiveMaxCharges();
            if (slot.currentCharges >= effectiveMax) continue;

            if (Time.time < slot.nextChargeReadyTime) continue;

            slot.currentCharges++;
            SkillCategory cat = GetCategoryOf(slot);
            NotifyChargeChange(cat, slot.key, slot.currentCharges, effectiveMax);

            // 아직 풀이 아니면 다음 회복 타이머
            if (slot.currentCharges < effectiveMax)
            {
                // [CHANGED] 회복 시간에 CooldownReduction 적용
                float recovery = CalculateFinalCooldown(slot.skill.GetChargeRecoveryTime(slot.level), slot);
                slot.nextChargeReadyTime = Time.time + recovery;
                uiManager?.StartSkillCoolDown(cat, slot.key, recovery);
            }
        }
    }

    /// <summary>
    /// 차지 수 변경 시 UI 알림.
    /// CombatUIManager에 UpdateSkillCharges 메서드 추가 후 활성화 — 일단 로그.
    /// </summary>
    private void NotifyChargeChange(SkillCategory category, KeyCode key, int current, int max)
    {
        uiManager?.UpdateSkillCharges(category, key, current, max);
    }

    // ========================================================================
    // 쿨다운
    // ========================================================================

    private void StartCooldown(SkillSlot slot, float duration)
    {
        slot.cooldownEndTime = Time.time + duration;
        SkillCategory cat = GetCategoryOf(slot);
        uiManager?.StartSkillCoolDown(cat, slot.key, duration);
    }

    private void ResetCooldown(SkillSlot slot) { slot.cooldownEndTime = 0f; }

    // ========================================================================
    // [NEW] 불씨 모디파이어 적용 헬퍼 — 세 ProcessRoutine 공유
    // ========================================================================

    /// <summary>
    /// 불씨 모디파이어를 반영한 최종 데미지·크리티컬 계산.
    /// 데미지 +%, 크리 확률 +%, 크리 피해 +%를 일괄 처리.
    /// </summary>
    private float CalculateFinalDamage(float baseDmg, SkillData skillData, SkillSlot slot, out bool isCritical, string skillLabel)
    {
        // 1. 불씨 데미지 % 적용
        float damageMult = 1f + (slot.GetEmberModifierSum(EmberEffectType.DamagePercent) / 100f);
        baseDmg *= damageMult;

        // 2. 스탯 보정
        float myStat = PlayerStats.Instance.GetStat(skillData.statType);
        float totalDamage = baseDmg + (myStat * skillData.damageRatio);

        // 3. 크리티컬 굴림 (기본 + 불씨 보너스)
        //    주의: 기본 크리에 실패했을 때만 보너스 추가 굴림 — 정확한 확률 합산은 아니지만 데모 단계에선 충분
        isCritical = PlayerStats.Instance.RollCritical();
        if (!isCritical)
        {
            float critBonus = slot.GetEmberModifierSum(EmberEffectType.CritChanceBonus);
            if (critBonus > 0f && Random.value < critBonus / 100f)
            {
                isCritical = true;
            }
        }

        // 4. 크리 시 데미지 증폭 (기본 1.5배 + 불씨 추가 %)
        if (isCritical)
        {
            float critDmgBonus = slot.GetEmberModifierSum(EmberEffectType.CritDamagePercent);
            float critMult = 1.5f + (critDmgBonus / 100f);
            totalDamage *= critMult;
            Debug.Log($"<color=red><b>[CRITICAL!]</b></color> {skillLabel} 데미지 {critMult:F2}배!");
        }

        return totalDamage;
    }

    /// <summary>
    /// 불씨 모디파이어를 반영한 최종 쿨다운 계산 (CooldownReduction 차감).
    /// 일반 스킬 쿨 + 차지 스킬 회복 시간 양쪽에 사용.
    /// </summary>
    private float CalculateFinalCooldown(float baseCool, SkillSlot slot)
    {
        float cool = baseCool;
        cool -= slot.GetEmberModifierSum(EmberEffectType.CooldownReduction);

        if (RewardManager.Instance != null)
            cool *= RewardManager.Instance.GetCooldownMultiplier();

        return Mathf.Max(0.1f, cool);
    }

    // ========================================================================
    // 장착/해제/스왑 — 카테고리 인지
    // ========================================================================

    public bool AutoEquipSkill(SkillData skillData, int level = 1)
    {
        if (skillData == null) return false;

        SkillSlot[] primary;
        SkillSlot[] fallback = null;

        if (skillData.category == SkillCategory.Common)
        {
            primary = GetActiveSlots();
            fallback = (activeCategory == SkillCategory.Melee) ? rangedSlots : meleeSlots;
        }
        else
        {
            primary = GetSlotsByCategory(skillData.category);
        }

        if (TryFillEmpty(primary, skillData, level)) return true;
        if (fallback != null && TryFillEmpty(fallback, skillData, level)) return true;
        return false;
    }

    /// <summary>
    /// 현재 활성 카테고리에 빈 스킬 슬롯이 있는지 여부.
    /// false = 3슬롯 다 참 (신규 스킬 획득 불가, 업그레이드 전환만 가능).
    /// </summary>
    public bool HasEmptySlot()
    {
        SkillSlot[] slots = GetActiveSlots();
        if (slots == null) return false;

        foreach (var s in slots)
        {
            if (s != null && s.IsEmpty) return true;
        }
        return false;
    }

    private bool TryFillEmpty(SkillSlot[] slots, SkillData skillData, int level)
    {
        foreach (var slot in slots)
        {
            if (slot.IsEmpty)
            {
                slot.skill = skillData;
                slot.level = level;
                slot.ResetCombo();
                ResetCooldown(slot);

                // [NEW] 차지 스킬이면 풀 차지로 초기화
                InitializeChargesIfNeeded(slot, skillData);

                NotifySlotUI(GetCategoryOf(slot), slot.key, skillData);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 스킬 장착 시점에 차지 상태 초기화.
    /// 차지 스킬이면 풀 차지로, 일반/콤보면 차지 비움.
    /// </summary>
    private void InitializeChargesIfNeeded(SkillSlot slot, SkillData skillData)
    {
        SkillCategory cat = GetCategoryOf(slot);

        if (skillData.useCharges)
        {
            // [CHANGED] ExtraCharges 불씨 반영한 실효 max로 초기화
            int effectiveMax = slot.GetEffectiveMaxCharges();
            slot.currentCharges = effectiveMax;
            slot.nextChargeReadyTime = 0f;
            NotifyChargeChange(cat, slot.key, slot.currentCharges, effectiveMax);
        }
        else
        {
            slot.ResetCharges();
            NotifyChargeChange(cat, slot.key, 0, 0);
        }
    }

    private SkillCategory GetCategoryOf(SkillSlot slot)
    {
        foreach (var s in meleeSlots) if (s == slot) return SkillCategory.Melee;
        return SkillCategory.Ranged;
    }

    public void UnequipSkill(KeyCode slotKey)
        => UnequipSkill(activeCategory, slotKey);

    public void UnequipSkill(SkillCategory category, KeyCode slotKey)
    {
        SkillSlot slot = FindSlot(GetSlotsByCategory(category), slotKey);
        if (slot == null || slot.IsEmpty) return;

        SkillData oldSkill = slot.skill;
        int oldLevel = slot.level;
        slot.Clear();
        DropSkill(oldSkill, oldLevel);
        NotifySlotUI(category, slotKey, null);
        NotifyChargeChange(category, slotKey, 0, 0);
    }

    public void SwapSkills(KeyCode fromKey, KeyCode toKey)
        => SwapSkills(activeCategory, fromKey, toKey);

    public void SwapSkills(SkillCategory category, KeyCode fromKey, KeyCode toKey)
    {
        if (fromKey == toKey) return;

        SkillSlot[] slots = GetSlotsByCategory(category);
        SkillSlot fromSlot = FindSlot(slots, fromKey);
        SkillSlot toSlot = FindSlot(slots, toKey);
        if (fromSlot == null || toSlot == null) return;

        (fromSlot.skill, toSlot.skill) = (toSlot.skill, fromSlot.skill);
        (fromSlot.level, toSlot.level) = (toSlot.level, fromSlot.level);
        (fromSlot.cooldownEndTime, toSlot.cooldownEndTime) = (toSlot.cooldownEndTime, fromSlot.cooldownEndTime);
        (fromSlot.currentCharges, toSlot.currentCharges) = (toSlot.currentCharges, fromSlot.currentCharges);
        (fromSlot.nextChargeReadyTime, toSlot.nextChargeReadyTime) = (toSlot.nextChargeReadyTime, fromSlot.nextChargeReadyTime);
        (fromSlot.appliedEmbers, toSlot.appliedEmbers) = (toSlot.appliedEmbers, fromSlot.appliedEmbers); // [NEW]
        fromSlot.ResetCombo();
        toSlot.ResetCombo();

        NotifySlotUI(category, fromKey, fromSlot.skill);
        NotifySlotUI(category, toKey, toSlot.skill);

        int fromMax = (fromSlot.skill != null && fromSlot.skill.useCharges) ? fromSlot.GetEffectiveMaxCharges() : 0;
        int toMax = (toSlot.skill != null && toSlot.skill.useCharges) ? toSlot.GetEffectiveMaxCharges() : 0;
        NotifyChargeChange(category, fromKey, fromSlot.currentCharges, fromMax);
        NotifyChargeChange(category, toKey, toSlot.currentCharges, toMax);
    }

    private void DropSkill(SkillData skillToDrop, int level)
    {
        if (skillToDrop == null || droppedSkillItemPrefab == null) return;
        Vector3 spawnPos = transform.position + transform.forward * 1f;
        GameObject droppedItem = Instantiate(droppedSkillItemPrefab, spawnPos, Quaternion.identity);
        DroppedSkillItem itemLogic = droppedItem.GetComponent<DroppedSkillItem>();
        if (itemLogic != null) itemLogic.Initialize(skillToDrop, level);
    }

    // ========================================================================
    // 카드 줍기 교체 UI
    // ========================================================================

    public void StartSwapProcess(DroppedSkillItem itemToSwap)
    {
        pendingSkillToSwap = itemToSwap;
        uiManager?.ExpandUI(true);
    }

    private void HandleSwapInput()
    {
        if (Input.GetKeyDown(slotQKey)) FinalizeSwap(slotQKey);
        if (Input.GetKeyDown(slotEKey)) FinalizeSwap(slotEKey);
        if (Input.GetKeyDown(slotRKey)) FinalizeSwap(slotRKey);
        if (Input.GetKeyDown(KeyCode.Escape)) CancelSwapProcess();
    }

    private void CancelSwapProcess()
    {
        pendingSkillToSwap = null;
        if (!Input.GetKey(KeyCode.LeftControl)) uiManager?.ExpandUI(false);
    }

    private void FinalizeSwap(KeyCode selectedKey)
    {
        if (pendingSkillToSwap == null) return;

        SkillData newSkill = pendingSkillToSwap.GetSkillData();
        int newLevel = pendingSkillToSwap.GetSkillLevel();
        SkillSlot targetSlot = GetActiveSlot(selectedKey);
        if (targetSlot == null) return;

        if (!IsCategoryCompatible(newSkill.category, activeCategory))
        {
            Debug.Log($"[SkillManager] '{newSkill.skillName}' ({newSkill.category}) — 현재 슬롯({activeCategory}) 장착 불가");
            return;
        }

        SkillData oldSkill = targetSlot.skill;
        int oldLevel = targetSlot.level;

        targetSlot.skill = newSkill;
        targetSlot.level = newLevel;
        ResetCooldown(targetSlot);
        targetSlot.ResetCombo();

        // [NEW] 차지 스킬이면 풀 차지로 초기화
        InitializeChargesIfNeeded(targetSlot, newSkill);

        NotifySlotUI(activeCategory, selectedKey, newSkill);

        if (oldSkill != null) DropSkill(oldSkill, oldLevel);

        Destroy(pendingSkillToSwap.gameObject);
        pendingSkillToSwap = null;

        if (!Input.GetKey(KeyCode.LeftControl)) uiManager?.ExpandUI(false);
    }

    private bool IsCategoryCompatible(SkillCategory cardCategory, SkillCategory slotCategory)
    {
        if (cardCategory == SkillCategory.Common) return true;
        return cardCategory == slotCategory;
    }

    // ========================================================================
    // UI 알림
    // ========================================================================

    private void NotifySlotUI(SkillCategory category, KeyCode key, SkillData skillData)
    {
        uiManager?.UpdateSkillSlot(category, key, skillData);
    }

    // ========================================================================
    // 외부 API — 기존 KeyCode 기반 (활성 카테고리 폴백)
    // ========================================================================

    public SkillData GetEquippedSkill(KeyCode key)
    {
        SkillSlot slot = GetActiveSlot(key);
        return slot != null ? slot.skill : null;
    }

    public int GetSkillLevel(KeyCode key)
    {
        SkillSlot slot = GetActiveSlot(key);
        return slot != null ? slot.level : 1;
    }

    public void UpgradeSkill(KeyCode key)
    {
        SkillSlot slot = GetActiveSlot(key);
        if (slot == null || slot.IsEmpty) return;
        slot.level++;
    }

    public int GetCurrentComboStage(KeyCode key)
    {
        SkillSlot slot = GetActiveSlot(key);
        return slot != null ? slot.comboStageIndex : 0;
    }

    public float GetComboWindowRemaining(KeyCode key)
    {
        SkillSlot slot = GetActiveSlot(key);
        if (slot == null) return 0f;
        return Mathf.Max(0f, slot.comboWindowEndTime - Time.time);
    }

    /// <summary>
    /// 해당 스킬이 현재 어느 슬롯(검/활 카테고리 모두)에든 장착되어 있는지 확인.
    /// 카드 보상에서 이미 가진 스킬을 후보에서 제외할 때 사용.
    /// </summary>
    public bool IsSkillEquipped(SkillData skill)
    {
        if (skill == null) return false;

        // 검 슬롯 검사
        if (meleeSlots != null)
        {
            foreach (var slot in meleeSlots)
            {
                if (slot != null && !slot.IsEmpty && slot.skill == skill) return true;
            }
        }

        // 활 슬롯 검사
        if (rangedSlots != null)
        {
            foreach (var slot in rangedSlots)
            {
                if (slot != null && !slot.IsEmpty && slot.skill == skill) return true;
            }
        }

        return false;
    }

    // [NEW] 차지 상태 조회용 (UI에서 사용)
    public int GetCurrentCharges(KeyCode key)
    {
        SkillSlot slot = GetActiveSlot(key);
        return slot != null ? slot.currentCharges : 0;
    }

    public float GetChargeRecoveryRemaining(KeyCode key)
    {
        SkillSlot slot = GetActiveSlot(key);
        if (slot == null) return 0f;
        return Mathf.Max(0f, slot.nextChargeReadyTime - Time.time);
    }

    // ========================================================================
    // 카테고리 명시 API
    // ========================================================================

    public SkillSlot GetSlot(SkillCategory category, KeyCode key)
        => FindSlot(GetSlotsByCategory(category), key);

    public SkillSlot[] GetSlots(SkillCategory category) => GetSlotsByCategory(category);

    /// <summary>활성 카테고리 + 키 슬롯 직접 조회 (액션에서 SkillSlot 접근용).</summary>
    public SkillSlot GetCurrentSlot(KeyCode key) => FindSlot(GetActiveSlots(), key);

    /// <summary>
    /// 슬롯에 불씨 부여 — 카드 픽 UI에서 호출.
    /// 슬롯에 이미 3개 부여되어 있거나 슬롯이 비어있으면 거부.
    /// 부여 시 차지 max 갱신 (ExtraCharges 불씨인 경우 currentCharges도 +1).
    /// </summary>
    /// <param name="category">대상 슬롯 카테고리</param>
    /// <param name="key">대상 슬롯 키</param>
    /// <param name="ember">부여할 불씨</param>
    /// <returns>부여 성공 여부</returns>
    public bool ApplyEmberToSlot(SkillCategory category, KeyCode key, EmberData ember)
    {
        if (ember == null) return false;

        SkillSlot slot = FindSlot(GetSlotsByCategory(category), key);
        if (slot == null || slot.IsEmpty)
        {
            Debug.Log($"[SkillManager] {category}-{key} 슬롯이 비어 있어 불씨 부여 불가");
            return false;
        }
        if (slot.IsEmberMax)
        {
            Debug.Log($"[SkillManager] {category}-{key} 슬롯이 이미 불씨 최대(3개) — 부여 불가");
            return false;
        }

        slot.appliedEmbers.Add(ember);
        slot.level++; // 강화 = 레벨 + 1 (Lv1 → Lv2 → Lv3 → Lv4)

        // ExtraCharges 불씨면 현재 차지도 같이 늘려줌 (즉시 사용 가능하도록)
        if (ember.effectType == EmberEffectType.ExtraCharges)
        {
            int delta = Mathf.RoundToInt(ember.effectValue);
            slot.currentCharges = Mathf.Min(slot.currentCharges + delta, slot.GetEffectiveMaxCharges());
            NotifyChargeChange(category, key, slot.currentCharges, slot.GetEffectiveMaxCharges());
        }

        Debug.Log($"[SkillManager] {slot.skill.skillName}에 '{ember.emberName}' 부여 (Lv{slot.level}, 불씨 {slot.appliedEmbers.Count}/3)");
        return true;
    }

    /// <summary>
    /// 활성 카테고리(검/활) 슬롯에서 지정 불씨 효과의 합산 수치를 조회.
    /// 
    /// 용도: SkillAction이나 발사체/스폰 오브젝트처럼 SkillSlot을 직접 모르는 코드가
    /// 불씨 모디파이어를 적용할 때 사용하는 외부 조회 API.
    /// 
    /// 예) SpawnTornadoAction에서 lifetime 적용 시:
    ///     float bonus = SkillManager.Instance.GetActiveEmberModifier(key, EmberEffectType.DurationPercent);
    ///     float finalLifetime = lifetime * (1f + bonus / 100f);
    /// </summary>
    /// <param name="key">조회할 슬롯 키 (Q, E, R, LMB, RMB 등)</param>
    /// <param name="type">조회할 불씨 효과 타입</param>
    /// <returns>해당 효과의 effectValue 합산 (슬롯이 없거나 비어있으면 0)</returns>
    public float GetActiveEmberModifier(KeyCode key, EmberEffectType type)
    {
        SkillSlot slot = GetSlot(activeCategory, key);
        return slot != null ? slot.GetEmberModifierSum(type) : 0f;
    }

    // ========================================================================
    // 런 리셋 / 봉인
    // ========================================================================

    public void ResetAllSkills()
    {
        foreach (var s in meleeSlots) s.Clear();
        foreach (var s in rangedSlots) s.Clear();

        pendingSkillToSwap = null;
        isSequenceBroken = false;
        StopAllCoroutines();

        // UI 측은 양쪽 다 비우기 알림 + 차지 표시도 OFF
        foreach (var s in meleeSlots)
        {
            NotifySlotUI(SkillCategory.Melee, s.key, null);
            NotifyChargeChange(SkillCategory.Melee, s.key, 0, 0);
        }
        foreach (var s in rangedSlots)
        {
            NotifySlotUI(SkillCategory.Ranged, s.key, null);
            NotifyChargeChange(SkillCategory.Ranged, s.key, 0, 0);
        }

        Debug.Log("[SkillManager] 런 리셋 — 모든 스킬 초기화 완료");
    }

    public void OnSkillsSealed(List<KeyCode> sealedSlots)
    {
        Debug.Log($"[SkillManager] 봉인된 스킬: {string.Join(", ", sealedSlots)}");
    }
}
