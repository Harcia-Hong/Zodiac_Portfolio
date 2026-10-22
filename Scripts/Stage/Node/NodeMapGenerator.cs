using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 노드맵 생성기 (싱글톤).
/// 런 시작 시 전체 노드맵을 랜덤으로 생성하고 관리한다.
/// 컬럼 기반 좌→우 구조로, Moonlighter 2 스타일의 분기형 맵을 만든다.
/// </summary>
public class NodeMapGenerator : MonoBehaviour
{
    public static NodeMapGenerator Instance { get; private set; }

    // ============================
    //  맵 구조 설정
    // ============================

    [Header("맵 구조")]
    [SerializeField, Tooltip("챕터당 컬럼 수 (시작/보스 제외한 중간 층 수)")]
    private int columnsPerChapter = 8;

    [SerializeField, Tooltip("컬럼당 최소 노드 수")]
    private int minNodesPerColumn = 1;

    [SerializeField, Tooltip("컬럼당 최대 노드 수")]
    private int maxNodesPerColumn = 3;

    // ============================
    //  노드 타입 확률 설정
    // ============================

    [Header("비전투 노드 출현 확률 (%)")]
    [SerializeField, Range(0f, 100f)] private float shopChance = 25f;
    [SerializeField, Range(0f, 100f)] private float restChance = 20f;
    [SerializeField, Range(0f, 100f)] private float eventChance = 15f;

    [Header("비전투 노드 제한")]
    [SerializeField, Tooltip("챕터당 상점 최대 등장 횟수")]
    private int maxShopCount = 2;
    [SerializeField, Tooltip("챕터당 휴식 최대 등장 횟수")]
    private int maxRestCount = 1;
    [SerializeField, Tooltip("같은 타입 노드 간 최소 컬럼 간격")]
    private int minTypeGap = 2;

    // ============================
    //  스테이지 데이터
    // ============================

    [Header("스테이지 데이터")]
    [SerializeField, Tooltip("전투 스테이지 데이터 (랜덤 배정용)")]
    private StageData[] combatStageDatas;

    [SerializeField, Tooltip("보스 스테이지 데이터 (챕터별)")]
    private StageData[] bossStageDatas;

    [Header("재앙 설정")]
    [SerializeField, Tooltip("사용 가능한 재앙 목록")]
    private CalamityDataSO[] availableCalamities;
    [SerializeField, Range(0f, 100f)] private float baseCalamityChance = 20f;

    // ============================
    //  런타임 데이터
    // ============================

    /// <summary>전체 노드 데이터 (ID → NodeData)</summary>
    private Dictionary<string, NodeData> allNodes = new Dictionary<string, NodeData>();

    /// <summary>컬럼별 노드 목록 (컬럼 인덱스 → 노드 리스트)</summary>
    private List<List<NodeData>> columnNodes = new List<List<NodeData>>();

    /// <summary>현재 플레이어가 위치한 노드 ID</summary>
    private string currentNodeId;

    /// <summary>총 컬럼 수 (시작 + 중간 + 보스)</summary>
    private int totalColumns;

    // 비전투 노드 출현 추적
    private int shopCount = 0;
    private int restCount = 0;
    private int lastShopColumn = -10;
    private int lastRestColumn = -10;
    private int lastEventColumn = -10;

    // ============================
    //  공개 프로퍼티
    // ============================

    /// <summary>전체 노드 데이터 읽기 전용 접근</summary>
    public Dictionary<string, NodeData> AllNodes => allNodes;

    /// <summary>컬럼별 노드 목록 읽기 전용 접근</summary>
    public List<List<NodeData>> ColumnNodes => columnNodes;

    /// <summary>현재 노드 ID</summary>
    public string CurrentNodeId => currentNodeId;

    /// <summary>총 컬럼 수</summary>
    public int TotalColumns => totalColumns;

    // ============================
    //  초기화
    // ============================

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    // ============================
    //  맵 생성 — 외부에서 호출
    // ============================

    /// <summary>
    /// 노드맵 전체를 새로 생성한다. 런 시작 시 호출.
    /// </summary>
    /// <param name="chapter">현재 챕터 번호 (1부터)</param>
    public void GenerateMap(int chapter = 1)
    {
        ClearMap();
        ResetTracking();

        // 총 컬럼: 시작(0) + 중간(1~N) + 보스(N+1)
        totalColumns = columnsPerChapter + 2;

        // 1단계: 각 컬럼별 노드 생성
        CreateAllNodes(chapter);

        // 2단계: 노드 간 연결선 생성
        ConnectNodes();

        // 3단계: 시작 노드를 현재 위치로 설정
        SetStartNode();

        // 4단계: UI 배치용 좌표 계산
        CalculateMapPositions();

        Debug.Log($"[NodeMapGenerator] 맵 생성 완료 — {totalColumns}컬럼, 총 {allNodes.Count}개 노드");
    }

