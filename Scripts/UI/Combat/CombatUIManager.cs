using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

/// <summary>
/// 전투 UI 매니저 — 스킬 슬롯, 쿨다운, 검 슬롯, 아이템 슬롯 등 우하단/중앙 HUD 관리.
/// 
/// 슬롯 구조:
///   - 검(Melee) 3슬롯 + 활(Ranged) 3슬롯 = 총 6슬롯을 manageableSkillSlots에 다 등록.
///   - 각 SkillSlotUI는 slotCategory + slotKey 조합으로 식별됨.
///   - UpdateSkillSlot / StartSkillCoolDown은 카테고리 + 키 둘 다 받아 정확한 슬롯만 갱신.
/// 
/// 책임 밖:
///   - 좌하단 게이지 UI (StanceGaugeUI)
///   - 캐릭터 활성/비활성 (PlayerTagController)
///   - 게이지/락 관리 (StanceGaugeSystem)
///   - 스탠스 전환 시 슬롯 카테고리 동기화 (SkillManager가 자체 구독)
/// </summary>
public class CombatUIManager : MonoBehaviour
{
    public static CombatUIManager Instance;

    // ============================================================
    // Skill Slots
    // ============================================================

    [Header("Skill Slots (Draggable) — 검/활 6슬롯 모두 등록")]
    [Tooltip("Q/E/R 슬롯 6개 (검 3 + 활 3). 각 SkillSlotUI는 본인 slotCategory 필드로 카테고리 식별.")]
    [SerializeField] private SkillSlotUI[] manageableSkillSlots;

    [Header("Cooldown Handlers (Melee)")]
    [SerializeField] private SkillCooldownSlot cdSlotQ;
    [SerializeField] private SkillCooldownSlot cdSlotE;
    [SerializeField] private SkillCooldownSlot cdSlotR;

    [Header("Cooldown Handlers (Ranged) — 활 슬롯 추가 후 끌어놓기")]
    [SerializeField] private SkillCooldownSlot cdSlotQ_Ranged;
    [SerializeField] private SkillCooldownSlot cdSlotE_Ranged;
    [SerializeField] private SkillCooldownSlot cdSlotR_Ranged;

    [Header("Cooldown Handlers (Common — 두 스탠스 공유)")]
    [SerializeField] private SkillCooldownSlot cdSlotShift;
    [SerializeField] private SkillCooldownSlot cdSlotRMB;

    [Header("Skill Slots (Fixed)")]
    [SerializeField] private Image tSlotIcon;
    [SerializeField] private Image lmbSlotIcon;
    [SerializeField] private Image rmbSlotIcon;

    [Header("Sword Slot")]
    public Image swordIcon;
    public Animator swordShineEffect;

    [Header("Item Slots")]
    public Image[] itemIcons;

    [Header("UI Expansion")]
    [SerializeField] private GameObject playerStatsPanel;
    [SerializeField] private HorizontalLayoutGroup slotLayoutGroup;
    [SerializeField] private RectTransform slotsContainer;

    [Header("Animation Settings")]
    public float normalSpacing = 10f;
    public float expandedSpacing = 150f;
    public float normalScale = 1.0f;
    public float expandedScale = 1.5f;
    public float animDuration = 0.3f;

    // ============================================================
    // 내부 상태
    // ============================================================

    private bool isExpanded = false;
    private bool isLocked = false;

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (playerStatsPanel != null) playerStatsPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (isLocked) return;

