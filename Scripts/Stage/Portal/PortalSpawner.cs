// PortalSpawner.cs

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 층 클리어 후 포탈 생성 담당
/// - 확률 기반으로 포탈 종류/개수 결정
/// - 포탈 프리팹을 스폰하고 FloorChoice 데이터 전달
/// - StageManager.HandleStageCleared()에서 호출됨
/// </summary>
public class PortalSpawner : MonoBehaviour
{
    public static PortalSpawner Instance { get; private set; }

    [Header("포탈 프리팹")]
    [SerializeField, Tooltip("포탈 프리팹 (FloorPortal 컴포넌트 필수)")]
    private GameObject portalPrefab;

    [Header("스폰 위치")]
    [SerializeField, Tooltip("포탈이 생성될 위치들 (최대 3개)")]
    private Transform[] spawnPoints;

    [Header("보스 스테이지 데이터")]
    [SerializeField, Tooltip("챕터별 보스 StageData (인덱스 0 = 챕터1 보스, 1 = 챕터2 보스...)")]
    private StageData[] bossStageDataPerChapter;

    [Header("비전투 포탈 출현 확률 (%)")]
    [SerializeField, Range(0f, 100f)] private float shopChance = 25f;
    [SerializeField, Range(0f, 100f)] private float restChance = 20f;
    [SerializeField, Range(0f, 100f)] private float eventChance = 15f;

    [Header("비전투 포탈 제한")]
    [SerializeField, Tooltip("챕터당 상점 최대 등장 횟수")]
    private int maxShopPerChapter = 2;
    [SerializeField, Tooltip("챕터당 휴식 최대 등장 횟수")]
    private int maxRestPerChapter = 1;
    [SerializeField, Tooltip("같은 타입 포탈 간 최소 층 간격")]
    private int minFloorGap = 3;

    [Header("재앙 설정")]
    [SerializeField, Tooltip("사용 가능한 재앙 목록")]
    private CalamityDataSO[] availableCalamities;
    [SerializeField, Tooltip("챕터별 재앙 기본 확률 (%)")]
    private float baseCalamityChance = 20f;
    [SerializeField, Tooltip("챕터당 재앙 확률 증가량 (%)")]
    private float calamityChancePerChapter = 15f;

    // === 출현 횟수 추적 ===
    private int shopCountThisChapter = 0;
    private int restCountThisChapter = 0;
    private int lastShopFloor = -10;
    private int lastRestFloor = -10;
    private int lastEventFloor = -10;

    // === 현재 활성 포탈 관리 ===
    private List<GameObject> activePortals = new List<GameObject>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ============================================================
    // 포탈 생성 — StageManager에서 호출
    // ============================================================

    /// <summary>
    /// 포탈 생성 메인 함수
    /// - 현재 층/챕터 정보를 기반으로 포탈 종류와 개수를 결정
    /// </summary>
    public void SpawnPortals(int currentFloor, int currentChapter, StageData combatStageData)
    {
        // 기존 포탈 정리
        ClearActivePortals();

        // 선택지 목록 생성
        List<FloorChoice> choices = GenerateChoices(currentFloor, currentChapter, combatStageData);

        // 스폰 위치 유효성 검사
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[PortalSpawner] 스폰 포인트가 설정되지 않았습니다!");
            return;
        }

        // 포탈 생성
        for (int i = 0; i < choices.Count && i < spawnPoints.Length; i++)
        {
            SpawnSinglePortal(choices[i], spawnPoints[i]);
        }