    // ============================
    //  1단계: 노드 생성
    // ============================

    /// <summary>
    /// 모든 컬럼의 노드를 생성한다.
    /// </summary>
    private void CreateAllNodes(int chapter)
    {
        for (int col = 0; col < totalColumns; col++)
        {
            List<NodeData> nodesInColumn = new List<NodeData>();

            if (col == 0)
            {
                // 시작 노드 (항상 1개, 전투)
                NodeData startNode = CreateNodeForColumn(col, 0, chapter, true);
                nodesInColumn.Add(startNode);
            }
            else if (col == totalColumns - 1)
            {
                // 보스 노드 (항상 1개)
                StageData bossData = GetBossStageData(chapter);
                NodeData bossNode = NodeData.CreateBoss(col, 0, bossData);
                bossNode.mapPosition = Vector2.zero; // 나중에 계산
                nodesInColumn.Add(bossNode);
                RegisterNode(bossNode);
            }
            else
            {
                // 중간 컬럼: 1~3개 노드
                int nodeCount = Random.Range(minNodesPerColumn, maxNodesPerColumn + 1);

                for (int row = 0; row < nodeCount; row++)
                {
                    NodeData node = CreateNodeForColumn(col, row, chapter, false);
                    nodesInColumn.Add(node);
                }
            }

            columnNodes.Add(nodesInColumn);
        }
    }

    /// <summary>
    /// 한 컬럼의 노드 하나를 타입 확률에 따라 생성한다.
    /// </summary>
    private NodeData CreateNodeForColumn(int col, int row, int chapter, bool forceCombat)
    {
        NodeData node;

        if (forceCombat)
        {
            node = CreateCombatNode(col, row, chapter);
        }
        else
        {
            // 비전투 노드 확률 굴림
            StageType type = RollNodeType(col);

            switch (type)
            {
                case StageType.Shop:
                    node = NodeData.CreateShop(col, row);
                    shopCount++;
                    lastShopColumn = col;
                    break;

                case StageType.Rest:
                    node = NodeData.CreateRest(col, row);
                    restCount++;
                    lastRestColumn = col;
                    break;

                case StageType.Event:
                    node = NodeData.CreateEvent(col, row);
                    lastEventColumn = col;
                    break;

                default:
                    node = CreateCombatNode(col, row, chapter);
                    break;
            }
        }

        RegisterNode(node);
        return node;
    }

    /// <summary>
    /// 전투 노드를 생성한다 (재앙 확률 포함).
    /// </summary>
    private NodeData CreateCombatNode(int col, int row, int chapter)
    {
        StageData data = GetRandomCombatData();
        CalamityDataSO calamity = RollCalamity(chapter);
        return NodeData.CreateCombat(col, row, data, calamity);
    }

    /// <summary>
    /// 노드 타입 확률 굴림. 조건 불충족 시 전투로 폴백.
    /// </summary>
    private StageType RollNodeType(int col)
    {
        // 상점 굴림
        if (CanSpawnType(StageType.Shop, col))
        {
            if (Random.Range(0f, 100f) < shopChance)
                return StageType.Shop;
        }

        // 휴식 굴림
        if (CanSpawnType(StageType.Rest, col))
        {
            if (Random.Range(0f, 100f) < restChance)
                return StageType.Rest;
        }

        // 이벤트 굴림
        if (CanSpawnType(StageType.Event, col))
        {
            if (Random.Range(0f, 100f) < eventChance)
                return StageType.Event;
        }

        return StageType.Combat;
    }

    /// <summary>
    /// 해당 타입의 노드가 이 컬럼에 생성 가능한지 확인.
    /// </summary>
    private bool CanSpawnType(StageType type, int col)
    {
        switch (type)
        {
            case StageType.Shop:
                if (shopCount >= maxShopCount) return false;
                if (col - lastShopColumn < minTypeGap) return false;
                return true;

            case StageType.Rest:
                if (restCount >= maxRestCount) return false;
                if (col - lastRestColumn < minTypeGap) return false;
                return true;

            case StageType.Event:
                if (col - lastEventColumn < minTypeGap) return false;
                return true;

            default:
                return true;
        }
    }

    // ============================
    //  2단계: 노드 연결
    // ============================

