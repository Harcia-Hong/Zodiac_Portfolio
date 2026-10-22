using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// ============================================================
/// MonsterSpanwer.cs — Layout 기반 아케이드 웨이브 몬스터 스포너 (모듈 토글 지원)
/// ============================================================
/// 누적형 아레나 구조에서 "플레이어가 있는 Layout의 가장자리"에서
/// VFX 연출과 함께 몬스터를 스폰하는 시스템.
///
/// 핵심 단위는 "웨이브"이며, 한 페이즈 안에 여러 웨이브가 순차로 진행됨.
/// 이 클래스는 한 번에 하나의 웨이브만 처리한다.
/// 페이즈/웨이브 진행 흐름은 StageManager가 제어.
///
/// 핵심 역할:
///   1. StartWave(WaveData): 웨이브 시작 — 스폰 루프 시작
///   2. 플레이어가 있는 Layout의 스폰 포인트 동적 조회
///      - LayoutSpawnData (Core/공통)
///      - 활성 LayoutModuleGroup들의 SpawnPoints (모듈)
///      - 합쳐서 랜덤 선택 → 비활성 모듈에서는 스폰 안 됨
///   3. 스폰 VFX 재생 → 딜레이 → 실제 몬스터 Instantiate + 등장 애니메이션
///   4. 고정 총 몰량 + 동시 생존 수 제한 (빈 자리만 보충)
///   5. 처치율이 트리거 비율 도달 시 엘리트 1회 스폰
///   6. 총 몰량 소진 + 필드 적 0마리 = 웨이브 클리어
///   7. 클리어 시 OnWaveCleared 이벤트 발행
///
/// 호환성:
///   StartPhase(PhaseData)는 레거시 wrapper로 유지.
///   OnPhaseCleared 이벤트도 alias로 유지되어 기존 StageManager 코드와 호환됨.
///
/// 참고: 클래스명 오타(Spanwer)는 기존 코드 호환성을 위해 유지.
/// ============================================================
public class MonsterSpanwer : MonoBehaviour
{
    // ============================================================
    // 스폰 연출 타입
    // ============================================================

    /// <summary>몬스터 등장 애니메이션 방식</summary>
    public enum SpawnAnimationType
    {
        None,       // 애니메이션 없음 (즉시 등장)
        DropDown,   // 위에서 떨어짐
        ScaleUp     // 작게 시작 → 커지면서 등장
    }

    // ============================================================
    // 스폰 설정
    // ============================================================

    [Header("스폰 위치 오프셋")]
    [Tooltip("스폰 포인트 주변 랜덤 오프셋 반경 (겹침 방지)")]
    [SerializeField] private float spawnOffsetRadius = 1.5f;

    [Header("스폰 연출 — VFX")]
    [Tooltip("스폰 시 스폰 포인트에서 재생할 VFX 프리팹 (파티클/라이트 등)")]
    [SerializeField] private GameObject spawnVFXPrefab;

    [Tooltip("VFX 재생 후 몬스터가 등장하기까지의 대기 시간 (초)")]
    [SerializeField, Range(0f, 2f)] private float spawnDelay = 0.4f;

    [Tooltip("VFX가 자동 제거되는 시간 (초). spawnDelay보다 길어야 함")]
    [SerializeField] private float vfxLifetime = 1.5f;

    [Header("스폰 연출 — 몬스터 등장 애니메이션")]
    [Tooltip("몬스터가 등장할 때 재생할 애니메이션 방식")]
    [SerializeField] private SpawnAnimationType spawnAnimationType = SpawnAnimationType.DropDown;

    [Tooltip("DropDown 시 시작 높이 (스폰 포인트 Y + 이 값에서 떨어짐)")]
    [SerializeField] private float dropDownHeight = 5f;

    [Tooltip("등장 애니메이션 지속 시간 (초)")]
    [SerializeField] private float spawnAnimationDuration = 0.35f;

    // ============================================================
    // 런타임 상태
    // ============================================================

    [Header("현재 상태 (디버깅용)")]
    [SerializeField] private int aliveEnemyCount = 0;
    [SerializeField] private int totalSpawnedCount = 0;
    [SerializeField] private int remainingNormalCount = 0;
    [SerializeField] private int normalKilledCount = 0;

    [SerializeField] private bool isWaveActive = false;

    /// <summary>현재 필드에 살아있는 모든 몬스터 리스트</summary>
    private List<Enemy> aliveEnemies = new List<Enemy>();

