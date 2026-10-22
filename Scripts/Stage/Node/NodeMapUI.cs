using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 노드맵 UI 컨트롤러.
/// NodeMapGenerator의 데이터를 기반으로 노드맵을 화면에 표시한다.
/// 노드 슬롯 생성, 연결선 그리기, 스크롤, 열기/닫기를 담당.
/// </summary>
public class NodeMapUI : MonoBehaviour
{
    public static NodeMapUI Instance { get; private set; }

    // ============================
    //  Inspector 연결
    // ============================

    [Header("전체 패널")]
    [SerializeField, Tooltip("노드맵 UI 루트 패널")]
    private GameObject mapPanel;

    [Header("맵 컨테이너")]
    [SerializeField, Tooltip("노드와 연결선이 배치될 RectTransform")]
    private RectTransform mapContainer;

    [SerializeField, Tooltip("맵 컨테이너의 스크롤 영역")]
    private ScrollRect scrollRect;

    [Header("프리팹")]
    [SerializeField, Tooltip("노드 슬롯 프리팹 (MapNodeSlot 컴포넌트 필수)")]
    private GameObject nodeSlotPrefab;

    [SerializeField, Tooltip("연결선 프리팹 (Image 컴포넌트, 얇은 직선)")]
    private GameObject linePrefab;

    [Header("맵 레이아웃")]
    [SerializeField, Tooltip("맵 전체 너비 (픽셀)")]
    private float mapWidth = 2400f;

    [SerializeField, Tooltip("맵 전체 높이 (픽셀)")]
    private float mapHeight = 600f;

    [SerializeField, Tooltip("좌우 여백 (픽셀)")]
    private float horizontalPadding = 150f;

    [SerializeField, Tooltip("상하 여백 (픽셀)")]
    private float verticalPadding = 80f;

