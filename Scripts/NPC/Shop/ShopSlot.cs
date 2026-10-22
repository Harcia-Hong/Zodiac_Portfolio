using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 상점 슬롯 1개
/// - 아이콘, 강화 레벨, 가격 표시
/// - 마우스 호버 시 ShopUI에 툴팁 요청
/// - 클릭 시 구매 요청
/// </summary>
public class ShopSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI 요소")]
    [SerializeField, Tooltip("아이템 아이콘")]
    private Image iconImage;

    [SerializeField, Tooltip("강화 레벨 텍스트 (예: Lv.2)")]
    private TextMeshProUGUI levelText;

    [SerializeField, Tooltip("가격 텍스트")]
    private TextMeshProUGUI priceText;

    [SerializeField, Tooltip("품질 텍스트 (소켓 전용)")]
    private TextMeshProUGUI qualityText;

    [SerializeField, Tooltip("품절 오버레이")]
    private GameObject soldOutOverlay;

    [SerializeField, Tooltip("구매 버튼")]
    private Button purchaseButton;

    // === 런타임 ===
    private ShopItemData itemData;
    private int slotIndex;

    private void Awake()
    {
        if (soldOutOverlay != null)
            soldOutOverlay.SetActive(false);

        if (purchaseButton != null)
            purchaseButton.onClick.AddListener(OnPurchaseClicked);
    }

    /// <summary>
    /// 슬롯 초기 세팅 — ShopUI.RefreshSlots()에서 호출
    /// </summary>
    public void Setup(ShopItemData data, int index)
    {
        itemData = data;
        slotIndex = index;

        // 아이콘
        if (iconImage != null && data.icon != null)
        {
            iconImage.sprite = data.icon;
            iconImage.enabled = true;
        }

        // 강화 레벨
        if (levelText != null)
        {
            if (data.enhanceLevel > 0)
            {
                levelText.text = $"+{data.enhanceLevel}";
                levelText.gameObject.SetActive(true);
            }
            else
            {
                levelText.gameObject.SetActive(false);
            }
        }

        // 가격
        if (priceText != null)
        {
            priceText.text = data.price.ToString();
        }

        // 품질 (소켓만)
        if (qualityText != null)
        {
            if (data.itemType == ShopItemType.Socket)
            {
                qualityText.text = $"{data.quality}%";
                qualityText.gameObject.SetActive(true);
            }
            else
            {
                qualityText.gameObject.SetActive(false);
            }
        }

        // 품절 상태
        if (soldOutOverlay != null)
            soldOutOverlay.SetActive(data.isSoldOut);

        // 구매 가능 여부에 따라 가격 색상
        UpdatePriceColor();
    }

    /// <summary>
    /// 품절 처리
    /// </summary>
    public void SetSoldOut()
    {
        if (itemData != null)
            itemData.isSoldOut = true;

        if (soldOutOverlay != null)
            soldOutOverlay.SetActive(true);

        if (purchaseButton != null)
            purchaseButton.interactable = false;
    }

    // ============================================================
    // 이벤트 핸들러
    // ============================================================

    /// <summary>
    /// 마우스 올렸을 때 — 툴팁 표시
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (itemData == null || itemData.isSoldOut) return;

        ShopUI.Instance?.ShowTooltip(itemData, transform.position);
    }

    /// <summary>
    /// 마우스 벗어났을 때 — 툴팁 숨김
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        ShopUI.Instance?.HideTooltip();
    }

    /// <summary>
    /// 클릭 — 구매
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        OnPurchaseClicked();
    }

    /// <summary>
    /// 구매 버튼 클릭
    /// </summary>
    private void OnPurchaseClicked()
    {
        if (itemData == null || itemData.isSoldOut) return;

        ShopUI.Instance?.OnSlotPurchase(slotIndex);
    }

    // ============================================================
    // 유틸
    // ============================================================

    private void UpdatePriceColor()
    {
        // 가격 색상 항상 금색 유지
        if (priceText != null)
            priceText.color = new Color(1f, 0.84f, 0f); // FFD700
    }
}
