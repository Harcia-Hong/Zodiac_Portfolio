using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 노드맵의 개별 노드 슬롯 UI.
/// 노드 타입별 아이콘/색상 표시, 상태별 시각 변화, 클릭 처리를 담당한다.
/// NodeMapUI에서 프리팹으로 동적 생성된다.
/// </summary>
public class MapNodeSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // ============================
    //  Inspector 연결
    // ============================

    [Header("UI 요소")]
    [SerializeField, Tooltip("노드 아이콘 이미지")]
    private Image iconImage;

    [SerializeField, Tooltip("노드 배경 이미지 (원형 테두리)")]
    private Image borderImage;

    [SerializeField, Tooltip("노드 배경 채우기 이미지")]
    private Image fillImage;

    [SerializeField, Tooltip("현재 위치 표시 이펙트 (깜빡임 등)")]
    private GameObject currentMarker;

    [SerializeField, Tooltip("완료 체크 표시")]
    private GameObject completedMark;

    [SerializeField, Tooltip("잠금 표시 (자물쇠 등)")]
    private GameObject lockedOverlay;

    [Header("툴팁")]
    [SerializeField, Tooltip("마우스 호버 시 표시할 이름 텍스트")]
    private TextMeshProUGUI tooltipText;

    [SerializeField, Tooltip("툴팁 배경")]
    private GameObject tooltipRoot;

    [Header("타입별 아이콘")]
    [SerializeField] private Sprite combatIcon;
    [SerializeField] private Sprite shopIcon;
    [SerializeField] private Sprite restIcon;
    [SerializeField] private Sprite eventIcon;
    [SerializeField] private Sprite bossIcon;

    [Header("상태별 색상")]
    [SerializeField] private Color lockedColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
    [SerializeField] private Color availableColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color currentColor = new Color(0.2f, 1f, 0.5f, 1f);
    [SerializeField] private Color completedColor = new Color(0.6f, 0.6f, 0.6f, 0.8f);
    [SerializeField] private Color skippedColor = new Color(0.4f, 0.4f, 0.4f, 0.4f);

    // ============================
    //  런타임 변수
    // ============================

    private NodeData nodeData;
    private Button button;
    private System.Action<NodeData> onClickCallback;

    // ============================
    //  초기화
    // ============================

    private void Awake()
    {
        button = GetComponent<Button>();

        // 툴팁 숨김
        if (tooltipRoot != null)
            tooltipRoot.SetActive(false);

        // 마커 숨김
        if (currentMarker != null) currentMarker.SetActive(false);
        if (completedMark != null) completedMark.SetActive(false);
        if (lockedOverlay != null) lockedOverlay.SetActive(false);
    }

    /// <summary>
    /// 슬롯 초기화. NodeMapUI.CreateNodeSlots()에서 호출.
    /// </summary>
    /// <param name="data">이 슬롯이 표시할 노드 데이터</param>
    /// <param name="onClick">클릭 시 호출할 콜백</param>
    public void Setup(NodeData data, System.Action<NodeData> onClick)
    {
        nodeData = data;
        onClickCallback = onClick;

        // 버튼 이벤트 연결
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClicked);
        }

        // 타입별 아이콘 설정
        SetIcon(data.stageType);

        // 툴팁 텍스트 설정
        SetTooltip(data);

        // 시각 상태 반영
        UpdateVisual(data);
    }

    // ============================
    //  시각 업데이트
    // ============================

    /// <summary>
    /// 노드 상태에 따라 시각 요소를 갱신한다.
    /// 맵을 열 때, 노드 선택 후 등에 호출.
    /// </summary>
    public void UpdateVisual(NodeData data)
    {
        nodeData = data;

        // 상태별 색상 및 인터랙션 설정
        switch (data.state)
        {
            case NodeState.Locked:
                ApplyState(lockedColor, false);
                SetMarkers(false, false, true);
                break;

            case NodeState.Available:
                ApplyState(availableColor, true);
                SetMarkers(false, false, false);
                break;

            case NodeState.Current:
                ApplyState(currentColor, false);
                SetMarkers(true, false, false);
                break;

            case NodeState.Completed:
                ApplyState(completedColor, false);
                SetMarkers(false, true, false);
                break;

            case NodeState.Skipped:
                ApplyState(skippedColor, false);
                SetMarkers(false, false, false);
                break;
        }

        // 노드 타입 색상을 테두리에 적용 (Available/Current일 때만 선명하게)
        if (borderImage != null)
        {
            Color typeColor = data.nodeColor;

            if (data.state == NodeState.Locked || data.state == NodeState.Skipped)
                typeColor *= 0.4f;
            else if (data.state == NodeState.Completed)
                typeColor *= 0.6f;

            typeColor.a = 1f;
            borderImage.color = typeColor;
        }
    }

    /// <summary>
    /// 색상과 버튼 인터랙션을 적용한다.
    /// </summary>
    private void ApplyState(Color color, bool interactable)
    {
        if (fillImage != null)
            fillImage.color = color;

        if (iconImage != null)
        {
            Color iconColor = iconImage.color;
            iconColor.a = color.a;
            iconImage.color = iconColor;
        }

        if (button != null)
            button.interactable = interactable;
    }

    /// <summary>
    /// 마커(현재/완료/잠금) 표시를 설정한다.
    /// </summary>
    private void SetMarkers(bool current, bool completed, bool locked)
    {
        if (currentMarker != null) currentMarker.SetActive(current);
        if (completedMark != null) completedMark.SetActive(completed);
        if (lockedOverlay != null) lockedOverlay.SetActive(locked);
    }

    // ============================
    //  아이콘 / 툴팁
    // ============================

    /// <summary>
    /// 노드 타입에 맞는 아이콘을 설정한다.
    /// </summary>
    private void SetIcon(StageType type)
    {
        if (iconImage == null) return;

        Sprite icon = type switch
        {
            StageType.Combat => combatIcon,
            StageType.Shop => shopIcon,
            StageType.Rest => restIcon,
            StageType.Event => eventIcon,
            StageType.Boss => bossIcon,
            _ => combatIcon
        };

        if (icon != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = true;
        }
        else
        {
            iconImage.enabled = false;
        }
    }

    /// <summary>
    /// 툴팁 텍스트를 설정한다.
    /// </summary>
    private void SetTooltip(NodeData data)
    {
        if (tooltipText == null) return;

        string tooltip = data.displayName;

        // 재앙이 있으면 표시
        if (data.calamityData != null)
        {
            tooltip += $"\n<color=#FF4444><size=80%>재앙: {data.calamityData.calamityName}</size></color>";
        }

        tooltipText.text = tooltip;
    }

    // ============================
    //  입력 처리
    // ============================

    /// <summary>
    /// 노드 클릭 시 호출.
    /// </summary>
    private void OnClicked()
    {
        if (nodeData == null) return;
        if (nodeData.state != NodeState.Available) return;

        onClickCallback?.Invoke(nodeData);
    }

    /// <summary>
    /// 마우스 호버 진입 시 툴팁 표시.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltipRoot != null && nodeData != null)
        {
            // Locked 상태에서는 툴팁 숨김
            if (nodeData.state == NodeState.Locked) return;

            tooltipRoot.SetActive(true);
        }
    }

    /// <summary>
    /// 마우스 호버 이탈 시 툴팁 숨김.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipRoot != null)
            tooltipRoot.SetActive(false);
    }

    // ============================
    //  공개 프로퍼티
    // ============================

    /// <summary>이 슬롯이 담고 있는 노드 데이터</summary>
    public NodeData Data => nodeData;
}
