using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 개별 스킬 슬롯의 UI 상호작용(클릭, 드래그, 드롭, 툴팁)을 담당합니다.
/// 
/// 슬롯은 KeyCode + SkillCategory 조합으로 식별됨:
///   - 검 Q / 활 Q는 같은 KeyCode지만 서로 다른 슬롯
///   - slotCategory 필드로 본인이 어느 카테고리 슬롯인지 인스펙터에서 지정
/// 
/// Q/E/R은 유동성 있는 스킬, LMB/RMB는 고정 스킬(fixedSkillData).
/// </summary>
public class SkillSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IDropHandler, IEndDragHandler
{
    [Header("슬롯 설정")]
    [Tooltip("이 슬롯에 해당하는 KeyCode (예: KeyCode.Q)")]
    [SerializeField] private KeyCode slotKey;

    [Tooltip("이 슬롯이 어느 카테고리에 속하는지 — Melee(검) / Ranged(활).\n" +
             "이 값이 SkillManager의 어느 슬롯 배열을 참조할지 결정.\n" +
             "검/활 슬롯이 같은 KeyCode를 가질 수 있으므로 카테고리로 구분 필수.")]
    [SerializeField] private SkillCategory slotCategory = SkillCategory.Melee;

    [Header("고정 스킬 설정 (LMB/RMB용)")]
    [SerializeField] private SkillData fixedSkillData;

    [Header("UI 참조")]
    [Tooltip("스킬 아이콘을 표시할 Image 컴포넌트")]
    [SerializeField] private Image skillIconImage;
    [Tooltip("쿨다운 오버레이 Image 컴포넌트")]
    [SerializeField] private Image cooldownOverlayImage;
    [Tooltip("비어있을 때 표시할 기본 아이콘 (옵션)")]
    [SerializeField] private Sprite defaultIcon;

    [Header("스택 표시 컴포넌트")]
    [SerializeField] private GameObject stackContainerPanel;
    [SerializeField] private TextMeshProUGUI stackCountText;

    [Header("강화 모드 설정")]
    [SerializeField] private TextMeshProUGUI costText;
    private bool isUpgradeMode = false;

    // --- 내부 참조 ---
    private SkillManager skillManager;
    private SkillTooltipUI skillTooltip;
    private SkillData currentSkill;
    private bool isManageable = false;

    // --- 정적 변수 (드래그 상태 공유) ---
    private static SkillSlotUI slotBeingDragged;
    private static GameObject dragIconInstance;
    private static Canvas rootCanvas;

