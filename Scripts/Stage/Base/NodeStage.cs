using System.Collections;
using UnityEngine;

/// ============================================================
/// NodeStage.cs — Stage 1-2 컨트롤러 (StageBase 상속)
/// ============================================================
/// 4개 노드 활성화 → 환경 해저드 점진 등장 → 포탈 도달 시 클리어
///
/// [작동 흐름]
///   1. Stage 진입 → 적 X (정비 시간)
///   2. 첫 노드 영역 진입 → StartMission() → enemySpawner.StartSpawning()
///   3. 노드 1/2/3개 완료 → 단계별 해저드 ON (인스펙터 배열 순서대로)
///   4. 노드 4개 완료
///        - 새 적 스폰 멈춤 (C 옵션: 잔여 적 + 해저드 유지)
///        - 클리어 보상 스폰 (RewardObjectSpawner)
///        - 최종 포탈 등장 (맵 중앙 또는 portalSpawnPoint 위치)
///   5. 플레이어 포탈 도달 → NodeFinalPortal이 OnPortalReached() 호출
///        - SetProgress(100f) → StageBase 자동 흐름 → 페이드 → Stage 1-3
///
/// [StageBase 진행도와의 분리]
///   - 노드 활성화 카운트는 자체 변수(completedNodeCount)로 관리
///   - StageBase의 currentProgress는 포탈 도달 시점에만 100% 설정
///     (4개 채워도 즉시 페이드 X — 포탈 도달까지 디펜스 유지하는 C 옵션)
///
/// [환경 해저드 유연성]
///   hazards 배열에 넣은 순서대로 1/2/3개 활성화 시점에 ON.
///   배열에 안 넣은 단계는 해저드 없음 (강제 ON 아님, 자유 구성).
///
/// [의존성]
///   - StageBase: 미션 시작/클리어 + 다음 씬 페이드 전환
///   - NodeBase[]: 노드 완료 이벤트 구독
///   - StageEnemySpawner: 1-1과 동일한 단순 스포너 재사용
///   - GlobalTimer: 1-1에서 흐르던 시간 그대로 이어서 진행 (적 자동 강화)
///   - RewardObjectSpawner (싱글톤): 클리어 보상 스폰
///   - NodeFinalPortal (별도 스크립트): 포탈 진입 감지
/// ============================================================
public class NodeStage : StageBase
{
    // ============================================================
    // 인스펙터 설정
    // ============================================================

    [Header("Stage 1-2 — 노드 시스템")]
    [SerializeField, Tooltip("관리할 노드들 (씬에 배치한 KillNode/CastingNode 4개 할당)")]
    private NodeBase[] nodes;

    [Header("적 스포너")]
    [SerializeField, Tooltip("1-1과 동일한 단순 스포너 (씬에 배치된 StageEnemySpawner)")]
    private StageEnemySpawner enemySpawner;

    [Header("환경 해저드 — 노드 활성화 수에 따라 순서대로 ON")]
    [SerializeField, Tooltip("배열 순서대로 1/2/3개 활성화 시점에 등장.\n" +
                             "안 넣은 단계는 해저드 없음 — 인스펙터에서 자유 구성.\n" +
                             "예: [회전, 라인, 외곽] / [라인만] / [] (테스트용 빈 배열)")]
    private GameObject[] hazards;

    [Header("최종 포탈")]
    [SerializeField, Tooltip("4개 노드 완료 시 활성화될 포탈 GameObject (씬에 비활성 상태로 배치)")]
    private GameObject finalPortal;

    [SerializeField, Tooltip("포탈 등장 위치 (null이면 finalPortal의 현재 Transform 위치 사용)")]
    private Transform portalSpawnPoint;

    [Header("타이밍")]
    [SerializeField, Tooltip("4개 완료 → 보상 스폰까지 대기 시간")]
    private float clearToRewardDelay = 1.5f;

    [SerializeField, Tooltip("보상 스폰 → 포탈 등장까지 대기 시간")]
    private float rewardToPortalDelay = 0.5f;

    // ============================================================
    // 런타임 상태
    // ============================================================

    /// <summary>활성화 완료된 노드 수 (0 ~ nodes.Length)</summary>
    private int completedNodeCount = 0;

    /// <summary>플레이어가 어느 노드든 처음 진입했는지 (미션 자동 시작용)</summary>
    private bool playerEnteredFirstNode = false;

    /// <summary>최종 포탈이 활성화되었는지</summary>
    private bool isPortalSpawned = false;

    // ============================================================
    // 공개 프로퍼티 (UI / 디버깅용)
    // ============================================================

    public int CompletedNodeCount => completedNodeCount;
    public int TotalNodeCount => nodes != null ? nodes.Length : 0;
    public bool IsPortalSpawned => isPortalSpawned;