    /// <summary>현재 진행 중인 웨이브 데이터</summary>
    private WaveData currentWave;

    /// <summary>엘리트가 이미 스폰되었는지 (중복 방지)</summary>
    private bool eliteSpawned = false;

    /// <summary>웨이브 스폰 코루틴 참조</summary>
    private Coroutine spawnCoroutine;

    /// <summary>Layout 인덱스별 LayoutSpawnData(Core/공통) 캐시</summary>
    private readonly Dictionary<int, LayoutSpawnData> spawnDataCache
        = new Dictionary<int, LayoutSpawnData>();

    /// <summary>Layout 인덱스별 LayoutModuleGroup[] 캐시 (활성 모듈 스폰 포인트 조회용)</summary>
    private readonly Dictionary<int, LayoutModuleGroup[]> moduleGroupCache
        = new Dictionary<int, LayoutModuleGroup[]>();

    /// <summary>스폰 포인트 후보 임시 작업 리스트 (매번 새 List 할당 방지 = GC 절감)</summary>
    private readonly List<Transform> spawnPointBuffer = new List<Transform>();

    // ============================================================
    // 이벤트
    // ============================================================

    /// <summary>웨이브 클리어 시 발행 (모든 적 처치 + 총 몰량 소진)</summary>
    public event System.Action OnWaveCleared;

    /// <summary>엘리트 스폰 시 발행 (UI 경고, 사운드 등)</summary>
    public event System.Action OnEliteSpawned;

    /// <summary>[레거시 alias] OnWaveCleared와 동일 — 기존 StageManager 호환용</summary>
    public event System.Action OnPhaseCleared
    {
        add { OnWaveCleared += value; }
        remove { OnWaveCleared -= value; }
    }

    // ============================================================
    // 웨이브 진행 (핵심 진입점)
    // ============================================================

    #region Wave Control

    /// <summary>웨이브 시작 — 총 몰량 기반 스폰 루프 시작</summary>
    public void StartWave(WaveData waveData)
    {
        if (waveData == null)
        {
            Debug.LogError("[MonsterSpawner] WaveData가 null입니다.");
            return;
        }

        if (isWaveActive)
        {
            Debug.LogWarning("[MonsterSpawner] 이미 웨이브 진행 중입니다.");
            return;
        }

        currentWave = waveData;
        remainingNormalCount = waveData.totalNormalCount;
        normalKilledCount = 0;
        eliteSpawned = false;
        isWaveActive = true;

        Debug.Log($"[MonsterSpawner] 웨이브 시작: {waveData.waveName} " +
                  $"(총 일반 {waveData.totalNormalCount}마리, 엘리트 {waveData.eliteCount}마리)");

        spawnCoroutine = StartCoroutine(WaveSpawnRoutine());
    }

    /// <summary>웨이브 강제 중단 — 진행 중인 스폰 루프만 멈춤</summary>
    public void StopWave()
    {
        if (!isWaveActive) return;

        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        isWaveActive = false;
        Debug.Log("[MonsterSpawner] 웨이브 중단");
    }

    #endregion

    // ============================================================
    // 페이즈 진행 (레거시 호환 wrapper)
    // ============================================================

    #region Legacy Phase Wrapper

    /// <summary>[레거시 호환] PhaseData 기반 페이즈 시작 — 첫 웨이브로 위임</summary>
    public void StartPhase(PhaseData phaseData)
    {
        if (phaseData == null)
        {
            Debug.LogError("[MonsterSpawner] PhaseData가 null입니다.");
            return;
        }

        WaveData firstWave = phaseData.GetWave(0);
        if (firstWave == null)
        {
            Debug.LogError("[MonsterSpawner] PhaseData에서 웨이브를 추출할 수 없습니다.");
            return;
        }

        StartWave(firstWave);
    }

    /// <summary>[레거시 호환] StopWave와 동일</summary>
    public void StopPhase() => StopWave();

    #endregion

    // ============================================================
    // 스폰 루프 (코루틴)
    // ============================================================

    #region Spawn Routine

    /// <summary>웨이브 스폰 메인 루프 — 클리어 조건 충족까지 보충 스폰 반복</summary>
    private IEnumerator WaveSpawnRoutine()
    {
        while (isWaveActive)
        {
            CleanupDeadReferences();

            // 엘리트 트리거 체크
            if (!eliteSpawned && currentWave.ShouldTriggerElite(normalKilledCount))
            {
                SpawnEliteBurst();
                eliteSpawned = true;
            }

            // 일반 몬스터 보충 스폰
            TrySpawnNormalBatch();

            // 웨이브 클리어 조건 체크
            if (IsWaveCompleted())
            {
                CompleteWave();
                yield break;
            }

            yield return new WaitForSeconds(currentWave.spawnInterval);
        }
    }