    [Header("연결선 설정")]
    [SerializeField, Tooltip("기본 연결선 색상")]
    private Color defaultLineColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);

    [SerializeField, Tooltip("이동한 경로 연결선 색상")]
    private Color traveledLineColor = new Color(0.2f, 0.9f, 0.4f, 0.9f);

    [SerializeField, Tooltip("연결선 두께")]
    private float lineThickness = 4f;

    [Header("상단 정보")]
    [SerializeField, Tooltip("챕터/층 표시 텍스트")]
    private TextMeshProUGUI chapterText;

    // ============================
    //  런타임 변수
    // ============================

    private bool isOpen = false;
    private Dictionary<string, MapNodeSlot> nodeSlots = new Dictionary<string, MapNodeSlot>();
    private List<GameObject> lineObjects = new List<GameObject>();

    // HUD 캐싱
    private GameObject gameUI;
    private GameObject combatCanvas;

    // ============================
    //  초기화
    // ============================

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (mapPanel != null)
            mapPanel.SetActive(false);
    }

    private void Update()
    {
        if (!isOpen) return;

        // ESC로 닫기
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
    }

    // ============================
    //  열기 / 닫기
    // ============================

    /// <summary>
    /// 노드맵 UI를 열고 현재 맵 상태를 표시한다.
    /// 스테이지 클리어 후 포탈 상호작용 시 호출.
    /// </summary>
    public void Open()
    {
        if (mapPanel == null) return;
        if (NodeMapGenerator.Instance == null) return;

        isOpen = true;
        mapPanel.SetActive(true);

        // HUD 숨기기
        SetHUDVisible(false);

        // 맵 구축
        BuildMap();

        // 현재 노드 위치로 스크롤
        ScrollToCurrentNode();

        // 챕터 텍스트 갱신
        UpdateChapterText();

        // 플레이어 입력 차단
        LockPlayer(true);

        // 커서 표시
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("[NodeMapUI] 노드맵 열림");
    }

    /// <summary>
    /// 노드맵 UI를 닫는다.
    /// </summary>
    public void Close()
    {
        if (mapPanel == null) return;

        isOpen = false;
        mapPanel.SetActive(false);

        // HUD 복원
        SetHUDVisible(true);

        // 플레이어 입력 해제
        LockPlayer(false);

        Debug.Log("[NodeMapUI] 노드맵 닫힘");
    }

    /// <summary>UI가 열려있는지 확인</summary>
    public bool IsOpen() => isOpen;

    // ============================
    //  맵 구축
    // ============================

    /// <summary>
    /// NodeMapGenerator의 데이터를 기반으로 노드 슬롯과 연결선을 생성한다.
    /// </summary>
    private void BuildMap()
    {
        // 기존 슬롯/연결선 정리
        ClearMap();

        if (NodeMapGenerator.Instance == null) return;

        var columnNodes = NodeMapGenerator.Instance.ColumnNodes;
        if (columnNodes == null || columnNodes.Count == 0) return;

        // 맵 컨테이너 크기 설정
        mapContainer.sizeDelta = new Vector2(mapWidth, mapHeight);

        // 노드 슬롯 생성
        CreateNodeSlots(columnNodes);

        // 연결선 생성
        CreateConnectionLines(columnNodes);

        // 슬롯 상태 갱신
        RefreshAllSlots();
    }

    /// <summary>
    /// 모든 노드의 UI 슬롯을 생성하고 배치한다.
    /// </summary>
    private void CreateNodeSlots(List<List<NodeData>> columnNodes)
    {
        foreach (var column in columnNodes)
        {
            foreach (var nodeData in column)
            {
                GameObject slotObj = Instantiate(nodeSlotPrefab, mapContainer);
                MapNodeSlot slot = slotObj.GetComponent<MapNodeSlot>();

                if (slot == null)
                {
                    Debug.LogError("[NodeMapUI] 노드 슬롯 프리팹에 MapNodeSlot 컴포넌트가 없습니다!");
                    Destroy(slotObj);
                    continue;
                }

                // 슬롯 초기화
                slot.Setup(nodeData, OnNodeClicked);

                // UI 위치 설정
                RectTransform slotRect = slotObj.GetComponent<RectTransform>();
                slotRect.anchoredPosition = CalculateSlotPosition(nodeData.mapPosition);

                // 등록
                nodeSlots[nodeData.nodeId] = slot;
            }
        }
    }

    /// <summary>
    /// 노드 간 연결선을 생성한다.
    /// </summary>
    private void CreateConnectionLines(List<List<NodeData>> columnNodes)
    {
        foreach (var column in columnNodes)
        {
            foreach (var nodeData in column)
            {
                foreach (string nextId in nodeData.connectedNextIds)
                {
                    if (!nodeSlots.ContainsKey(nodeData.nodeId)) continue;
                    if (!nodeSlots.ContainsKey(nextId)) continue;

                    Vector2 fromPos = CalculateSlotPosition(nodeData.mapPosition);
                    Vector2 toPos = CalculateSlotPosition(
                        NodeMapGenerator.Instance.AllNodes[nextId].mapPosition
                    );

                    // 연결선 색상 결정 (둘 다 Completed면 이동 경로)
                    bool isTraveled = nodeData.state == NodeState.Completed
                        && NodeMapGenerator.Instance.AllNodes[nextId].state != NodeState.Locked;

                    CreateLine(fromPos, toPos, isTraveled);
                }
            }
        }
    }

    /// <summary>
    /// 두 지점 사이에 연결선(Image)을 생성한다.
    /// </summary>
    private void CreateLine(Vector2 from, Vector2 to, bool isTraveled)
    {
        if (linePrefab == null) return;

        GameObject lineObj = Instantiate(linePrefab, mapContainer);
        RectTransform lineRect = lineObj.GetComponent<RectTransform>();
        Image lineImage = lineObj.GetComponent<Image>();

        if (lineRect == null || lineImage == null)
        {
            Destroy(lineObj);
            return;
        }

        // 연결선을 노드 뒤에 배치 (노드보다 먼저 그려지도록)
        lineObj.transform.SetAsFirstSibling();

        // 두 점 사이 거리와 각도 계산
        Vector2 direction = to - from;
        float distance = direction.magnitude;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // 위치: 두 점의 중간
        lineRect.anchoredPosition = from + direction * 0.5f;

        // 크기: 길이 = 거리, 높이 = 두께
        lineRect.sizeDelta = new Vector2(distance, lineThickness);

        // 회전
        lineRect.localRotation = Quaternion.Euler(0, 0, angle);

        // 색상
        lineImage.color = isTraveled ? traveledLineColor : defaultLineColor;

        lineObjects.Add(lineObj);
    }

    // ============================
    //  노드 클릭 처리
    // ============================

    /// <summary>
    /// 노드 슬롯이 클릭되었을 때 호출되는 콜백.
    /// </summary>
    private void OnNodeClicked(NodeData nodeData)
    {
        if (nodeData.state != NodeState.Available)
        {
            Debug.Log($"[NodeMapUI] 선택 불가 노드: {nodeData.nodeId} ({nodeData.state})");
            return;
        }

        // NodeMapGenerator에 선택 전달
        FloorChoice choice = NodeMapGenerator.Instance.SelectNode(nodeData.nodeId);

        if (choice == null) return;

        // UI 닫기
        Close();

        // StageManager에 선택 결과 전달
        if (StageManager.Instance != null)
        {
            StageManager.Instance.OnPortalEntered(choice);
        }

        Debug.Log($"[NodeMapUI] 노드 선택 완료: {nodeData.displayName}");
    }

    // ============================
    //  슬롯 상태 갱신
    // ============================

    /// <summary>
    /// 모든 노드 슬롯의 시각 상태를 갱신한다.
    /// </summary>
    private void RefreshAllSlots()
    {
        foreach (var pair in nodeSlots)
        {
            if (NodeMapGenerator.Instance.AllNodes.ContainsKey(pair.Key))
            {
                NodeData data = NodeMapGenerator.Instance.AllNodes[pair.Key];
                pair.Value.UpdateVisual(data);
            }
        }
    }

    // ============================
    //  스크롤
    // ============================

    /// <summary>
    /// 현재 노드 위치로 스크롤을 이동한다.
    /// </summary>
    private void ScrollToCurrentNode()
    {
        if (scrollRect == null) return;

        NodeData currentNode = NodeMapGenerator.Instance?.GetCurrentNode();
        if (currentNode == null) return;

        // 정규화된 스크롤 위치 (0=왼쪽, 1=오른쪽)
        float normalizedX = currentNode.mapPosition.x;

        // 약간 왼쪽 여유를 두고 스크롤
        normalizedX = Mathf.Clamp01(normalizedX - 0.1f);

        scrollRect.horizontalNormalizedPosition = normalizedX;
    }

    // ============================
    //  좌표 계산
    // ============================

    /// <summary>
    /// 정규화 좌표(0~1)를 실제 UI 앵커 좌표로 변환한다.
    /// </summary>
    private Vector2 CalculateSlotPosition(Vector2 normalizedPos)
    {
        float x = horizontalPadding + normalizedPos.x * (mapWidth - horizontalPadding * 2);
        float y = verticalPadding + normalizedPos.y * (mapHeight - verticalPadding * 2);

        // 앵커가 좌하단 기준이므로 보정
        x -= mapWidth * 0.5f;
        y -= mapHeight * 0.5f;

        return new Vector2(x, y);
    }

    // ============================
    //  정리
    // ============================

    /// <summary>
    /// 기존 노드 슬롯과 연결선을 모두 제거한다.
    /// </summary>
    private void ClearMap()
    {
        foreach (var slot in nodeSlots.Values)
        {
            if (slot != null)
                Destroy(slot.gameObject);
        }
        nodeSlots.Clear();

        foreach (var line in lineObjects)
        {
            if (line != null)
                Destroy(line);
        }
        lineObjects.Clear();
    }

    // ============================
    //  HUD / 플레이어 제어
    // ============================

    /// <summary>
    /// 전투 HUD 표시/숨김.
    /// </summary>
    private void SetHUDVisible(bool visible)
    {
        if (!visible)
        {
            if (gameUI == null)
                gameUI = GameObject.Find("GameUI");
            if (combatCanvas == null)
                combatCanvas = GameObject.Find("Combat_Canvas");
        }

        if (gameUI != null) gameUI.SetActive(visible);
        if (combatCanvas != null) combatCanvas.SetActive(visible);
    }

    /// <summary>
    /// 플레이어 조작 잠금/해제.
    /// </summary>
    private void LockPlayer(bool locked)
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            player.isSkillCasting = locked;
    }

    /// <summary>
    /// 챕터/층 텍스트 갱신.
    /// </summary>
    private void UpdateChapterText()
    {
        if (chapterText == null) return;

        if (StageManager.Instance != null)
        {
            int floor = StageManager.Instance.GetCurrentFloor();
            int chapter = (floor - 1) / 10 + 1;
            chapterText.text = $"챕터 {chapter}";
        }
    }
}
