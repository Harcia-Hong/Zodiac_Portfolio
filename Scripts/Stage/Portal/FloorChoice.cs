using UnityEngine;

/// <summary>
/// 포탈 1개가 담고 있는 "다음 층 정보"
/// - 층 클리어 시 PortalSpawner가 생성하여 FloorPortal에 전달
/// - 포탈 타입, 색상, 재앙 정보를 포함하는 런타임 데이터
/// </summary>
[System.Serializable]
public class FloorChoice
{
    [Header("층 기본 정보")]
    [Tooltip("이 포탈이 연결하는 층 타입")]
    public StageType stageType;

    [Tooltip("해당 층에서 사용할 StageData (null이면 기본 데이터 사용)")]
    public StageData stageData;

    [Header("포탈 표시 정보")]
    [Tooltip("포탈 색상")]
    public Color portalColor;

    [Tooltip("포탈 위에 표시할 타입 이름")]
    public string displayName;

    [Header("재앙 정보 (전투 전용, null이면 재앙 없음)")]
    public CalamityDataSO calamityData;

    // ============================================================
    // 팩토리 메서드 — 타입별 FloorChoice를 간편하게 생성
    // ============================================================

    /// <summary>
    /// 전투 포탈 생성
    /// </summary>
    public static FloorChoice CreateCombat(StageData data, CalamityDataSO calamity = null)
    {
        bool hasCalamity = calamity != null;

        return new FloorChoice
        {
            stageType = StageType.Combat,
            stageData = data,
            portalColor = hasCalamity
                ? new Color(0.8f, 0.1f, 0.1f) // 짙은 빨강 (재앙 포함)
                : new Color(1f, 0.3f, 0.3f),   // 일반 빨강
            displayName = "전투",
            calamityData = calamity
        };
    }

    /// <summary>
    /// 상점 포탈 생성
    /// </summary>
    public static FloorChoice CreateShop(StageData data = null)
    {
        return new FloorChoice
        {
            stageType = StageType.Shop,
            stageData = data,
            portalColor = new Color(1f, 0.85f, 0.2f), // 금색
            displayName = "상점",
            calamityData = null
        };
    }

    /// <summary>
    /// 휴식 포탈 생성
    /// </summary>
    public static FloorChoice CreateRest(StageData data = null)
    {
        return new FloorChoice
        {
            stageType = StageType.Rest,
            stageData = data,
            portalColor = new Color(0.3f, 0.9f, 0.4f), // 초록
            displayName = "휴식",
            calamityData = null
        };
    }

    /// <summary>
    /// 이벤트 포탈 생성
    /// </summary>
    public static FloorChoice CreateEvent(StageData data = null)
    {
        return new FloorChoice
        {
            stageType = StageType.Event,
            stageData = data,
            portalColor = new Color(0.6f, 0.3f, 0.9f), // 보라
            displayName = "???",
            calamityData = null
        };
    }

    /// <summary>
    /// 보스 포탈 생성
    /// </summary>
    public static FloorChoice CreateBoss(StageData data)
    {
        return new FloorChoice
        {
            stageType = StageType.Boss,
            stageData = data,
            portalColor = new Color(0.15f, 0.0f, 0.0f), // 검붉은색
            displayName = "보스",
            calamityData = null
        };
    }
}