    /// <summary>일반 몬스터 보충 스폰 (빈 자리 + 남은 몰량 한도)</summary>
    private void TrySpawnNormalBatch()
    {
        if (remainingNormalCount <= 0) return;

        int emptySlots = currentWave.maxAliveCount - aliveEnemies.Count;
        if (emptySlots <= 0) return;

        int canSpawn = Mathf.Min(emptySlots, currentWave.spawnBatchSize, remainingNormalCount);

        for (int i = 0; i < canSpawn; i++)
        {
            GameObject prefab = currentWave.GetRandomNormalPrefab();
            if (prefab == null)
            {
                Debug.LogWarning("[MonsterSpawner] 일반 몬스터 프리팹이 설정되지 않았습니다.");
                return;
            }

            if (TryStartSpawn(prefab, isElite: false))
                remainingNormalCount--;
        }
    }

    /// <summary>엘리트 몬스터 1회 스폰 (동시 생존 상한 무시)</summary>
    private void SpawnEliteBurst()
    {
        int eliteCount = currentWave.eliteCount;

        for (int i = 0; i < eliteCount; i++)
        {
            GameObject prefab = currentWave.GetRandomElitePrefab();
            if (prefab == null)
            {
                Debug.LogWarning("[MonsterSpawner] 엘리트 프리팹이 설정되지 않았습니다.");
                return;
            }

            TryStartSpawn(prefab, isElite: true);
        }

        OnEliteSpawned?.Invoke();
        Debug.Log($"[MonsterSpawner] 엘리트 {eliteCount}마리 출현!");
    }

    #endregion

    // ============================================================
    // 개별 몬스터 스폰 (VFX + 딜레이 + 애니메이션)
    // ============================================================

    #region Single Spawn

    /// <summary>몬스터 스폰 시작 — 스폰 포인트 결정 + 코루틴 실행</summary>
    private bool TryStartSpawn(GameObject prefab, bool isElite)
    {
        if (prefab == null) return false;

        Transform spawnPoint = GetSpawnPointFromCurrentLayout();
        if (spawnPoint == null)
        {
            Debug.LogWarning("[MonsterSpawner] 스폰 포인트를 찾을 수 없습니다.");
            return false;
        }

        // 스폰 위치 — 스폰 포인트 + 랜덤 오프셋 (겹침 방지)
        Vector2 randCircle = Random.insideUnitCircle * spawnOffsetRadius;
        Vector3 spawnPos = spawnPoint.position + new Vector3(randCircle.x, 0f, randCircle.y);
        Quaternion rot = spawnPoint.rotation;

        totalSpawnedCount++;

        StartCoroutine(SpawnWithVFXRoutine(prefab, spawnPos, rot, isElite));
        return true;
    }

    /// <summary>VFX 재생 → 딜레이 → 몬스터 Instantiate + 등장 애니메이션</summary>
    private IEnumerator SpawnWithVFXRoutine(GameObject prefab, Vector3 spawnPos, Quaternion rot, bool isElite)
    {
        PlaySpawnVFX(spawnPos);

        yield return new WaitForSeconds(spawnDelay);

        if (!isWaveActive)
        {
            totalSpawnedCount = Mathf.Max(0, totalSpawnedCount - 1);
            yield break;
        }

        GameObject enemyObj = Instantiate(prefab, spawnPos, rot);
        Enemy enemyComponent = enemyObj.GetComponent<Enemy>();

        if (enemyComponent == null)
        {
            Debug.LogWarning($"[MonsterSpawner] Enemy 컴포넌트 없음: {prefab.name}");
            Destroy(enemyObj);
            totalSpawnedCount = Mathf.Max(0, totalSpawnedCount - 1);
            yield break;
        }

        bool isEliteLocal = isElite;
        enemyComponent.SetDeathCallback(() => OnEnemyDied(enemyComponent, isEliteLocal));

        aliveEnemies.Add(enemyComponent);
        UpdateAliveCount();

        PlaySpawnAnimation(enemyObj, spawnPos);
    }

    /// <summary>스폰 VFX 재생</summary>
    private void PlaySpawnVFX(Vector3 position)
    {
        if (spawnVFXPrefab == null) return;

        GameObject vfx = Instantiate(spawnVFXPrefab, position, Quaternion.identity);
        Destroy(vfx, vfxLifetime);
    }