    // ============================================================
    // Stage 진입 — 초기화
    // ============================================================

    /// <summary>
    /// Stage 1-2 진입 시 자동 호출 (StageBase.Start 내부에서).
    /// - GlobalTimer는 1-1에서 시작된 상태 그대로 이어서 진행
    /// - 노드 완료 이벤트 구독
    /// - 해저드/포탈 비활성 초기화
    /// </summary>
    protected override void OnStageStart()
    {
        // 1-1에서 시작된 글로벌 타이머 그대로 이어서 (이미 작동 중이면 호출해도 안전)
        GlobalTimer.Instance?.StartTimer();

        InitializeNodes();
        DeactivateAllHazards();
        DeactivateFinalPortal();

        Debug.Log($"[NodeStage] Stage 1-2 진입 — 노드 {TotalNodeCount}개 대기");
    }

    /// <summary>인스펙터에 할당된 노드들의 OnNodeCompleted 이벤트 구독</summary>
    private void InitializeNodes()
    {
        if (nodes == null || nodes.Length == 0)
        {
            Debug.LogError("[NodeStage] 노드가 할당되지 않았습니다. Inspector에서 NodeBase 4개를 연결하세요.");
            return;
        }

        foreach (var node in nodes)
        {
            if (node == null) continue;
            node.OnNodeCompleted += HandleNodeCompleted;
        }
    }

    /// <summary>모든 환경 해저드 비활성화 (Stage 시작 시점)</summary>
    private void DeactivateAllHazards()
    {
        if (hazards == null) return;

        foreach (var hazard in hazards)
        {
            if (hazard != null)
                hazard.SetActive(false);
        }
    }

    /// <summary>최종 포탈 비활성화 (Stage 시작 시점)</summary>
    private void DeactivateFinalPortal()
    {
        if (finalPortal != null)
            finalPortal.SetActive(false);
    }

    // ============================================================
    // 첫 노드 영역 진입 감지 — 미션 자동 시작
    // ============================================================

    /// <summary>
    /// 매 프레임 폴링: 어느 노드든 플레이어 영역 진입 시 미션 시작.
    /// 한 번 시작되면 더 이상 폴링하지 않음 (조기 리턴).
    /// </summary>
    private void Update()
    {
        if (isMissionStarted) return;
        if (playerEnteredFirstNode) return;
        if (nodes == null) return;

        foreach (var node in nodes)
        {
            if (node == null) continue;
            if (!node.IsPlayerInRange) continue;

            playerEnteredFirstNode = true;
            StartMission();
            break;
        }
    }

    // ============================================================
    // 미션 시작 — 적 스폰 시작
    // ============================================================

    /// <summary>
    /// 미션 시작 (첫 노드 영역 진입 시 자동 호출).
    /// base: isMissionStarted = true / 자식: enemySpawner.StartSpawning()
    /// </summary>
    public override void StartMission()
    {
        base.StartMission();

        if (enemySpawner != null)
        {
            enemySpawner.StartSpawning();
        }
        else
        {
            Debug.LogError("[NodeStage] StageEnemySpawner가 할당되지 않았습니다.");
        }
    }

    // ============================================================
    // 노드 완료 콜백 — 단계별 해저드 ON
    // ============================================================

    /// <summary>
    /// 노드의 OnNodeCompleted 이벤트 콜백.
    /// - 완료 카운트 +1
    /// - 단계별 해저드 ON (배열 범위 내일 때만)
    /// - 4개 다 채워지면 클리어 처리로 분기
    /// </summary>
    private void HandleNodeCompleted(NodeBase node)
    {
        completedNodeCount++;

        Debug.Log($"[NodeStage] 노드 완료: '{node.NodeName}' " +
                  $"({completedNodeCount}/{TotalNodeCount})");

        // 단계별 해저드 활성화 (1개 완료 → index 0)
        TryActivateHazard(completedNodeCount - 1);

        // 4개 다 완료 → 보상 + 포탈 단계
        if (completedNodeCount >= TotalNodeCount)
        {
            HandleAllNodesCompleted();
        }
    }

    /// <summary>
    /// 해저드 배열 인덱스가 범위 내일 때만 활성화.
    /// 배열에 안 넣은 단계는 그냥 패스 (강제 ON 아님 — 인스펙터 자유 구성).
    /// </summary>
    private void TryActivateHazard(int index)
    {
        if (hazards == null) return;
        if (index < 0 || index >= hazards.Length) return;

        GameObject hazard = hazards[index];
        if (hazard == null) return;

        hazard.SetActive(true);
        Debug.Log($"[NodeStage] 해저드 활성화 (단계 {index + 1}): {hazard.name}");
    }

    // ============================================================
    // 4개 노드 완료 — 보상 + 포탈 등장 (적/해저드 유지)
    // ============================================================

