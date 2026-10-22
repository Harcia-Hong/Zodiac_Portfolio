using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// "지식의 서고" 좌측 리스트의 행 하나를 담당.
/// 항목 이름 표시, 클릭 시 선택 콜백 호출, 선택 하이라이트 처리.
/// KnowledgeArchiveUI에서 Prefab으로 동적 생성된다.
/// </summary>
public class UpgradeSlot : MonoBehaviour
{
    [Header("UI 요소")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject selectedHighlight;
    [SerializeField] private Image iconImage;

    [Header("색상 설정")]
    [SerializeField] private Color normalColor = new Color(0.3f, 0.3f, 0.2f, 0.8f);
    [SerializeField] private Color selectedColor = new Color(0.4f, 0.4f, 0.25f, 1f);
    [SerializeField] private Color hiddenColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    [SerializeField] private Color maxLevelColor = new Color(0.3f, 0.4f, 0.3f, 0.8f);

    // --- 런타임 ---
    private StatUpgradeDataSO data;
    private bool isVisible;
    private System.Action<StatUpgradeDataSO> onSelectedCallback;

    /// <summary>이 슬롯이 담고 있는 데이터</summary>
    public StatUpgradeDataSO Data => data;

    /// <summary>
    /// 슬롯을 초기화한다. KnowledgeArchiveUI.PopulateList()에서 호출.
    /// </summary>
    /// <param name="upgradeData">강화 항목 데이터</param>
    /// <param name="visible">공개 여부 (false면 ??? 표시)</param>
    /// <param name="currentLevel">현재 강화 레벨</param>
    /// <param name="onSelected">클릭 시 호출할 콜백</param>
    public void Setup(StatUpgradeDataSO upgradeData, bool visible, int currentLevel, System.Action<StatUpgradeDataSO> onSelected)
    {
        data = upgradeData;
        isVisible = visible;
        onSelectedCallback = onSelected;

        // 이름 표시
        if (nameText != null)
        {
            if (isVisible)
                nameText.text = upgradeData.upgradeName;
            else
                nameText.text = "???";
        }

        // 배경 색상 설정
        UpdateBackgroundColor(currentLevel);

        // 하이라이트 비활성화
        SetSelected(false);

        // 버튼 이벤트 연결
        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClicked);
        }
    }

    /// <summary>
    /// 슬롯 클릭 시 호출
    /// </summary>
    private void OnClicked()
    {
        if (data != null && onSelectedCallback != null)
            onSelectedCallback.Invoke(data);
    }

    /// <summary>
    /// 선택 상태를 설정 (하이라이트 표시/숨김)
    /// </summary>
    public void SetSelected(bool selected)
    {
        // 하이라이트 오브젝트 켜기/끄기
        if (selectedHighlight != null)
            selectedHighlight.SetActive(selected);

        // 배경 색상도 변경
        if (backgroundImage != null && data != null)
        {
            if (selected)
                backgroundImage.color = selectedColor;
            else
                UpdateBackgroundColor(KnowledgeArchiveManager.Instance != null
                    ? KnowledgeArchiveManager.Instance.GetCurrentLevel(data) : 0);
        }
    }

    /// <summary>
    /// 현재 레벨에 따라 배경 색상을 설정
    /// </summary>
    private void UpdateBackgroundColor(int currentLevel)
    {
        if (backgroundImage == null) return;

        if (!isVisible)
        {
            // 비공개 항목: 어두운 색
            backgroundImage.color = hiddenColor;
        }
        else if (data != null && currentLevel >= data.maxLevel)
        {
            // 만렙: 초록빛
            backgroundImage.color = maxLevelColor;
        }
        else
        {
            // 일반 상태
            backgroundImage.color = normalColor;
        }
    }
}