        bool isCtrlPressed = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        if (isCtrlPressed && !isExpanded) ExpandUI(true);
        else if (!isCtrlPressed && isExpanded) ExpandUI(false);
    }

    // ============================================================
    // UI Expansion (Ctrl 키 확장)
    // ============================================================

    public void ExpandUI(bool expand, bool lockState = false)
    {
        isExpanded = expand;
        if (lockState) isLocked = expand;

        if (playerStatsPanel != null) playerStatsPanel.SetActive(expand);

        float targetSpacing = expand ? expandedSpacing : normalSpacing;
        float targetScale = expand ? expandedScale : normalScale;

        if (slotLayoutGroup != null)
        {
            DOTween.To(() => slotLayoutGroup.spacing, x => slotLayoutGroup.spacing = x, targetSpacing, animDuration)
                .SetUpdate(true).SetEase(Ease.OutBack);
        }

        if (slotsContainer != null)
        {
            slotsContainer.DOScale(targetScale, animDuration)
                .SetUpdate(true).SetEase(Ease.OutBack);
        }

        if (manageableSkillSlots != null)
        {
            foreach (SkillSlotUI slot in manageableSkillSlots)
            {
                if (slot != null) slot.SetManageable(expand);
            }
        }
    }

    // ============================================================
    // Skill Cooldown — 카테고리 인지
    // ============================================================

    /// <summary>
    /// 카테고리 + 키로 정확한 쿨다운 슬롯에 적용 (신규).
    /// 검/활 Q가 서로 다른 슬롯이므로 카테고리 분기 필수.
    /// </summary>
    public void StartSkillCoolDown(SkillCategory category, KeyCode key, float duration)
    {
        SkillCooldownSlot target = GetCooldownSlot(category, key);
        if (target != null) target.StartCooldown(duration);
    }

    /// <summary>카테고리 + 키 → SkillCooldownSlot 매핑.</summary>
    private SkillCooldownSlot GetCooldownSlot(SkillCategory category, KeyCode key)
    {
        // Common (Shift/RMB) — 카테고리 무관, 공유
        if (key == KeyCode.LeftShift || key == KeyCode.RightShift) return cdSlotShift;
        if (key == KeyCode.Mouse1) return cdSlotRMB;

        // Q/E/R — 카테고리별 분기
        bool isRanged = (category == SkillCategory.Ranged);
        switch (key)
        {
            case KeyCode.Q: return isRanged ? cdSlotQ_Ranged : cdSlotQ;
            case KeyCode.E: return isRanged ? cdSlotE_Ranged : cdSlotE;
            case KeyCode.R: return isRanged ? cdSlotR_Ranged : cdSlotR;
        }
        return null;
    }

    /// <summary>기존 KeyCode 기반 시그니처 — 활성 카테고리로 폴백.</summary>
    public void StartSkillCoolDown(KeyCode key, float duration)
    {
        SkillCategory cat = (SkillManager.Instance != null)
            ? SkillManager.Instance.GetActiveCategory()
            : SkillCategory.Melee;
        StartSkillCoolDown(cat, key, duration);
    }

    /// <summary>인덱스 기반 호환 (0=Q, 1=E, 2=R).</summary>
    public void StartSkillCoolDown(int index, float duration)
    {
        if (index == 0) StartSkillCoolDown(KeyCode.Q, duration);
        else if (index == 1) StartSkillCoolDown(KeyCode.E, duration);
        else if (index == 2) StartSkillCoolDown(KeyCode.R, duration);
    }

    // ============================================================
    // Skill Slot Update — 카테고리 인지
    // ============================================================

    /// <summary>
    /// 카테고리 + 키로 정확한 SkillSlotUI에 갱신 (신규).
    /// 검 Q / 활 Q가 다른 슬롯이므로 카테고리 분기 필수.
    /// </summary>
    public void UpdateSkillSlot(SkillCategory category, KeyCode key, SkillData skillData)
    {
        SkillSlotUI targetSlot = FindSlotUI(category, key);

        if (targetSlot != null)
        {
            targetSlot.UpdateDisplay(skillData);
            return;
        }

        // 매칭 슬롯 못 찾으면 고정 슬롯(T/LMB/RMB) 아이콘으로 폴백
        UpdateFixedSlotIcon(key, skillData);
    }

    /// <summary>기존 KeyCode 기반 시그니처 — 활성 카테고리로 폴백.</summary>
    public void UpdateSkillSlot(KeyCode key, SkillData skillData)
    {
        SkillCategory cat = (SkillManager.Instance != null)
            ? SkillManager.Instance.GetActiveCategory()
            : SkillCategory.Melee;
        UpdateSkillSlot(cat, key, skillData);
    }

    /// <summary>manageableSkillSlots에서 카테고리 + 키 둘 다 매칭되는 SkillSlotUI 찾기.</summary>
    private SkillSlotUI FindSlotUI(SkillCategory category, KeyCode key)
    {
        if (manageableSkillSlots == null) return null;
        foreach (var slot in manageableSkillSlots)
        {
            if (slot != null && slot.GetSlotKey() == key && slot.GetSlotCategory() == category)
                return slot;
        }
        return null;
    }

    /// <summary>고정 슬롯(T/LMB/RMB) 아이콘만 갱신.</summary>
    private void UpdateFixedSlotIcon(KeyCode key, SkillData skillData)
    {
        Image targetIcon = null;
        if (key == KeyCode.T) targetIcon = tSlotIcon;
        else if (key == KeyCode.Mouse0) targetIcon = lmbSlotIcon;
        else if (key == KeyCode.Mouse1) targetIcon = rmbSlotIcon;

        if (targetIcon == null) return;

        if (skillData != null)
        {
            targetIcon.sprite = skillData.skillIcon;
            targetIcon.color = Color.white;
            targetIcon.enabled = true;
        }
        else
        {
            targetIcon.sprite = null;
            targetIcon.color = new Color(1, 1, 1, 0.5f);
            targetIcon.enabled = false;
        }
    }

    /// <summary>
    /// 카테고리 + 키 슬롯의 차지 수 표시 갱신.
    /// SkillManager가 차지 변경 시(시전/회복) 호출.
    /// 
    /// maxCharges 인자는 현재는 미사용 — 향후 "2/3" 같은 표시로 확장 시 사용.
    /// </summary>
    public void UpdateSkillCharges(SkillCategory category, KeyCode key, int currentCharges, int maxCharges)
    {
        SkillSlotUI targetSlot = FindSlotUI(category, key);
        if (targetSlot != null)
        {
            targetSlot.SetChargeDisplay(currentCharges);
        }
    }

    // ============================================================
    // Sword & Item Slots
    // ============================================================

    public void SetSwordIcon(Sprite icon)
    {
        if (swordIcon != null) swordIcon.sprite = icon;
    }

    public void SetSwordShineEffect(bool active)
    {
        if (swordShineEffect != null) swordShineEffect.SetBool("isShining", active);
    }

    public void SetItemIcon(int index, Sprite icon)
    {
        if (index >= 0 && index < itemIcons.Length) itemIcons[index].sprite = icon;
    }
}
