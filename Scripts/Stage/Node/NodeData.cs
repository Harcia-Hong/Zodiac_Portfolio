using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 노드맵의 개별 노드 데이터.
/// 런 시작 시 NodeMapGenerator가 생성하며, 런타임에만 존재한다.
/// 노드의 타입, 맵 위 위치, 연결 관계, 방문 상태를 관리한다.
/// </summary>
[System.Serializable]
public class NodeData
{
    // ============================
    //  노드 식별
    // ============================

    /// <summary>노드 고유 ID (컬럼_행 형식, 예: "2_1")</summary>
    public string nodeId;

    /// <summary>노드가 속한 컬럼 (0부터 시작, 왼→오 진행 방향)</summary>
    public int column;

    /// <summary>같은 컬럼 내 행 인덱스 (0부터 시작, 위→아래)</summary>
    public int row;

    // ============================
    //  노드 타입 / 스테이지 정보
    // ============================

    /// <summary>이 노드의 스테이지 타입 (전투/상점/휴식/이벤트/보스)</summary>
    public StageType stageType;

    /// <summary>이 노드에서 사용할 StageData (전투/보스 노드용, null 가능)</summary>
    public StageData stageData;

    /// <summary>이 노드에 부여된 재앙 (null이면 재앙 없음)</summary>
    public CalamityDataSO calamityData;

    // ============================
    //  연결 정보
    // ============================

    /// <summary>이 노드에서 갈 수 있는 다음 노드 ID 목록</summary>
    public List<string> connectedNextIds = new List<string>();

    /// <summary>이 노드로 올 수 있는 이전 노드 ID 목록</summary>
    public List<string> connectedPrevIds = new List<string>();

    // ============================
    //  노드 상태
    // ============================

    /// <summary>노드 방문 상태</summary>
    public NodeState state = NodeState.Locked;

    // ============================
    //  UI 배치용 좌표
    // ============================

    /// <summary>노드맵 UI에서의 정규화 좌표 (0~1 범위, UI 배치에 사용)</summary>
    public Vector2 mapPosition;

    // ============================
    //  표시 정보
    // ============================

    /// <summary>노드 위에 표시할 이름 (예: "전투", "상점", "???")</summary>
    public string displayName;

    /// <summary>노드 색상 (타입별 구분용)</summary>
    public Color nodeColor;

    // ============================
    //  팩토리 메서드
    // ============================

    /// <summary>
    /// 전투 노드 생성
    /// </summary>
    public static NodeData CreateCombat(int column, int row, StageData data, CalamityDataSO calamity = null)
    {
        return new NodeData
        {
            nodeId = $"{column}_{row}",
            column = column,
            row = row,
            stageType = StageType.Combat,
            stageData = data,
            calamityData = calamity,
            displayName = "전투",
            nodeColor = calamity != null
                ? new Color(0.8f, 0.1f, 0.1f)
                : new Color(1f, 0.3f, 0.3f)
        };
    }

    /// <summary>
    /// 상점 노드 생성
    /// </summary>
    public static NodeData CreateShop(int column, int row)
    {
        return new NodeData
        {
            nodeId = $"{column}_{row}",
            column = column,
            row = row,
            stageType = StageType.Shop,
            displayName = "상점",
            nodeColor = new Color(1f, 0.85f, 0.2f)
        };
    }

    /// <summary>
    /// 휴식 노드 생성
    /// </summary>
    public static NodeData CreateRest(int column, int row)
    {
        return new NodeData
        {
            nodeId = $"{column}_{row}",
            column = column,
            row = row,
            stageType = StageType.Rest,
            displayName = "휴식",
            nodeColor = new Color(0.3f, 0.9f, 0.4f)
        };
    }

    /// <summary>
    /// 이벤트 노드 생성
    /// </summary>
    public static NodeData CreateEvent(int column, int row)
    {
        return new NodeData
        {
            nodeId = $"{column}_{row}",
            column = column,
            row = row,
            stageType = StageType.Event,
            displayName = "???",
            nodeColor = new Color(0.6f, 0.3f, 0.9f)
        };
    }

    /// <summary>
    /// 보스 노드 생성
    /// </summary>
    public static NodeData CreateBoss(int column, int row, StageData data)
    {
        return new NodeData
        {
            nodeId = $"{column}_{row}",
            column = column,
            row = row,
            stageType = StageType.Boss,
            stageData = data,
            displayName = "보스",
            nodeColor = new Color(0.15f, 0.0f, 0.0f)
        };
    }

    // ============================
    //  유틸리티
    // ============================

    /// <summary>
    /// 기존 FloorChoice로 변환 (StageManager 호환용)
    /// </summary>
    public FloorChoice ToFloorChoice()
    {
        return new FloorChoice
        {
            stageType = this.stageType,
            stageData = this.stageData,
            portalColor = this.nodeColor,
            displayName = this.displayName,
            calamityData = this.calamityData
        };
    }
}

/// <summary>
/// 노드 방문 상태
/// </summary>
public enum NodeState
{
    Locked,     // 아직 갈 수 없음 (연결되지 않은 경로)
    Available,  // 선택 가능 (현재 노드에서 연결된 다음 노드)
    Current,    // 현재 플레이어가 위치한 노드
    Completed,  // 이미 방문 완료한 노드
    Skipped     // 선택하지 않아 지나친 노드
}
