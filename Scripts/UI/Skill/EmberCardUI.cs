using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 불씨 카드 1장의 UI 컴포넌트.
/// 
/// 상태 3가지:
///   - Active: 현재 픽 가능 (활성 색, 클릭 핸들러 동작)
///   - Picked: 과거 레벨에서 이미 픽한 카드 (회색, 클릭 불가)
///   - Empty: 빈 슬롯 (미래 레벨 placeholder 또는 표시 X)
/// 
/// 부모 EmberSelectionUI가 SetEmber()로 데이터 + 상태 주입.
/// 클릭 시 OnCardClicked 콜백으로 부모에 알림.
/// </summary>
public class EmberCardUI : MonoBehaviour
{
    // ========================================================================
    // 카드 상태
    // ========================================================================

    public enum CardState
    {
        Active,   // 현재 픽 가능
        Picked,   // 이미 픽한 카드 (회색)
        Empty     // 빈 슬롯
    }

    // ========================================================================
    // UI 참조
    // ========================================================================

    [Header("UI 참조")]
    [SerializeField] private GameObject contentRoot;        // 카드 내용 컨테이너 (Empty 시 비활성)
    [SerializeField] private Image iconImage;               // 불씨 아이콘
    [SerializeField] private TextMeshProUGUI nameText;      // 불씨 이름
    [SerializeField] private TextMeshProUGUI descriptionText; // 효과 설명
    [SerializeField] private Button clickButton;            // 클릭 버튼
    [SerializeField] private CanvasGroup canvasGroup;       // 회색 처리용 alpha 조절

    [Header("상태별 색상")]
    [Tooltip("Active 상태의 alpha (1 = 완전 불투명).")]
    [SerializeField] private float activeAlpha = 1f;

    [Tooltip("Picked 상태의 alpha (0.4 정도 = 회색 톤).")]
    [SerializeField] private float pickedAlpha = 0.4f;

    // ========================================================================
    // 내부 상태
    // ========================================================================

    private EmberData currentEmber;
    private CardState currentState;
    private System.Action<EmberData> onClickCallback;

    // ========================================================================
    // Unity Lifecycle
    // ========================================================================

    private void Awake()
    {
        if (clickButton != null)
        {
            clickButton.onClick.AddListener(HandleClick);
        }
    }

    // ========================================================================
    // 외부 API
    // ========================================================================

    /// <summary>
    /// 카드에 불씨 데이터 + 상태 설정.
    /// 부모 EmberSelectionUI가 호출.
    /// </summary>
    /// <param name="ember">표시할 불씨 (Empty 상태면 null 가능)</param>
    /// <param name="state">카드 상태</param>
    /// <param name="onClick">Active 상태일 때 클릭 시 호출될 콜백</param>
    public void SetEmber(EmberData ember, CardState state, System.Action<EmberData> onClick = null)
    {
        currentEmber = ember;
        currentState = state;
        onClickCallback = onClick;

        ApplyVisuals();
    }

    /// <summary>빈 슬롯으로 리셋 (화면 닫을 때 호출).</summary>
    public void Clear()
    {
        currentEmber = null;
        currentState = CardState.Empty;
        onClickCallback = null;
        ApplyVisuals();
    }

    // ========================================================================
    // 내부 처리
    // ========================================================================

    /// <summary>현재 상태에 맞게 UI 시각 갱신.</summary>
    private void ApplyVisuals()
    {
        // Empty 상태 → 내용 숨김
        if (currentState == CardState.Empty || currentEmber == null)
        {
            if (contentRoot != null) contentRoot.SetActive(false);
            if (clickButton != null) clickButton.interactable = false;
            return;
        }

        // 내용 표시 ON
        if (contentRoot != null) contentRoot.SetActive(true);

        // 아이콘·이름·설명 갱신
        if (iconImage != null && currentEmber.icon != null)
        {
            iconImage.sprite = currentEmber.icon;
            iconImage.enabled = true;
        }
        else if (iconImage != null)
        {
            iconImage.enabled = false;
        }

        if (nameText != null) nameText.text = currentEmber.emberName;
        if (descriptionText != null) descriptionText.text = currentEmber.GetFormattedDescription();

        // 상태별 처리
        switch (currentState)
        {
            case CardState.Active:
                if (canvasGroup != null) canvasGroup.alpha = activeAlpha;
                if (clickButton != null) clickButton.interactable = true;
                break;

            case CardState.Picked:
                if (canvasGroup != null) canvasGroup.alpha = pickedAlpha;
                if (clickButton != null) clickButton.interactable = false;
                break;
        }
    }

    /// <summary>카드 클릭 시 — Active 상태일 때만 콜백 호출.</summary>
    private void HandleClick()
    {
        if (currentState != CardState.Active) return;
        if (currentEmber == null) return;

        onClickCallback?.Invoke(currentEmber);
    }
}