        Debug.Log($"[PortalSpawner] {choices.Count}개 포탈 생성 완료 (챕터 {currentChapter}, {currentFloor}층)");
    }

    // ============================================================
    // 선택지 생성 — 확률 굴림 핵심 로직
    // ============================================================

    /// <summary>
    /// 현재 상황에 맞는 FloorChoice 목록 생성
    /// </summary>
    private List<FloorChoice> GenerateChoices(int floor, int chapter, StageData combatData)
    {
        List<FloorChoice> choices = new List<FloorChoice>();

        // ── 보스층 (10, 20, 30...) → 보스 포탈 1개만 ──
        if (floor % 10 == 0)
        {
            // 해당 챕터의 보스 StageData를 가져옴 (없으면 전투 데이터로 폴백)
            StageData bossData = GetBossStageData(chapter);
            choices.Add(FloorChoice.CreateBoss(bossData));
            return choices;
        }

        // ── 전투 포탈 (100% 고정) ──
        CalamityDataSO calamity = RollCalamity(chapter);
        choices.Add(FloorChoice.CreateCombat(combatData, calamity));

        // ── 비전투 포탈 확률 굴림 (최대 2개 추가) ──
        List<FloorChoice> nonCombatCandidates = new List<FloorChoice>();

        // 상점 확률 굴림
        if (CanSpawnShop(floor))
        {
            if (Random.Range(0f, 100f) < shopChance)
            {
                nonCombatCandidates.Add(FloorChoice.CreateShop());
            }
        }

        // 휴식 확률 굴림
        if (CanSpawnRest(floor))
        {
            if (Random.Range(0f, 100f) < restChance)
            {
                nonCombatCandidates.Add(FloorChoice.CreateRest());
            }
        }

        // 이벤트 확률 굴림
        if (CanSpawnEvent(floor))
        {
            if (Random.Range(0f, 100f) < eventChance)
            {
                nonCombatCandidates.Add(FloorChoice.CreateEvent());
            }
        }

        // 비전투 최대 2개까지만 추가
        int nonCombatLimit = Mathf.Min(nonCombatCandidates.Count, 2);
        for (int i = 0; i < nonCombatLimit; i++)
        {
            choices.Add(nonCombatCandidates[i]);
        }

        return choices;
    }

    // ============================================================
    // 보스 StageData 조회
    // ============================================================

    /// <summary>
    /// 해당 챕터의 보스 StageData를 반환
    /// - bossStageDataPerChapter 배열에서 챕터 인덱스(0부터)로 조회
    /// - 설정되지 않았으면 경고 로그 출력 후 null 반환
    /// </summary>
    private StageData GetBossStageData(int chapter)
    {
        // 챕터는 1부터 시작, 배열은 0부터 → (chapter - 1) 인덱스
        int index = chapter - 1;

        if (bossStageDataPerChapter != null && index >= 0 && index < bossStageDataPerChapter.Length)
        {
            StageData bossData = bossStageDataPerChapter[index];

            if (bossData != null)
            {
                Debug.Log($"[PortalSpawner] 챕터 {chapter} 보스 StageData 로드: {bossData.stageName}");
                return bossData;
            }
        }

        Debug.LogError($"[PortalSpawner] 챕터 {chapter}의 보스 StageData가 설정되지 않았습니다! Inspector에서 bossStageDataPerChapter를 설정해주세요.");
        return null;
    }

    // ============================================================
    // 출현 조건 체크
    // ============================================================

    /// <summary>
    /// 상점 포탈 출현 가능 여부
    /// </summary>
    private bool CanSpawnShop(int currentFloor)
    {
        if (shopCountThisChapter >= maxShopPerChapter) return false;
        if (currentFloor - lastShopFloor < minFloorGap) return false;
        return true;
    }

    /// <summary>
    /// 휴식 포탈 출현 가능 여부
    /// </summary>
    private bool CanSpawnRest(int currentFloor)
    {
        if (restCountThisChapter >= maxRestPerChapter) return false;
        if (currentFloor - lastRestFloor < minFloorGap) return false;
        return true;
    }

    /// <summary>
    /// 이벤트 포탈 출현 가능 여부
    /// </summary>
    private bool CanSpawnEvent(int currentFloor)
    {
        if (currentFloor - lastEventFloor < minFloorGap) return false;
        return true;
    }

    // ============================================================
    // 재앙 굴림
    // ============================================================

    /// <summary>
    /// 전투 포탈에 부착할 재앙 확률 굴림
    /// - 챕터가 올라갈수록 확률 증가
    /// - 해당 챕터에 맞는 재앙만 후보에 포함
    /// </summary>
    private CalamityDataSO RollCalamity(int chapter)
    {
        if (availableCalamities == null || availableCalamities.Length == 0)
            return null;

        // 챕터별 확률: 기본 + (챕터 - 1) * 증가량
        float chance = baseCalamityChance + (chapter - 1) * calamityChancePerChapter;

        if (Random.Range(0f, 100f) >= chance)
            return null;

        // 현재 챕터에서 사용 가능한 재앙만 필터링
        List<CalamityDataSO> validCalamities = new List<CalamityDataSO>();
        foreach (var calamity in availableCalamities)
        {
            if (calamity != null && calamity.minChapter <= chapter)
            {
                validCalamities.Add(calamity);
            }
        }

        if (validCalamities.Count == 0)
            return null;

        // 랜덤 선택
        return validCalamities[Random.Range(0, validCalamities.Count)];
    }

    // ============================================================
    // 포탈 스폰/정리
    // ============================================================

    /// <summary>
    /// 포탈 1개 생성
    /// </summary>
    private void SpawnSinglePortal(FloorChoice choice, Transform spawnPoint)
    {
        if (portalPrefab == null)
        {
            Debug.LogError("[PortalSpawner] 포탈 프리팹이 설정되지 않았습니다!");
            return;
        }

        GameObject portalObj = Instantiate(portalPrefab, spawnPoint.position, spawnPoint.rotation);
        FloorPortal portal = portalObj.GetComponent<FloorPortal>();

        if (portal != null)
        {
            portal.Initialize(choice);
        }
        else
        {
            Debug.LogError("[PortalSpawner] 포탈 프리팹에 FloorPortal 컴포넌트가 없습니다!");
        }

        activePortals.Add(portalObj);
    }

    /// <summary>
    /// 현재 활성 포탈 전부 제거
    /// </summary>
    public void ClearActivePortals()
    {
        foreach (var portal in activePortals)
        {
            if (portal != null)
                Destroy(portal);
        }

        activePortals.Clear();
    }

    // ============================================================
    // 출현 기록 갱신 — StageManager에서 호출
    // ============================================================

    /// <summary>
    /// 플레이어가 선택한 층 타입을 기록
    /// - 다음 포탈 생성 시 제한 조건에 반영
    /// </summary>
    public void RecordFloorChoice(StageType chosenType, int floor)
    {
        switch (chosenType)
        {
            case StageType.Shop:
                shopCountThisChapter++;
                lastShopFloor = floor;
                break;

            case StageType.Rest:
                restCountThisChapter++;
                lastRestFloor = floor;
                break;

            case StageType.Event:
                lastEventFloor = floor;
                break;
        }
    }

    /// <summary>
    /// 챕터 종료 시 출현 횟수 초기화
    /// </summary>
    public void OnChapterEnd()
    {
        shopCountThisChapter = 0;
        restCountThisChapter = 0;
    }
}