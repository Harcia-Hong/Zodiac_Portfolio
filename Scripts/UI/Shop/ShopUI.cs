using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 상점 UI 컨트롤러
/// - 2x3 그리드로 스킬 3개 + 소켓 3개 표시
/// - 슬롯 호버 시 상세 설명 툴팁
/// - 클릭으로 구매
/// - ESC로 닫기
/// </summary>
public class ShopUI : MonoBehaviour
{
    public static ShopUI Instance { get; private set; }

    [Header("패널")]
    [SerializeField, Tooltip("상점 UI 전체 패널")]
    private GameObject shopPanel;

    [Header("슬롯 (6개 — 상단 스킬3, 하단 소켓3)")]
    [SerializeField, Tooltip("상점 슬롯 6개 (순서: 스킬1,2,3 → 소켓1,2,3)")]
    private ShopSlot[] shopSlots;

    // === 런타임 ===
    private bool isOpen = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (shopPanel != null)
            shopPanel.SetActive(false);
    }

    private void Update()
    {
        if (!isOpen) return;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D)) CloseShop();
    }

    // ============================================================
    // 열기 / 닫기
    // ============================================================

    /// <summary>
    /// 상점 열기 — ShopNPC에서 호출
    /// </summary>
    public void OpenShop()
    {
        if (ShopManager.Instance == null) return;

        isOpen = true;

        // UI 표시
        if (shopPanel != null)
            shopPanel.SetActive(true);

        if (CombatUIManager.Instance != null)
            CombatUIManager.Instance.ExpandUI(true, true);

        if (shopPanel != null)
            shopPanel.SetActive(true);

        // 슬롯 세팅
        RefreshSlots();

        // 플레이어 잠금
        LockPlayer(true);

        Debug.Log("[ShopUI] 상점 열림");
    }

    /// <summary>
    /// 상점 닫기
    /// </summary>
    public void CloseShop()
    {
        isOpen = false;

        // 상점 패널 숨김
        if (shopPanel != null)
            shopPanel.SetActive(false);

        // 기존 툴팁 숨김
        if (SkillTooltipUI.Instance != null)    
            SkillTooltipUI.Instance.Hide();

        // 확장 화면 해제
        if (CombatUIManager.Instance != null)
            CombatUIManager.Instance.ExpandUI(false);

        // 플레이어 잠금 해제
        LockPlayer(false);

        Debug.Log("[ShopUI] 상점 닫힘");
    }

    /// <summary>
    /// 상점 열려있는지 확인
    /// </summary>
    public bool IsOpen() => isOpen;

    // ============================================================
    // 슬롯 관리
    // ============================================================

    /// <summary>
    /// 모든 슬롯 갱신
    /// </summary>
    private void RefreshSlots()
    {
        List<ShopItemData> items = ShopManager.Instance.GetShopItems();

        for (int i = 0; i < shopSlots.Length; i++)
        {
            if (i < items.Count && items[i] != null)
            {
                shopSlots[i].Setup(items[i], i);
            }
        }
    }

    // ============================================================
    // 구매 처리
    // ============================================================

    /// <summary>
    /// 슬롯에서 구매 버튼 클릭 시 호출
    /// </summary>
    public void OnSlotPurchase(int slotIndex)
    {
        if (ShopManager.Instance == null) return;

        bool success = ShopManager.Instance.PurchaseItem(slotIndex);

        if (success)
        {
            // 슬롯 품절 표시
            if (slotIndex < shopSlots.Length)
                shopSlots[slotIndex].SetSoldOut();

            // 툴팁 숨김
            if (SkillTooltipUI.Instance != null)
                SkillTooltipUI.Instance.Hide();
        }
    }

    // ============================================================
    // 툴팁
    // ============================================================

    /// <summary>
    /// 슬롯 호버 시 툴팁 표시 — 기존 SkillTooltipUI 사용
    /// </summary>
    public void ShowTooltip(ShopItemData item, Vector3 slotWorldPosition)
    {
        if (item == null || SkillTooltipUI.Instance == null) return;

        // World Space → Screen Space 변환
        Vector3 screenPos = Camera.main.WorldToScreenPoint(slotWorldPosition);
        screenPos.y -= 50f;

        if (item.itemType == ShopItemType.Skill && item.skillData != null)
        {
            int level = item.enhanceLevel + 1;
            SkillTooltipUI.Instance.Show(item.skillData, level, screenPos);
        }
        else if (item.itemType == ShopItemType.Socket && item.socketData != null)
        {
            Debug.Log($"[ShopUI] 소켓 툴팁: {item.displayName} (품질 {item.quality}%)");
        }
    }


    /// <summary>
    /// 툴팁 숨김
    /// </summary>
    public void HideTooltip()
    {
        if (SkillTooltipUI.Instance != null)
            SkillTooltipUI.Instance.Hide();

    }

    // ============================================================
    // 유틸
    // ============================================================
    /// <summary>
    /// 플레이어 이동 잠금/해제
    /// </summary>
    private void LockPlayer(bool locked)
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            player.isSkillCasting = locked;
    }
}