    void Start()
    {
        skillManager = FindFirstObjectByType<SkillManager>();
        skillTooltip = FindFirstObjectByType<SkillTooltipUI>(FindObjectsInactive.Include);

        if (skillManager == null)
        {
            Debug.LogError($"[SkillSlotUI] {slotCategory}-{slotKey} 슬롯이 SkillManager를 찾을 수 없습니다!");
        }
        if (skillTooltip == null)
        {
            Debug.LogWarning($"[SkillSlotUI] {slotCategory}-{slotKey} 슬롯이 SkillTooltipUI를 찾을 수 없습니다!");
        }
        // 고정 슬롯(LMB/RMB)은 fixedSkillData 우선
        if (fixedSkillData != null)
        {
            UpdateDisplay(fixedSkillData);
        }
        else
        {
            // 변경 가능 슬롯(Q/E/R) — SkillManager에 장착된 스킬이 있으면 표시.
            // 씬 전환 후 SkillManager에 보존된 데이터를 새 UI에 반영하기 위해 필수.
            SkillData equippedSkill = null;
            if (skillManager != null)
            {
                var dataSlot = skillManager.GetSlot(slotCategory, slotKey);
                if (dataSlot != null && !dataSlot.IsEmpty)
                {
                    equippedSkill = dataSlot.skill;
                }
            }
            UpdateDisplay(equippedSkill);
        }

        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>().rootCanvas;
        }
    }

    /// <summary>CombatUIManager가 호출: Ctrl키 상태에 따라 관리 가능 여부 설정.</summary>
    public void SetManageable(bool manageable)
    {
        isManageable = manageable;
        if (!isManageable) skillTooltip?.Hide();
    }

    /// <summary>
    /// SkillManager/CombatUIManager가 호출: 이 슬롯의 UI를 갱신.
    /// currentStack 인자:
    ///   - -1 (기본값): stack/charge 표시를 건드리지 않음 (차지 표시는 SetChargeDisplay 별도 호출)
    ///   -  0 이상: 기존 stack 표시 로직 적용
    /// </summary>
    public void UpdateDisplay(SkillData skillData, int currentStack = -1)
    {
        Debug.Log($"[{slotCategory}-{slotKey} 슬롯] UpdateDisplay 호출됨! 데이터: {(skillData != null ? skillData.skillName : "없음")}");
        currentSkill = skillData;
        if (currentSkill != null)
        {
            skillIconImage.sprite = currentSkill.skillIcon;
            skillIconImage.color = Color.white;
            skillIconImage.enabled = true;
        }
        else
        {
            skillIconImage.sprite = defaultIcon;
            skillIconImage.color = new Color(1, 1, 1, 0.5f);
            skillIconImage.enabled = (defaultIcon != null);
        }

        // currentStack이 음수면 stack/charge 표시 건드리지 않음 (차지 표시는 SetChargeDisplay 별도)
        if (currentStack >= 0)
        {
            bool showStack = currentStack > 0;

            if (stackContainerPanel != null)
                stackContainerPanel.SetActive(showStack);

            if (showStack && stackCountText != null)
                stackCountText.text = currentStack.ToString();
        }
    }

    /// <summary>
    /// 차지 스킬용 — 슬롯에 현재 차지 수 표시 (예: "3").
    /// 기존 stackContainerPanel + stackCountText 컴포넌트를 재활용.
    /// 
    /// currentCharges가 0이면 표시 OFF, 1 이상이면 숫자 표시 ON.
    /// 차지 스킬이 아닌 슬롯에 대해 0으로 호출하면 표시 자동 OFF.
    /// </summary>
    public void SetChargeDisplay(int currentCharges)
    {
        if (stackContainerPanel != null)
            stackContainerPanel.SetActive(currentCharges > 0);

        if (currentCharges > 0 && stackCountText != null)
            stackCountText.text = currentCharges.ToString();
    }

    // --- 1. 툴팁 ---
    public void OnPointerEnter(PointerEventData eventData)
    {
        SkillData data = null;
        int currentLvl = 1;
        List<EmberData> appliedEmbers = null;  // 적용된 불씨 목록 — 데이터 슬롯에 장착된 스킬만 보유, 폴백 시 null 유지

        // 본인 카테고리·키 슬롯에서 직접 조회 (활 슬롯은 검 스탠스에서도 본인 정보 표시 가능)
        if (SkillManager.Instance != null)
        {
            SkillSlot slot = SkillManager.Instance.GetSlot(slotCategory, slotKey);
            if (slot != null && !slot.IsEmpty)
            {
                data = slot.skill;
                currentLvl = slot.level;
                appliedEmbers = slot.appliedEmbers;  // 호버 시 SkillTooltipUI에 함께 전달
            }
        }

        // 폴백 — 고정 슬롯(LMB/RMB)의 fixedSkillData
        if (data == null) data = fixedSkillData;
        if (data == null) return;
        if (currentLvl <= 0) currentLvl = 1;

        if (isUpgradeMode)
        {
            // 강화 모드 → 미리보기 툴팁
            SkillTooltipUI.Instance.ShowUpgradePreview(data, currentLvl, transform.position, appliedEmbers);
        }
        else
        {
            // 일반 모드 → 일반 툴팁
            SkillTooltipUI.Instance.Show(data, currentLvl, transform.position, appliedEmbers);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SkillTooltipUI.Instance.Hide();
    }

    // --- 2. 장착 해제 (사진 3, 좌클릭) ---
    public void OnPointerClick(PointerEventData eventData)
    {
        // [NEW] 불씨 강화 모드 — 클릭 시 UpgradeModeController로 라우팅
        if (UpgradeModeController.Instance != null && UpgradeModeController.Instance.IsActive)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                UpgradeModeController.Instance.RequestEnhance(slotCategory, slotKey);
            }
            return;
        }

        // 1. 기존 재화 소모 강화 모드 (별개 시스템)
        if (isUpgradeMode)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                Debug.Log($"[SkillSlotUI] {slotKey} 강화 요청!");
                SkillUpgradeUI.Instance.TryUpgradeSkill(slotKey);
            }
            return;
        }

        // 2. 일반 모드 — 클릭만으로는 아무 동작 안 함
        //    (장착 해제는 데모 룰상 금지. 스왑은 드래그로만 가능)
    }

    // --- 3. 드래그 앤 드롭 ---
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        if (isManageable && currentSkill != null && slotBeingDragged == null)
        {
            slotBeingDragged = this;
            skillTooltip?.Hide();

            dragIconInstance = new GameObject("DragIcon");
            dragIconInstance.transform.SetParent(rootCanvas.transform, false);
            dragIconInstance.AddComponent<RectTransform>().sizeDelta = new Vector2(80, 80);
            Image img = dragIconInstance.AddComponent<Image>();
            img.sprite = currentSkill.skillIcon;
            img.raycastTarget = false;

            dragIconInstance.transform.position = eventData.position;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragIconInstance != null)
        {
            dragIconInstance.transform.position = eventData.position;
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (isManageable && slotBeingDragged != null && slotBeingDragged != this)
        {
            // 다른 카테고리 슬롯 간 스왑 거부 (검 ↔ 활 X)
            if (slotBeingDragged.slotCategory != this.slotCategory)
            {
                Debug.Log($"[SkillSlotUI] 다른 카테고리 슬롯 간 스왑 거부: {slotBeingDragged.slotCategory} ↔ {this.slotCategory}");
                return;
            }

            Debug.Log($"[SkillSlotUI] {slotCategory}: {slotBeingDragged.slotKey} → {this.slotKey} 스킬 스왑");
            skillManager.SwapSkills(this.slotCategory, slotBeingDragged.slotKey, this.slotKey);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (slotBeingDragged == this) // 내가 시작한 드래그가 끝났을 때
        {
            // 1. 임시 아이콘 파괴
            if (dragIconInstance != null)
            {
                Destroy(dragIconInstance);
            }

            // 2. UI 바깥/자기 자신에 드롭해도 장착 해제 X
            //    스킬 해제는 데모 룰상 금지 — 다른 슬롯에 드롭만 스왑 처리됨 (OnDrop에서)

            // 3. 정적 변수 리셋
            slotBeingDragged = null;
            dragIconInstance = null;
        }
    }

    public void SetUpgradeMode(bool active, int cost = 0)
    {
        isUpgradeMode = active;

        if (active)
        {
            if (costText != null)
            {
                costText.text = cost.ToString();
                costText.gameObject.SetActive(true);
            }
        }
        else
        {
            if (costText != null) costText.gameObject.SetActive(false);
        }
    }

    // KeyCode 반환 (CombatUIManager에서 사용)
    public KeyCode GetSlotKey() { return slotKey; }

    /// <summary>이 슬롯의 카테고리 반환 (CombatUIManager가 카테고리별로 슬롯 찾을 때 사용).</summary>
    public SkillCategory GetSlotCategory() { return slotCategory; }

    public Image GetCooldownOverlayImage() { return cooldownOverlayImage; }
}