    /// <summary>
    /// 인접 컬럼 간 노드를 연결한다.
    /// 모든 노드가 최소 1개의 연결을 갖도록 보장한다.
    /// </summary>
    private void ConnectNodes()
    {
        for (int col = 0; col < columnNodes.Count - 1; col++)
        {
            List<NodeData> currentCol = columnNodes[col];
            List<NodeData> nextCol = columnNodes[col + 1];

            // 모든 현재 노드가 최소 1개의 다음 노드와 연결되도록
            foreach (var node in currentCol)
            {
                // 랜덤으로 1~2개의 다음 노드 선택
                int connectionCount = Mathf.Min(Random.Range(1, 3), nextCol.Count);
                List<NodeData> shuffled = new List<NodeData>(nextCol);
                ShuffleList(shuffled);

                for (int i = 0; i < connectionCount; i++)
                {
                    LinkNodes(node, shuffled[i]);
                }
            }

            // 다음 컬럼에서 연결이 없는 노드에 보장 연결 추가
            foreach (var nextNode in nextCol)
            {
                if (nextNode.connectedPrevIds.Count == 0)
                {
                    // 현재 컬럼에서 랜덤으로 하나 연결
                    NodeData randomPrev = currentCol[Random.Range(0, currentCol.Count)];
                    LinkNodes(randomPrev, nextNode);
                }
            }
        }
    }

    /// <summary>
    /// 두 노드를 양방향으로 연결한다 (중복 방지).
    /// </summary>
    private void LinkNodes(NodeData from, NodeData to)
    {
        if (!from.connectedNextIds.Contains(to.nodeId))
            from.connectedNextIds.Add(to.nodeId);

        if (!to.connectedPrevIds.Contains(from.nodeId))
            to.connectedPrevIds.Add(from.nodeId);
    }

    // ============================
    //  3단계: 시작 노드 설정
    // ============================

    /// <summary>
    /// 시작 노드를 현재 위치로 설정하고, 연결된 다음 노드들을 Available로 변경.
    /// </summary>
    private void SetStartNode()
    {
        if (columnNodes.Count == 0 || columnNodes[0].Count == 0) return;

        NodeData startNode = columnNodes[0][0];
        currentNodeId = startNode.nodeId;
        startNode.state = NodeState.Current;

        // 시작 노드에서 갈 수 있는 다음 노드들을 Available로
        UpdateAvailableNodes();
    }

    // ============================
    //  4단계: UI 좌표 계산
    // ============================

    /// <summary>
    /// 각 노드의 맵 위 정규화 좌표(0~1)를 계산한다.
    /// NodeMapUI에서 이 좌표를 기반으로 실제 UI 위치를 결정한다.
    /// </summary>
    private void CalculateMapPositions()
    {
        for (int col = 0; col < columnNodes.Count; col++)
        {
            float xNormalized = (float)col / (totalColumns - 1);
            List<NodeData> nodesInCol = columnNodes[col];

            for (int row = 0; row < nodesInCol.Count; row++)
            {
                // Y 좌표: 노드 수에 따라 균등 분배 (중앙 정렬)
                float yNormalized;

                if (nodesInCol.Count == 1)
                {
                    yNormalized = 0.5f;
                }
                else
                {
                    float spacing = 1f / (nodesInCol.Count + 1);
                    yNormalized = spacing * (row + 1);
                }

                nodesInCol[row].mapPosition = new Vector2(xNormalized, yNormalized);
            }
        }
    }

    // ============================
    //  노드 진행 — 스테이지 클리어 후 호출
    // ============================

    /// <summary>
    /// 플레이어가 노드를 선택했을 때 호출.
    /// 현재 노드를 이동하고 상태를 갱신한다.
    /// </summary>
    /// <param name="nodeId">선택한 노드 ID</param>
    /// <returns>선택한 노드의 FloorChoice (StageManager 전달용)</returns>
    public FloorChoice SelectNode(string nodeId)
    {
        if (!allNodes.ContainsKey(nodeId))
        {
            Debug.LogError($"[NodeMapGenerator] 존재하지 않는 노드: {nodeId}");
            return null;
        }

        NodeData selectedNode = allNodes[nodeId];

        // 선택 가능한 노드인지 확인
        if (selectedNode.state != NodeState.Available)
        {
            Debug.LogWarning($"[NodeMapGenerator] 선택할 수 없는 노드: {nodeId} (상태: {selectedNode.state})");
            return null;
        }

        // 현재 노드 완료 처리
        if (currentNodeId != null && allNodes.ContainsKey(currentNodeId))
        {
            allNodes[currentNodeId].state = NodeState.Completed;
        }

        // 선택하지 않은 Available 노드를 Skipped로
        MarkSkippedNodes();

        // 새 노드로 이동
        currentNodeId = nodeId;
        selectedNode.state = NodeState.Current;

        // 다음 노드들을 Available로 갱신
        UpdateAvailableNodes();

        Debug.Log($"[NodeMapGenerator] 노드 선택: {nodeId} ({selectedNode.displayName})");

        return selectedNode.ToFloorChoice();
    }

