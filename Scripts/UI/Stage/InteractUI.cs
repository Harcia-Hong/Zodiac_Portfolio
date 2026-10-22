using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 범용 상호작용 UI (Screen Space Overlay)
/// - 플레이어가 상호작용 가능한 오브젝트 근처에 가면 표시
/// - 상단: 오브젝트 정보 (예: "2층 - 전투", "스킬 강화", "보물 상자")
/// - 하단: 입력 안내 (예: "F 이동", "F 열기", "F 강화")
/// - 어떤 오브젝트에서든 Show/Hide로 호출
/// </summary>
public class InteractUI : MonoBehaviour
{
    public static InteractUI Instance { get; private set; }

    [Header("UI 루트")]
    [SerializeField, Tooltip("전체 패널 (활성화/비활성화용)")]
    private GameObject panel;

    [Header("상단 — 오브젝트 정보")]
    [SerializeField, Tooltip("메인 정보 텍스트")]
    private TextMeshProUGUI infoText;

    [SerializeField, Tooltip("상단 배경")]
    private Image infoBg;

    [Header("하단 — 입력 안내")]
    [SerializeField, Tooltip("행동 안내 텍스트")]
    private TextMeshProUGUI actionText;

    [SerializeField, Tooltip("하단 배경")]
    private Image actionBg;

    [Header("배경 색상")]
    [SerializeField] private Color defaultBgColor = new Color(0f, 0f, 0f, 0.7f);

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        Hide();
    }

    /// <summary>
    /// 상호작용 UI 표시
    /// </summary>
    /// <param name="info">상단 텍스트 (예: "2층 - 전투")</param>
    /// <param name="action">하단 텍스트 (예: "이동")</param>
    /// <param name="key">입력 키 표시 (예: "F")</param>
    /// <param name="infoColor">상단 텍스트 색상 (null이면 흰색)</param>
    public void Show(string info, string action = "상호작용", string key = "F", Color? infoColor = null)
    {
        if (panel == null) return;

        // 상단: 오브젝트 정보
        if (infoText != null)
        {
            infoText.text = info;
            infoText.color = infoColor ?? Color.white;
        }

        // 하단: 키 + 행동
        if (actionText != null)
        {
            actionText.text = $"<color=#FFD700>{key}</color>  {action}";
        }

        // 배경 색상
        if (infoBg != null) infoBg.color = defaultBgColor;
        if (actionBg != null) actionBg.color = defaultBgColor;

        panel.SetActive(true);
    }

    /// <summary>
    /// UI 숨김
    /// </summary>
    public void Hide()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    /// <summary>
    /// 현재 표시 중인지 확인
    /// </summary>
    public bool IsShowing()
    {
        return panel != null && panel.activeSelf;
    }
}