    /// <summary>몬스터 등장 애니메이션 재생</summary>
    private void PlaySpawnAnimation(GameObject enemyObj, Vector3 finalPos)
    {
        if (enemyObj == null) return;

        Transform t = enemyObj.transform;

        switch (spawnAnimationType)
        {
            case SpawnAnimationType.DropDown:
                Vector3 startPos = finalPos + Vector3.up * dropDownHeight;
                t.position = startPos;
                t.DOMoveY(finalPos.y, spawnAnimationDuration).SetEase(Ease.OutBounce);
                break;

            case SpawnAnimationType.ScaleUp:
                Vector3 finalScale = t.localScale;
                t.localScale = Vector3.zero;
                t.DOScale(finalScale, spawnAnimationDuration).SetEase(Ease.OutBack);
                break;

            case SpawnAnimationType.None:
            default:
                break;
        }
    }

    #endregion

    // ============================================================
    // 스폰 위치 결정 (Layout + 활성 모듈 통합)
    // ============================================================

    #region Spawn Point Resolution

    /// <summary>
    /// 현재 플레이어가 있는 Layout의 스폰 포인트 후보를 모두 모아 랜덤 선택.
    /// 후보 = LayoutSpawnData(Core 가장자리) + 활성 LayoutModuleGroup의 스폰 포인트들.
    /// 비활성 모듈은 자동 제외 (LayoutModuleGroup.GetActiveSpawnPoints가 빈 배열 반환).
    /// </summary>
    private Transform GetSpawnPointFromCurrentLayout()
    {
        int layoutIndex = ResolveCurrentLayoutIndex();
        if (layoutIndex < 0) return null;

        // 매 호출마다 새 List 할당하지 않도록 재사용 버퍼 활용
        spawnPointBuffer.Clear();

        // 1. LayoutSpawnData (Core/공통) 스폰 포인트 추가
        AppendCoreSpawnPoints(layoutIndex, spawnPointBuffer);

        // 2. 활성 모듈 스폰 포인트 추가
        AppendActiveModuleSpawnPoints(layoutIndex, spawnPointBuffer);

        if (spawnPointBuffer.Count == 0) return null;

        // 통합된 후보 중 랜덤 선택
        return spawnPointBuffer[Random.Range(0, spawnPointBuffer.Count)];
    }

    /// <summary>LayoutSpawnData의 스폰 포인트들을 버퍼에 추가</summary>
    private void AppendCoreSpawnPoints(int layoutIndex, List<Transform> buffer)
    {
        LayoutSpawnData spawnData = GetCachedSpawnData(layoutIndex);
        if (spawnData == null) return;

        Transform[] points = spawnData.GetEnemySpawnPoints();
        if (points == null) return;

        foreach (Transform pt in points)
        {
            if (pt != null) buffer.Add(pt);
        }
    }

    /// <summary>활성 LayoutModuleGroup들의 스폰 포인트들을 버퍼에 추가</summary>
    private void AppendActiveModuleSpawnPoints(int layoutIndex, List<Transform> buffer)
    {
        LayoutModuleGroup[] modules = GetCachedModuleGroups(layoutIndex);
        if (modules == null) return;

        foreach (LayoutModuleGroup module in modules)
        {
            if (module == null) continue;

            // GetActiveSpawnPoints는 비활성 모듈에 대해 빈 배열 반환
            // → 비활성 모듈에서 스폰 안 됨
            Transform[] modulePoints = module.GetActiveSpawnPoints();
            if (modulePoints == null) continue;

            foreach (Transform pt in modulePoints)
            {
                if (pt != null) buffer.Add(pt);
            }
        }
    }

    /// <summary>
    /// 현재 스폰 대상 Layout 인덱스 결정
    /// - 1순위: 플레이어가 실제로 있는 Layout
    /// - 2순위(폴백): 가장 최근 활성화된 Layout
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

    /// <summary>LayoutSpawnData 캐시 조회 (캐시 미스 시 GetComponent + 캐싱)</summary>
    private LayoutSpawnData GetCachedSpawnData(int layoutIndex)
    {
        if (spawnDataCache.TryGetValue(layoutIndex, out LayoutSpawnData cached))
        {
            if (cached != null) return cached;
            spawnDataCache.Remove(layoutIndex);
        }

        if (ArenaMapManager.Instance == null) return null;

        Transform layoutTransform = ArenaMapManager.Instance.GetLayoutTransform(layoutIndex);
        if (layoutTransform == null) return null;

        LayoutSpawnData spawnData = layoutTransform.GetComponent<LayoutSpawnData>();
        if (spawnData != null)
            spawnDataCache[layoutIndex] = spawnData;

        return spawnData;
    }