    /// <summary>
    /// 현재 노드 클리어 완료 처리. 스테이지 클리어 후 호출.
    /// </summary>
    public void CompleteCurrentNode()
    {
        if (currentNodeId != null && allNodes.ContainsKey(currentNodeId))
        {
            allNodes[currentNodeId].state = NodeState.Completed;
            UpdateAvailableNodes();

            Debug.Log($"[NodeMapGenerator] 노드 클리어: {currentNodeId}");
        }
    }

    /// <summary>
    /// 현재 노드의 데이터를 반환한다.
    /// </summary>
    public NodeData GetCurrentNode()
    {
        if (currentNodeId != null && allNodes.ContainsKey(currentNodeId))
            return allNodes[currentNodeId];
        return null;
    }

    // ============================
    //  상태 갱신 헬퍼
    // ============================

    /// <summary>
    /// 현재 노드에서 연결된 다음 노드들을 Available 상태로 변경.
    /// </summary>
    private void UpdateAvailableNodes()
    {
        if (currentNodeId == null || !allNodes.ContainsKey(currentNodeId)) return;

        NodeData current = allNodes[currentNodeId];

        foreach (string nextId in current.connectedNextIds)
        {
            if (allNodes.ContainsKey(nextId))
            {
                NodeData nextNode = allNodes[nextId];
                // Locked 상태인 노드만 Available로 변경 (이미 Completed/Skipped인 건 건드리지 않음)
                if (nextNode.state == NodeState.Locked)
                    nextNode.state = NodeState.Available;
            }
        }
    }

    /// <summary>
    /// 현재 Available 상태인 노드 중 선택되지 않은 것을 Skipped로 변경.
    /// </summary>
    private void MarkSkippedNodes()
    {
        foreach (var node in allNodes.Values)
        {
            if (node.state == NodeState.Available)
                node.state = NodeState.Skipped;
        }
    }

    // ============================
    //  유틸리티
    // ============================

    /// <summary>노드를 전체 딕셔너리에 등록</summary>
    private void RegisterNode(NodeData node)
    {
        if (!allNodes.ContainsKey(node.nodeId))
            allNodes.Add(node.nodeId, node);
    }

    /// <summary>맵 데이터 전체 초기화</summary>
    private void ClearMap()
    {
        allNodes.Clear();
        columnNodes.Clear();
        currentNodeId = null;
    }

    /// <summary>출현 추적 변수 초기화</summary>
    private void ResetTracking()
    {
        shopCount = 0;
        restCount = 0;
        lastShopColumn = -10;
        lastRestColumn = -10;
        lastEventColumn = -10;
    }

    /// <summary>랜덤 전투 StageData 반환</summary>
    private StageData GetRandomCombatData()
    {
        if (combatStageDatas == null || combatStageDatas.Length == 0)
            return null;
        return combatStageDatas[Random.Range(0, combatStageDatas.Length)];
    }

    /// <summary>챕터별 보스 StageData 반환</summary>
    private StageData GetBossStageData(int chapter)
    {
        int index = chapter - 1;
        if (bossStageDatas != null && index >= 0 && index < bossStageDatas.Length)
            return bossStageDatas[index];

        Debug.LogWarning($"[NodeMapGenerator] 챕터 {chapter} 보스 데이터 없음");
        return null;
    }

    /// <summary>재앙 확률 굴림</summary>
    private CalamityDataSO RollCalamity(int chapter)
    {
        if (availableCalamities == null || availableCalamities.Length == 0)
            return null;

        if (Random.Range(0f, 100f) >= baseCalamityChance)
            return null;

        List<CalamityDataSO> valid = new List<CalamityDataSO>();
        foreach (var c in availableCalamities)
        {
            if (c != null && c.minChapter <= chapter)
                valid.Add(c);
        }

        if (valid.Count == 0) return null;
        return valid[Random.Range(0, valid.Count)];
    }

    /// <summary>리스트를 랜덤으로 섞는다 (Fisher-Yates 셔플)</summary>
    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