    /// <summary>
    /// 모든 노드 완료 처리.
    /// C 옵션: 새 적 스폰만 멈춤. 잔여 적 + 해저드 유지.
    /// </summary>
    private void HandleAllNodesCompleted()
    {
        Debug.Log("[NodeStage] ===== 모든 노드 활성화 완료! =====");

        // 새 적 스폰만 멈춤 (잔여 적은 유지 — 보상 받는 동안 디펜스 계속)
        if (enemySpawner != null)
            enemySpawner.StopSpawning();

        StartCoroutine(SpawnRewardsAndPortalRoutine());
    }

    /// <summary>
    /// 보상 스폰 → 짧은 대기 → 포탈 등장 순차 처리.
    /// 단계별 딜레이로 연출 텀 확보.
    /// </summary>
    private IEnumerator SpawnRewardsAndPortalRoutine()
    {
        // 1단계: 클리어 직후 짧은 정적 (긴장감 환기)
        yield return new WaitForSeconds(clearToRewardDelay);

        // 2단계: 클리어 보상 오브젝트 스폰 (스킬, 별가루, 잉크병 등)
        Vector3 spawnCenter = GetPortalSpawnPosition();
        RewardObjectSpawner.Instance?.SpawnClearRewards(spawnCenter);

        // 3단계: 보상 등장 텀
        yield return new WaitForSeconds(rewardToPortalDelay);

        // 4단계: 최종 포탈 등장
        SpawnFinalPortal();
    }

    /// <summary>
    /// 포탈/보상 등장 위치 결정.
    /// portalSpawnPoint가 있으면 그 위치, 없으면 finalPortal의 현재 Transform 위치.
    /// </summary>
    private Vector3 GetPortalSpawnPosition()
    {
        if (portalSpawnPoint != null)
            return portalSpawnPoint.position;

        if (finalPortal != null)
            return finalPortal.transform.position;

        return Vector3.zero;
    }

    /// <summary>최종 포탈 GameObject 활성화 (위치 이동 포함)</summary>
    private void SpawnFinalPortal()
    {
        if (finalPortal == null)
        {
            Debug.LogError("[NodeStage] finalPortal이 할당되지 않았습니다.");
            return;
        }

        // portalSpawnPoint가 있으면 그 위치로 이동
        if (portalSpawnPoint != null)
            finalPortal.transform.position = portalSpawnPoint.position;

        finalPortal.SetActive(true);
        isPortalSpawned = true;

        Debug.Log("[NodeStage] 최종 포탈 등장 — 플레이어 도달 대기");
    }

    // ============================================================
    // 포탈 진입 — NodeFinalPortal이 호출
    // ============================================================

    /// <summary>
    /// 플레이어가 최종 포탈에 진입했을 때 외부(NodeFinalPortal)가 호출.
    /// SetProgress(100f) → StageBase가 자동으로 OnStageCleared 호출 → 페이드 → Stage 1-3.
    /// </summary>
    public void OnPortalReached()
    {
        if (!isPortalSpawned)
        {
            Debug.LogWarning("[NodeStage] 포탈이 아직 등장하지 않았는데 OnPortalReached 호출됨");
            return;
        }

        if (isCleared) return;

        Debug.Log("[NodeStage] 플레이어 포탈 진입 → Stage 1-3로 전환");

        // 게이지 100% 채움 → StageBase의 자동 클리어 흐름 트리거
        SetProgress(100f);
    }

    // ============================================================
    // 클리어 처리 — 적/해저드 정리 후 페이드
    // ============================================================

    /// <summary>
    /// 100% 도달 시 자동 호출 (StageBase 흐름).
    /// 잔여 적 제거 + 해저드 OFF → base 호출 → NextStageRoutine → 페이드 → Stage 1-3.
    /// </summary>
    protected override void OnStageCleared()
    {
        // 적 스폰 정지 + 잔여 적 제거 (포탈 진입 후이므로 깔끔하게 정리)
        if (enemySpawner != null)
        {
            enemySpawner.StopSpawning();
            enemySpawner.ClearAllEnemies();
        }

        // 모든 환경 해저드 OFF
        DeactivateAllHazards();

        // base: 페이드 컷씬 + Stage 1-3 전환 (NextStageRoutine)
        base.OnStageCleared();
    }

    // ============================================================
    // 정리 — 이벤트 해제
    // ============================================================

    /// <summary>씬 전환/파괴 시 노드 이벤트 구독 해제 (메모리 누수 방지)</summary>
    private void OnDestroy()
    {
        if (nodes == null) return;

        foreach (var node in nodes)
        {
            if (node != null)
                node.OnNodeCompleted -= HandleNodeCompleted;
        }
    }
}