    /// <summary>
    /// LayoutModuleGroup 배열 캐시 조회 (캐시 미스 시 GetComponentsInChildren + 캐싱).
    /// ModuleToggleManager가 동일한 캐시를 가지지만 매니저가 없을 때를 대비해
    /// 자체 캐시를 둠 (의존 분리).
    /// </summary>
    private LayoutModuleGroup[] GetCachedModuleGroups(int layoutIndex)
    {
        if (moduleGroupCache.TryGetValue(layoutIndex, out LayoutModuleGroup[] cached))
        {
            if (cached != null && IsCacheValid(cached)) return cached;
            moduleGroupCache.Remove(layoutIndex);
        }

        if (ArenaMapManager.Instance == null) return null;

        Transform layoutTransform = ArenaMapManager.Instance.GetLayoutTransform(layoutIndex);
        if (layoutTransform == null) return null;

        // includeInactive: true — 가라앉아 비활성 GameObject가 된 모듈도 포함
        LayoutModuleGroup[] modules = layoutTransform.GetComponentsInChildren<LayoutModuleGroup>(true);

        if (modules.Length > 0)
            moduleGroupCache[layoutIndex] = modules;

        return modules;
    }

    /// <summary>캐시된 모듈 배열이 모두 유효한지 (Destroy로 사라진 게 없는지)</summary>
    private bool IsCacheValid(LayoutModuleGroup[] cached)
    {
        foreach (LayoutModuleGroup m in cached)
        {
            if (m == null) return false;
        }
        return true;
    }

    #endregion

    // ============================================================
    // 몬스터 사망 처리
    // ============================================================

    #region Death Handling

    private void OnEnemyDied(Enemy deadEnemy, bool wasElite)
    {
        aliveEnemies.Remove(deadEnemy);

        if (!wasElite)
            normalKilledCount++;

        UpdateAliveCount();
    }

    private void CleanupDeadReferences()
    {
        aliveEnemies.RemoveAll(e => e == null || e.gameObject == null);
        UpdateAliveCount();
    }

    private void UpdateAliveCount()
    {
        aliveEnemyCount = aliveEnemies.Count;
    }

    #endregion

    // ============================================================
    // 웨이브 완료 판정
    // ============================================================

    #region Wave Completion

    private bool IsWaveCompleted()
    {
        if (remainingNormalCount > 0) return false;

        bool eliteConditionMet = currentWave.eliteCount <= 0 || eliteSpawned;
        if (!eliteConditionMet) return false;

        return aliveEnemies.Count == 0;
    }

    private void CompleteWave()
    {
        isWaveActive = false;
        spawnCoroutine = null;

        Debug.Log($"[MonsterSpawner] 웨이브 클리어: {currentWave.waveName}");

        OnWaveCleared?.Invoke();
    }

    #endregion

    // ============================================================
    // 정리
    // ============================================================

    #region Cleanup

    public void ClearAllEnemies()
    {
        foreach (Enemy enemy in aliveEnemies)
        {
            if (enemy != null && enemy.gameObject != null)
                Destroy(enemy.gameObject);
        }

        aliveEnemies.Clear();
        aliveEnemyCount = 0;

        Debug.Log("[MonsterSpawner] 모든 몬스터 제거 완료");
    }

    public void ResetSpawner()
    {
        StopWave();
        ClearAllEnemies();
        ResetCounts();
        spawnDataCache.Clear();
        moduleGroupCache.Clear();

        Debug.Log("[MonsterSpawner] 스포너 완전 리셋");
    }

    #endregion

    // ============================================================
    // 외부 인터페이스
    // ============================================================

    #region Public Interface

    public int GetAliveEnemyCount() => aliveEnemies.Count;
    public int GetTotalSpawnedCount() => totalSpawnedCount;
    public int GetRemainingNormalCount() => remainingNormalCount;
    public int GetNormalKilledCount() => normalKilledCount;

    public bool IsWaveActive() => isWaveActive;
    public bool IsPhaseActive() => isWaveActive;

    public bool IsEliteSpawned() => eliteSpawned;

    public void ResetCounts()
    {
        totalSpawnedCount = 0;
        aliveEnemyCount = 0;
        remainingNormalCount = 0;
        normalKilledCount = 0;
    }

    #endregion
}
