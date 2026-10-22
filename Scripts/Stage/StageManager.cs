using System.Collections;
using UnityEngine;

/// ============================================================
/// StageManager.cs — 아케이드 웨이브 스테이지 매니저 (누적형 + 다중 웨이브)
/// ============================================================
/// 킬 나이트 + 윈드브로운 혼합 페이즈 기반 전투 흐름.
///
/// 핵심 역할:
///   1. 페이즈 순차 진행 (페이즈 0 → 1 → 2 → ... → 보스)
///   2. 한 페이즈 안에서 여러 웨이브 순차 진행 (웨이브 사이 블록 변형)
///   3. MonsterSpawner에 웨이브 데이터 위임
///   4. 페이즈 클리어 → 다음 Layout을 "추가로" 활성화 (기존 유지, 누적)
///   5. 플레이어가 전투 지역(CombatZone)에 진입 → 페이즈 전투 시작
///   6. 마지막 페이즈 클리어 → 보스 Layout 활성 → CombatZone 진입 시 보스 소환
///   7. 보스 처치 = 클리어 / 플레이어 사망 = 실패
///
/// 플레이 흐름 (다중 웨이브 + 누적형 + CombatZone):
///   [아레나 시작]
///     Layout_0 솟아오름 → CombatZone_0 진입 → Phase 0 시작
///   [Phase 0 내부]
///     Wave 0-1 → 클리어 → 블록 변형(예정) → Wave 0-2 → 클리어 → ... → 마지막 Wave 클리어
///   [Phase 0 클리어]
///     Layout_1 솟아오름 → 대시 이동 → CombatZone_1 진입 → Phase 1 시작
///   [...]
///   [마지막 Phase 클리어]
///     보스 Layout 활성 → 대시 이동 → CombatZone 진입 → 보스 소환
///
/// 의존성:
///   - StageData (SO): 페이즈 배열 + 보스 설정
///   - MonsterSpanwer: 웨이브 단위 몹 스폰 (StartWave / OnWaveCleared)
///   - ArenaMapManager: Layout 누적 활성화
///   - CombatZoneManager: 전투 지역 진입 감지
///   - Boss.OnBossDeath: 보스 처치 이벤트
/// ============================================================
public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    // ============================================================
    // 스테이지 상태
    // ============================================================

    /// <summary>스테이지 진행 상태</summary>
    public enum StageState
    {
        Ready,              // 시작 전 대기
        Combat,             // 페이즈 전투 진행 중 (웨이브 또는 웨이브 사이)
        PhaseTransition,    // 페이즈 사이 (다음 Layout 활성 + CombatZone 진입 대기)
        BossPhase,          // 보스 전투 중
        Cleared,            // 보스 처치 → 클리어
        Failed              // 플레이어 사망 → 실패
    }

    /// <summary>페이즈 전환 중 하위 단계 (디버깅/상태 추적용)</summary>
    private enum TransitionSubState
    {
        None,
        WaitingForCombatZone,   // 다음 페이즈 CombatZone 진입 대기
        WaitingForBossZone      // 보스 CombatZone 진입 대기
    }

    // ============================================================
    // 인스펙터 설정
    // ============================================================

    [Header("스테이지 설정")]
    [SerializeField] private StageData currentStageData;

    [Header("외부 시스템 참조")]
    [SerializeField] private MonsterSpanwer monsterSpawner;
    [SerializeField] private ArenaMapManager arenaMapManager;

    [Header("페이즈 전환 타이밍")]
    [Tooltip("페이즈 클리어 후 다음 Layout 활성화까지 대기 시간 (초)")]
    [SerializeField] private float clearToActivateDelay = 1.0f;

    [Tooltip("Layout 활성화 완료 후 CombatZone 감지 시작까지 대기 시간 (초)")]
    [SerializeField] private float activateToDetectDelay = 0.3f;

    // ============================================================
    // 런타임 상태 (디버깅용)
    // ============================================================

    [Header("현재 상태 (디버깅용)")]
    [SerializeField] private StageState currentState = StageState.Ready;
    [SerializeField] private int currentPhaseIndex = -1;
    [SerializeField] private int currentWaveIndex = -1;
    [SerializeField] private int totalKillCount = 0;
    [SerializeField] private bool bossSpawned = false;

    /// <summary>현재 진행 중인 전환 하위 상태</summary>
    [SerializeField] private TransitionSubState transitionSubState = TransitionSubState.None;

    /// <summary>대기 중인 Phase Index — 이 값의 CombatZone이 트리거되면 전투 시작</summary>
    [SerializeField] private int expectedPhaseIndex = -1;

    /// <summary>웨이브 사이 전환 코루틴 참조 (중복 방지용)</summary>
    private Coroutine waveTransitionCoroutine;

    // ============================================================
    // 외부 접근용 프로퍼티
    // ============================================================

    public StageState CurrentState => currentState;
    public int CurrentPhaseIndex => currentPhaseIndex;
    public int CurrentWaveIndex => currentWaveIndex;
    public int TotalKillCount => totalKillCount;
    public int ChapterNumber => currentStageData?.chapterNumber ?? 1;
    public bool IsBossPhase => currentState == StageState.BossPhase;
    public bool IsLastPhase => currentStageData != null
        && currentPhaseIndex >= currentStageData.PhaseCount - 1;

    /// <summary>현재 페이즈에서 진행 중인 웨이브가 마지막인지</summary>
    public bool IsLastWaveInCurrentPhase
    {
        get
        {
            PhaseData phase = GetCurrentPhase();
            if (phase == null) return false;
            return currentWaveIndex >= phase.WaveCount - 1;
        }
    }

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // OnEnable 시점에 다른 매니저(특히 CombatZoneManager)가
        // 아직 Awake되지 않았을 수 있어, Start에서 한 번 더 안전하게 구독
        SubscribeToMonsterSpawner();
        SubscribeToCombatZoneManager();

        StartArena();
    }

    private void Update()
    {
        CheckPlayerDeath();
    }

    private void OnEnable()
    {
        // 적 처치 / 보스 처치 이벤트 구독
        Enemy.OnEnemyDeath += OnEnemyKilled;
        Boss.OnBossDeath += OnBossDefeated;

        SubscribeToMonsterSpawner();
        SubscribeToCombatZoneManager();
    }

    private void OnDisable()
    {
        Enemy.OnEnemyDeath -= OnEnemyKilled;
        Boss.OnBossDeath -= OnBossDefeated;

        UnsubscribeFromMonsterSpawner();
        UnsubscribeFromCombatZoneManager();
    }

    #endregion

    // ============================================================
    // 아레나 시작
    // ============================================================

    #region Arena Start

    /// <summary>아레나 전투 시작 — Layout_0 솟아오름 후 CombatZone 진입 대기</summary>
    public void StartArena()
    {
        if (currentStageData == null)
        {
            Debug.LogError("[StageManager] StageData가 설정되지 않았습니다.");
            return;
        }

        if (currentStageData.PhaseCount == 0)
        {
            Debug.LogError("[StageManager] 페이즈 데이터가 없습니다.");
            return;
        }

        // 상태 초기화
        currentPhaseIndex = -1;
        currentWaveIndex = -1;
        totalKillCount = 0;
        bossSpawned = false;
        transitionSubState = TransitionSubState.None;
        expectedPhaseIndex = -1;
        currentState = StageState.PhaseTransition;

        Debug.Log($"[StageManager] ===== 아레나 시작: {currentStageData.stageName} =====");
        Debug.Log($"[StageManager] 총 페이즈 수: {currentStageData.PhaseCount}");

        StartCoroutine(ArenaIntroRoutine());
    }

    /// <summary>
    /// 아레나 시작 연출 루틴
    /// Layout_0 솟아오름 → 완료 대기 → CombatZone_0 진입 대기
    /// (플레이어가 시작 지점에서 걸어가 Layout_0의 CombatZone에 진입해야 Phase 0 시작)
    /// </summary>
    private IEnumerator ArenaIntroRoutine()
    {
        if (arenaMapManager != null)
        {
            arenaMapManager.ActivateLayout(0);

            // Layout 활성화(솟아오름) 완료 대기
            yield return new WaitUntil(() => !arenaMapManager.IsAnyTransitioning());
        }

        yield return new WaitForSeconds(activateToDetectDelay);

        // Phase 0 CombatZone 진입 대기 상태로 진입
        expectedPhaseIndex = 0;
        transitionSubState = TransitionSubState.WaitingForCombatZone;

        Debug.Log("[StageManager] Layout_0 활성화 완료 — CombatZone_0 진입 대기 중...");
    }

    #endregion

    // ============================================================
    // 페이즈 / 웨이브 진행
    // ============================================================

    #region Phase / Wave Progression

    /// <summary>
    /// 페이즈 시작 — CombatZone 트리거 시 호출
    /// - 페이즈 첫 웨이브(인덱스 0)부터 시작
    /// - 다중 웨이브 페이즈면 OnWaveClearedByMonsterSpawner에서 자동으로 다음 웨이브 진행
    /// </summary>
    private void StartPhase(int phaseIndex)
    {
        currentPhaseIndex = phaseIndex;

        // 페이즈 범위 체크
        if (currentPhaseIndex >= currentStageData.PhaseCount)
        {
            Debug.LogWarning($"[StageManager] Phase {phaseIndex}는 존재하지 않음 — 보스 전환");
            BeginBossPhaseTransition();
            return;
        }

        PhaseData phase = currentStageData.GetPhase(currentPhaseIndex);
        if (phase == null)
        {
            Debug.LogError($"[StageManager] 페이즈 {currentPhaseIndex} 데이터가 null입니다.");
            return;
        }

        currentState = StageState.Combat;
        transitionSubState = TransitionSubState.None;
        expectedPhaseIndex = -1;

        Debug.Log($"[StageManager] ===== 페이즈 {currentPhaseIndex + 1} 시작: {phase.phaseName} " +
                  $"(웨이브 {phase.WaveCount}개) =====");

        StartWave(0);
    }

    /// <summary>
    /// 현재 페이즈의 특정 웨이브 시작
    /// - 웨이브 데이터를 추출해 MonsterSpawner.StartWave에 위임
    /// - 잘못된 인덱스면 에러 로그 후 무시
    /// </summary>
    private void StartWave(int waveIndex)
    {
        PhaseData phase = GetCurrentPhase();
        if (phase == null)
        {
            Debug.LogError("[StageManager] 현재 페이즈가 null입니다 — 웨이브 시작 불가");
            return;
        }

        WaveData wave = phase.GetWave(waveIndex);
        if (wave == null)
        {
            Debug.LogError($"[StageManager] 웨이브 {waveIndex} 데이터가 null입니다.");
            return;
        }

        currentWaveIndex = waveIndex;

        Debug.Log($"[StageManager] ----- 웨이브 {waveIndex + 1}/{phase.WaveCount} 시작: " +
                  $"{wave.waveName} (총 {wave.totalNormalCount}마리, 엘리트 {wave.eliteCount}) -----");

        if (monsterSpawner != null)
            monsterSpawner.StartWave(wave);
        else
            Debug.LogError("[StageManager] MonsterSpawner 참조가 null입니다.");
    }

    /// <summary>
    /// MonsterSpawner로부터 웨이브 클리어 알림 수신
    /// - 다음 웨이브가 있으면 WaveTransitionRoutine으로 분기 (사이 대기 + 블록 변형 예정)
    /// - 마지막 웨이브였으면 PhaseTransitionRoutine으로 분기 (다음 Layout 활성화)
    /// </summary>
    private void OnWaveClearedByMonsterSpawner()
    {
        // 전투 상태가 아니면 무시 (이미 다른 흐름으로 분기되었을 수 있음)
        if (currentState != StageState.Combat) return;

        if (IsLastWaveInCurrentPhase)
        {
            // 페이즈의 마지막 웨이브 — 페이즈 클리어 처리
            StartCoroutine(PhaseTransitionRoutine());
        }
        else
        {
            // 다음 웨이브로 — 사이 대기 후 시작
            StartWaveTransition();
        }
    }

    /// <summary>웨이브 사이 전환 코루틴 시작 (중복 방지)</summary>
    private void StartWaveTransition()
    {
        if (waveTransitionCoroutine != null)
            StopCoroutine(waveTransitionCoroutine);

        waveTransitionCoroutine = StartCoroutine(WaveTransitionRoutine());
    }

    /// <summary>
    /// 웨이브 사이 전환 루틴
    /// 1. 블록 변형 매니저에 변형 실행 위임 (있으면) → 완료까지 대기
    /// 2. WaveData.postWaveDelay만큼 추가 호흡 시간
    /// 3. 다음 웨이브 시작
    ///
    /// 이 시간 동안 currentState는 Combat 유지 — 즉 "전투 페이즈 진행 중"으로 간주.
    /// 변형 연출 동안 잠시 적이 0마리인 평화 시간이 됨.
    ///
    /// 변형/대기 중에 상태가 바뀌면(보스 전환, 사망 등) 안전하게 중단.
    /// </summary>
    private IEnumerator WaveTransitionRoutine()
    {
        PhaseData phase = GetCurrentPhase();
        WaveData wave = phase != null ? phase.GetWave(currentWaveIndex) : null;

        float delay = wave != null ? wave.postWaveDelay : 1.5f;

        Debug.Log($"[StageManager] 웨이브 {currentWaveIndex + 1} 클리어 — " +
                  $"블록 변형 + 호흡 {delay:F1}초");

        // 2. 변형 후 추가 호흡 시간 (다음 웨이브 시작 전 잠깐의 정적)
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        // 호흡 중에 상태가 바뀌었어도 중단
        if (currentState != StageState.Combat)
        {
            waveTransitionCoroutine = null;
            yield break;
        }

        // 3. 다음 웨이브 시작
        StartWave(currentWaveIndex + 1);

        waveTransitionCoroutine = null;
    }

    /// <summary>
    /// 페이즈 전환 루틴 (누적형 + CombatZone 대기)
    /// 1. 클리어 후 짧은 대기
    /// 2. 다음 Layout 추가 활성화 (기존 Layout은 유지)
    /// 3. CombatZone 감지 시작 (플레이어 진입 대기)
    /// 4. CombatZone 트리거 시 StartPhase() 호출 (OnCombatZoneTriggered에서)
    ///
    /// 마지막 페이즈 클리어 시에는 BeginBossPhaseTransition으로 분기
    /// </summary>
    private IEnumerator PhaseTransitionRoutine()
    {
        currentState = StageState.PhaseTransition;

        Debug.Log($"[StageManager] ===== 페이즈 {currentPhaseIndex + 1} 클리어 =====");

        yield return new WaitForSeconds(clearToActivateDelay);

        // 마지막 페이즈면 보스 페이즈 준비로 분기
        if (IsLastPhase)
        {
            BeginBossPhaseTransition();
            yield break;
        }

        PhaseData currentPhase = currentStageData.GetPhase(currentPhaseIndex);

        // 다음 Layout 추가 활성화 (누적형 — 기존 Layout 그대로 유지됨)
        int nextLayoutIndex = currentPhase.layoutIndexOnClear;

        if (arenaMapManager != null)
        {
            arenaMapManager.ActivateLayout(nextLayoutIndex, currentPhase.transitionDuration);
            yield return new WaitUntil(() => !arenaMapManager.IsAnyTransitioning());
        }

        yield return new WaitForSeconds(activateToDetectDelay);

        // 다음 페이즈 CombatZone 진입 대기 상태로 진입
        int nextPhaseIndex = currentPhaseIndex + 1;
        expectedPhaseIndex = nextPhaseIndex;
        transitionSubState = TransitionSubState.WaitingForCombatZone;

        // 다음 페이즈 시작 전 웨이브 인덱스 리셋
        currentWaveIndex = -1;

        Debug.Log($"[StageManager] Layout_{nextLayoutIndex} 활성화 완료 " +
                  $"— CombatZone_{nextPhaseIndex} 진입 대기 중...");
    }

    /// <summary>
    /// CombatZone이 트리거되었을 때의 콜백 (CombatZoneManager 이벤트)
    /// - 대기 중인 Phase Index와 일치하면 전투 시작 (또는 보스 소환)
    /// - 일치하지 않으면 무시 (잘못된 Zone 진입 방지)
    /// </summary>
    private void OnCombatZoneTriggered(int triggeredPhaseIndex)
    {
        // 대기 상태가 아니면 무시
        if (transitionSubState == TransitionSubState.None)
        {
            Debug.Log($"[StageManager] CombatZone_{triggeredPhaseIndex} 트리거됐지만 대기 중이 아님 → 무시");
            return;
        }

        // 기대하는 Phase Index와 일치하는지 검증
        if (triggeredPhaseIndex != expectedPhaseIndex)
        {
            Debug.LogWarning($"[StageManager] CombatZone_{triggeredPhaseIndex} 트리거됐으나 " +
                             $"기대값은 {expectedPhaseIndex} → 무시");
            return;
        }

        Debug.Log($"[StageManager] CombatZone_{triggeredPhaseIndex} 진입 확인 → 진행");

        // 보스 대기 상태였으면 보스 소환
        if (transitionSubState == TransitionSubState.WaitingForBossZone)
        {
            transitionSubState = TransitionSubState.None;
            expectedPhaseIndex = -1;

            SpawnBoss();
            return;
        }

        // 일반 페이즈 전환이면 해당 페이즈 시작
        if (transitionSubState == TransitionSubState.WaitingForCombatZone)
        {
            StartPhase(triggeredPhaseIndex);
        }
    }

    /// <summary>현재 진행 중인 페이즈 데이터 반환 (없으면 null)</summary>
    private PhaseData GetCurrentPhase()
    {
        if (currentStageData == null) return null;
        return currentStageData.GetPhase(currentPhaseIndex);
    }

    /// <summary>
    /// 현재 변형이 일어날 Layout 인덱스 결정.
    /// - 1순위: 플레이어가 실제로 있는 Layout (PlayerLayoutTracker)
    /// - 2순위(폴백): 가장 최근 활성화된 Layout (ArenaMapManager)
    /// MonsterSpanwer의 ResolveCurrentLayoutIndex와 동일한 로직 — 일관성 유지.
    /// </summary>
    private int ResolveCurrentLayoutIndex()
    {
        if (PlayerLayoutTracker.Instance != null)
        {
            int playerLayout = PlayerLayoutTracker.Instance.GetLatestEnteredLayout();
            if (playerLayout >= 0) return playerLayout;
        }

        if (ArenaMapManager.Instance != null)
            return ArenaMapManager.Instance.GetLatestActivatedIndex();

        return -1;
    }

    #endregion

    // ============================================================
    // 보스 페이즈 진입
    // ============================================================

    #region Boss Phase

    /// <summary>
    /// 보스 페이즈 준비 — 보스 Layout 활성화 + CombatZone 진입 대기
    /// - 보스 설정이 커스텀 Layout을 사용하면 해당 Layout을 추가 활성화
    /// - 설정이 없으면 즉시 보스 소환
    /// </summary>
    private void BeginBossPhaseTransition()
    {
        currentState = StageState.BossPhase;

        BossSettings boss = currentStageData.bossSettings;
        if (boss == null || boss.bossPrefab == null)
        {
            Debug.LogError("[StageManager] 보스 프리팹이 설정되지 않았습니다!");
            return;
        }

        // 스폰 중단 + 잔여 잡몹 정리
        monsterSpawner?.StopWave();
        if (boss.clearMobsOnBossSpawn)
            monsterSpawner?.ClearAllEnemies();

        // 보스 전용 Layout이 지정되어 있으면 활성화 + CombatZone 대기
        if (boss.useCustomBossLayout
            && boss.bossArenaLayoutIndex >= 0
            && arenaMapManager != null)
        {
            StartCoroutine(BossLayoutActivateRoutine(boss.bossArenaLayoutIndex));
        }
        else
        {
            // 보스 전용 Layout이 없으면 즉시 소환
            SpawnBoss();
        }
    }

    /// <summary>
    /// 보스 Layout 활성화 → CombatZone 진입 대기 루틴
    /// 보스 Layout 안에 phaseIndex가 마지막 페이즈 + 1인 CombatZone이 있어야 함
    /// (예: 페이즈 0,1,2면 보스 CombatZone의 phaseIndex = 3)
    /// </summary>
    private IEnumerator BossLayoutActivateRoutine(int bossLayoutIndex)
    {
        arenaMapManager.ActivateLayout(bossLayoutIndex);
        yield return new WaitUntil(() => !arenaMapManager.IsAnyTransitioning());

        yield return new WaitForSeconds(activateToDetectDelay);

        // 보스 CombatZone의 phaseIndex는 "마지막 일반 페이즈 + 1"로 약속
        expectedPhaseIndex = currentStageData.PhaseCount;
        transitionSubState = TransitionSubState.WaitingForBossZone;

        Debug.Log($"[StageManager] 보스 Layout_{bossLayoutIndex} 활성 " +
                  $"— 보스 CombatZone(phaseIndex={expectedPhaseIndex}) 진입 대기 중...");
    }

    /// <summary>
    /// 보스 실제 소환 (플레이어가 보스 CombatZone에 진입한 후 호출)
    /// </summary>
    private void SpawnBoss()
    {
        if (bossSpawned) return;

        bossSpawned = true;
        currentState = StageState.BossPhase;

        BossSettings boss = currentStageData.bossSettings;
        if (boss == null || boss.bossPrefab == null)
        {
            Debug.LogError("[StageManager] 보스 프리팹이 설정되지 않았습니다!");
            return;
        }

        // 보스 스폰 위치 — 아레나 중심 기준 오프셋
        Vector3 spawnPos;
        Quaternion spawnRot;

        if (boss.bossSpawnPoint != null)
        {
            spawnPos = boss.bossSpawnPoint.position;
            spawnRot = boss.bossSpawnPoint.rotation;
        }
        else
        {
            spawnPos = GetArenaCenter() + boss.spawnOffset;
            spawnRot = Quaternion.identity;
        }

        Instantiate(boss.bossPrefab, spawnPos, spawnRot);

        Debug.Log("[StageManager] ===== 보스 소환! =====");
    }

    #endregion

    // ============================================================
    // 킬 카운트 추적 (통계/UI용)
    // ============================================================

    #region Kill Tracking

    /// <summary>
    /// 적 처치 이벤트 수신
    /// - 통계용 카운트만 증가. 웨이브 클리어 판정은 MonsterSpawner가 담당.
    /// </summary>
    private void OnEnemyKilled()
    {
        totalKillCount++;
    }

    #endregion

    // ============================================================
    // 스테이지 종료 (클리어 / 실패)
    // ============================================================

    #region Stage End

    /// <summary>보스 처치 시 호출 (Boss.OnBossDeath 이벤트)</summary>
    private void OnBossDefeated()
    {
        if (currentState != StageState.BossPhase) return;
        StartCoroutine(HandleStageClear());
    }

    /// <summary>스테이지 클리어 처리</summary>
    private IEnumerator HandleStageClear()
    {
        currentState = StageState.Cleared;

        Debug.Log($"[StageManager] ===== 아레나 클리어! 총 처치: {totalKillCount} =====");

        monsterSpawner?.ClearAllEnemies();

        yield return new WaitForSeconds(1f);

        // 클리어 보상 지급 (골드, 별가루)
        GiveClearRewards();

        // 디버프 / 층 효과 정리
        FloorEffectManager.Instance?.OnFloorCleared();
        RewardManager.Instance?.OnFloorCleared();

        yield return new WaitForSeconds(1f);

        // 필드 보상 오브젝트
        Vector3 playerPos = GetPlayerPosition();
        RewardObjectSpawner.Instance?.SpawnClearRewards(playerPos);

        Debug.Log("[StageManager] 클리어 보상 지급 완료.");

        // TODO: 다음 씬 전환 (상점, 거점 등)
    }

    /// <summary>플레이어 사망 감지</summary>
    private void CheckPlayerDeath()
    {
        if (PlayerStats.Instance == null) return;
        if (!PlayerStats.Instance.isDead) return;
        if (currentState == StageState.Failed || currentState == StageState.Cleared) return;

        OnPlayerDeath();
    }

    /// <summary>런 실패 처리</summary>
    private void OnPlayerDeath()
    {
        currentState = StageState.Failed;

        monsterSpawner?.StopWave();
        StopAllCoroutines();

        Debug.Log($"[StageManager] ===== 런 실패 — 총 처치: {totalKillCount} =====");
    }

    /// <summary>클리어 보상 지급 (골드, 별가루)</summary>
    private void GiveClearRewards()
    {
        if (PlayerStats.Instance == null || currentStageData == null) return;

        PlayerStats.Instance.AddCoin(currentStageData.clearGold);
        PlayerStats.Instance.AddStardust(currentStageData.clearStardust);

        Debug.Log($"[StageManager] 보상 — 골드: {currentStageData.clearGold}, 별가루: {currentStageData.clearStardust}");
    }

    #endregion

    // ============================================================
    // 유틸리티
    // ============================================================

    #region Utility

    /// <summary>플레이어 현재 위치 반환</summary>
    private Vector3 GetPlayerPosition()
    {
        GameObject player = GameObject.FindWithTag("Player");
        return player != null ? player.transform.position : Vector3.zero;
    }

    /// <summary>아레나 중심 좌표 반환</summary>
    private Vector3 GetArenaCenter()
    {
        if (ArenaMapManager.Instance != null)
            return ArenaMapManager.Instance.GetArenaCenter();
        return Vector3.zero;
    }

    #endregion

    // ============================================================
    // 외부 인터페이스
    // ============================================================

    #region Public Interface

    /// <summary>아레나 재시작</summary>
    public void RestartArena()
    {
        StopAllCoroutines();
        waveTransitionCoroutine = null;
        monsterSpawner?.ResetSpawner();
        arenaMapManager?.ResetAll();
        StartArena();
    }

    /// <summary>새로운 스테이지 데이터 로드</summary>
    public void LoadStageData(StageData newData)
    {
        currentStageData = newData;
    }

    /// <summary>현재 스테이지 데이터 반환</summary>
    public StageData GetCurrentStageData() => currentStageData;

    #endregion

    // ============================================================
    // 디버그
    // ============================================================

    #region Debug

    [ContextMenu("현재 상황 확인")]
    public void LogCurrentStatus()
    {
        Debug.Log("===== 아레나 현황 =====");
        Debug.Log($"스테이지: {currentStageData?.stageName}");
        Debug.Log($"상태: {currentState}");
        Debug.Log($"전환 하위 상태: {transitionSubState}");
        Debug.Log($"현재 페이즈: {currentPhaseIndex + 1} / {currentStageData?.PhaseCount}");
        Debug.Log($"현재 웨이브: {currentWaveIndex + 1} / " +
                  $"{(GetCurrentPhase()?.WaveCount ?? 0)}");
        Debug.Log($"기대 Phase Index: {expectedPhaseIndex}");
        Debug.Log($"총 처치: {totalKillCount}");
        Debug.Log($"보스 소환됨: {bossSpawned}");
    }

    /// <summary>[디버그] 현재 웨이브를 강제 클리어 (다음 웨이브 또는 페이즈 전환으로 진행)</summary>
    [ContextMenu("강제 웨이브 클리어")]
    public void ForceClearWave()
    {
        if (currentState == StageState.Combat)
        {
            monsterSpawner?.ClearAllEnemies();
            OnWaveClearedByMonsterSpawner();
        }
    }

    /// <summary>[디버그] 현재 페이즈의 모든 웨이브를 한 번에 클리어 (페이즈 전환으로 직행)</summary>
    [ContextMenu("강제 페이즈 클리어 (모든 웨이브)")]
    public void ForceClearPhase()
    {
        if (currentState != StageState.Combat) return;

        PhaseData phase = GetCurrentPhase();
        if (phase == null) return;

        // 모든 웨이브를 다 진행한 것으로 간주
        currentWaveIndex = phase.WaveCount - 1;
        monsterSpawner?.StopWave();
        monsterSpawner?.ClearAllEnemies();

        StartCoroutine(PhaseTransitionRoutine());
    }

    [ContextMenu("강제 CombatZone 트리거 (디버그)")]
    public void ForceCombatZoneTrigger()
    {
        if (expectedPhaseIndex >= 0)
        {
            Debug.Log($"[StageManager] 디버그: Phase {expectedPhaseIndex} 강제 트리거");
            OnCombatZoneTriggered(expectedPhaseIndex);
        }
        else
        {
            Debug.LogWarning("[StageManager] 디버그: 대기 중인 CombatZone이 없습니다.");
        }
    }

    #endregion

    // ============================================================
    // 레거시 호환용
    // ============================================================

    #region Legacy Compatibility

    /// <summary>[레거시] 현재 층 번호</summary>
    public int CurrentFloor => currentStageData?.chapterNumber ?? 1;

    /// <summary>[레거시] 현재 챕터 번호</summary>
    public int CurrentChapter => ChapterNumber;

    /// <summary>[레거시] GetCurrentFloor</summary>
    public int GetCurrentFloor() => CurrentFloor;

    /// <summary>[레거시] GetCurrentChapter</summary>
    public int GetCurrentChapter() => ChapterNumber;

    /// <summary>[레거시] RestartStage</summary>
    public void RestartStage() => RestartArena();

    /// <summary>[레거시] StartNewStage</summary>
    public void StartNewStage(StageData newData)
    {
        LoadStageData(newData);
        RestartArena();
    }

    /// <summary>[레거시] OnPortalEntered — 포탈 시스템 폐지됨</summary>
    public void OnPortalEntered(FloorChoice choice)
    {
        Debug.LogWarning("[StageManager] OnPortalEntered는 폐지된 메서드입니다.");
    }

    /// <summary>[레거시] NotifyRewardSelected — 포탈/보상 선택 시스템 폐지</summary>
    public void NotifyRewardSelected()
    {
        Debug.LogWarning("[StageManager] NotifyRewardSelected는 폐지된 메서드입니다.");
    }

    #endregion

    // ============================================================
    // 이벤트 구독 헬퍼 — 중복 구독 방지 + 늦은 초기화 대응
    // ============================================================

    #region Event Subscription Helpers

    /// <summary>
    /// MonsterSpawner의 OnWaveCleared 이벤트 구독
    /// - 중복 구독 방지를 위해 -= 먼저 호출 후 +=
    /// </summary>
    private void SubscribeToMonsterSpawner()
    {
        if (monsterSpawner == null) return;

        monsterSpawner.OnWaveCleared -= OnWaveClearedByMonsterSpawner;
        monsterSpawner.OnWaveCleared += OnWaveClearedByMonsterSpawner;
    }

    /// <summary>MonsterSpawner 이벤트 구독 해제</summary>
    private void UnsubscribeFromMonsterSpawner()
    {
        if (monsterSpawner == null) return;
        monsterSpawner.OnWaveCleared -= OnWaveClearedByMonsterSpawner;
    }

    /// <summary>
    /// CombatZoneManager의 OnCombatZoneTriggered 이벤트 구독
    /// - OnEnable 시점에 Instance가 null일 수 있어 Start에서 재시도되도록 분리
    /// - 중복 구독 방지를 위해 -= 먼저 호출 후 +=
    /// </summary>
    private void SubscribeToCombatZoneManager()
    {
        if (CombatZoneManager.Instance == null)
        {
            Debug.LogWarning("[StageManager] CombatZoneManager.Instance가 아직 null입니다. " +
                             "Start에서 재시도됩니다.");
            return;
        }

        CombatZoneManager.Instance.OnCombatZoneTriggered -= OnCombatZoneTriggered;
        CombatZoneManager.Instance.OnCombatZoneTriggered += OnCombatZoneTriggered;
    }

    /// <summary>CombatZoneManager 이벤트 구독 해제</summary>
    private void UnsubscribeFromCombatZoneManager()
    {
        if (CombatZoneManager.Instance == null) return;
        CombatZoneManager.Instance.OnCombatZoneTriggered -= OnCombatZoneTriggered;
    }

    #endregion
}
