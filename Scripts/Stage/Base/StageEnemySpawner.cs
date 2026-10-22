using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스폰 방식
/// </summary>
public enum SpawnMode
{
    RandomPool,        // 기존: 풀에서 랜덤 뽑기 (enemiesPerWave 만큼)
    WaveComposition    // 신규: 웨이브별 적 종류+수 직접 지정
}

/// <summary>
/// 한 종류의 적 + 스폰 수 (WaveComposition 모드용)
/// </summary>
[System.Serializable]
public class EnemySpawnEntry
{
    [Tooltip("스폰할 적 프리팹")]
    public GameObject enemyPrefab;

    [Tooltip("이 웨이브에서 이 적을 몇 마리 스폰할지")]
    [Min(0)] public int count = 1;
}

/// <summary>
/// 한 웨이브의 구성 (WaveComposition 모드용)
/// </summary>
[System.Serializable]
public class EnemyWaveData
{
    [Tooltip("웨이브 이름 (디버그/인스펙터 가독성)")]
    public string waveName = "Wave";

    [Tooltip("이 웨이브에 나올 적들 + 각 수")]
    public List<EnemySpawnEntry> enemies = new List<EnemySpawnEntry>();
}

/// ============================================================
/// StageEnemySpawner.cs — Stage 씬용 웨이브 기반 스포너
/// ============================================================
/// 세그먼트 트윈즈 스타일 웨이브 시스템:
///   - 한 스테이지에 N개 웨이브, 각 웨이브에 M마리 적 스폰
///   - 한 웨이브 적 전원 처치 → 다음 웨이브 시작
///   - 모든 웨이브 종료 → OnAllWavesCompleted 이벤트 발화 (외부 클리어 트리거)
///
/// [스폰 분배 — 셔플 풀 (Fisher-Yates)]
///   8개 스폰 포인트를 셔플한 순서대로 차례로 사용 (한 바퀴 돌면 재셔플)
///   → 한 곳에 몰리지 않고 여러 군데에서 골고루 나타남
///
/// [흐름]
///   StartSpawning()
///     → Wave 0: enemiesPerWave 마리 스폰 → 전원 처치 대기 → 휴식
///     → ... → 마지막 웨이브 → OnAllWavesCompleted 발화
///
/// [GlobalTimer 연동]
///   적 스폰 시점의 GlobalTimer.CurrentLevel을 적용 → 시간 ↑ → 적 강해짐
/// ============================================================
public class StageEnemySpawner : MonoBehaviour
{
    // ============================================================
    // Inspector 설정
    // ============================================================
    [Header("스폰 모드")]
    [SerializeField, Tooltip("RandomPool = 풀에서 랜덤 / WaveComposition = 웨이브별 지정")]
    private SpawnMode spawnMode = SpawnMode.RandomPool;

    [Header("난이도 (스테이지 레벨)")]
    [SerializeField, Tooltip("이 스테이지의 적 레벨. 높을수록 적 HP/공격력 ↑.\n" +
    "스테이지 진행하면서 올려 난이도 상승 (예: 1-1=1, 1-2=2 ... 2-10=10).\n" +
    "GlobalTimer(시간 기반)와 비교해 더 큰 값 적용됨")]
    private int stageLevel = 1;

    [Header("웨이브 구성 (WaveComposition 모드 전용)")]
    [SerializeField, Tooltip("각 웨이브의 적 종류+수 (위에서 아래 순서로 진행)")]
    private List<EnemyWaveData> waves = new List<EnemyWaveData>();

    [Header("스폰할 적 프리팹")]
    [SerializeField, Tooltip("스폰 풀 — 매 스폰마다 랜덤 선택")]
    private GameObject[] enemyPrefabs;

    [Header("스폰 포인트")]
    [SerializeField, Tooltip("적이 스폰될 위치들 (8개 정도 사방에 배치 권장). 셔플된 순서로 골고루 사용됨")]
    private Transform[] spawnPoints;

    [Header("웨이브 설정")]
    [SerializeField, Tooltip("총 웨이브 수 (예: 2 → 2번 나눠서 스폰)")]
    private int totalWaves = 2;

    [SerializeField, Tooltip("한 웨이브에 스폰할 적 수 (예: 5)")]
    private int enemiesPerWave = 5;

    [SerializeField, Tooltip("웨이브 사이 휴식 시간 (초)")]
    private float waveInterval = 1.0f;

    [Header("스폰 설정")]
    [SerializeField, Tooltip("동시에 살아있을 수 있는 최대 적 수 (안전장치)")]
    private int maxAliveEnemies = 8;

    [SerializeField, Tooltip("웨이브 내 스폰 간격 (초)")]
    private float baseSpawnInterval = 0.5f;

    [SerializeField, Tooltip("스폰 위치 주변 랜덤 오프셋 반경 (한 곳에 정확히 겹치는 거 방지)")]
    private float spawnOffsetRadius = 1f;

    // ============================================================
    // 런타임 상태 (디버깅용)
    // ============================================================

    [Header("현재 상태 (디버깅용 - 직접 수정 금지)")]
    [SerializeField, Tooltip("현재 살아있는 적 수")]
    private int aliveCount = 0;

    [SerializeField, Tooltip("현재 진행 중인 웨이브 인덱스 (0부터, 시작 전엔 -1)")]
    private int currentWaveIndex = -1;

    [SerializeField, Tooltip("현재 웨이브에서 처치된 적 수")]
    private int currentWaveKilledCount = 0;

    [SerializeField, Tooltip("모든 웨이브 완료 여부")]
    private bool allWavesCompleted = false;

    /// <summary>살아있는 적 리스트</summary>
    private readonly List<Enemy> aliveEnemies = new List<Enemy>();

    /// <summary>스폰 코루틴 참조 (정지용)</summary>
    private Coroutine spawnCoroutine;

    /// <summary>스폰 작동 플래그</summary>
    private bool isSpawning = false;

    // ============================================================
    // 셔플 풀 (스폰 포인트 골고루 분배용)
    // ============================================================

    /// <summary>셔플된 스폰 포인트 인덱스 풀</summary>
    private readonly List<int> shuffledIndexPool = new List<int>();

    /// <summary>풀에서 다음으로 사용할 커서 위치</summary>
    private int poolCursor = 0;

    // ============================================================
    // 이벤트
    // ============================================================

    /// <summary>새 웨이브 시작 시 발화 (int = waveIndex, 0부터)</summary>
    public event System.Action<int> OnWaveStarted;

    /// <summary>웨이브 클리어 시 발화 (int = waveIndex)</summary>
    public event System.Action<int> OnWaveCleared;

    /// <summary>모든 웨이브 종료 시 발화 — 외부 스테이지 클리어 트리거로 사용</summary>
    public event System.Action OnAllWavesCompleted;

    // ============================================================
    // 외부 접근용 프로퍼티
    // ============================================================

    public int AliveEnemyCount => aliveEnemies.Count;
    public bool IsSpawning => isSpawning;
    public int CurrentWaveIndex => currentWaveIndex;
    public int TotalWaves => totalWaves;
    public int TotalEnemiesToSpawn => totalWaves * enemiesPerWave;
    public bool IsAllWavesCompleted => allWavesCompleted;

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    /// <summary>컴포넌트 파괴 시 자동 정리 (씬 전환 등)</summary>
    private void OnDestroy()
    {
        StopSpawning();
    }

    // ============================================================
    // Public Interface — 외부 컨트롤러가 호출
    // ============================================================

    /// <summary>
    /// 스폰 시작 — Stage 컨트롤러가 미션 시작 시 호출
    /// - 런타임 상태 초기화 + 셔플 풀 초기화 + 웨이브 순회 코루틴 시작
    /// </summary>
    public void StartSpawning()
    {
        if (isSpawning)
        {
            Debug.LogWarning("[StageEnemySpawner] 이미 스폰 중입니다.");
            return;
        }

        if (!ValidateSettings()) return;

        // 런타임 상태 초기화
        currentWaveIndex = -1;
        currentWaveKilledCount = 0;
        allWavesCompleted = false;
        isSpawning = true;

        // 스폰 시작 시 풀 셔플 초기화
        ReshuffleSpawnPool();

        spawnCoroutine = StartCoroutine(WaveSpawnRoutine());

        Debug.Log($"[StageEnemySpawner] 스폰 시작 — 총 {totalWaves}웨이브 × {enemiesPerWave}마리 = {TotalEnemiesToSpawn}마리");
    }

    /// <summary>
    /// 스폰 정지 — Stage 클리어 또는 강제 종료 시 호출
    /// 살아있는 적은 그대로 둠 (제거하려면 ClearAllEnemies 호출)
    /// </summary>
    public void StopSpawning()
    {
        if (!isSpawning) return;

        isSpawning = false;

        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        Debug.Log("[StageEnemySpawner] 스폰 정지");
    }

    /// <summary>모든 적 제거 — Stage 클리어 후 정리, 씬 전환 직전 등에 호출</summary>
    public void ClearAllEnemies()
    {
        foreach (Enemy enemy in aliveEnemies)
        {
            if (enemy != null && enemy.gameObject != null)
                Destroy(enemy.gameObject);
        }

        aliveEnemies.Clear();
        UpdateAliveCount();

        Debug.Log("[StageEnemySpawner] 모든 적 제거");
    }

    // ============================================================
    // 웨이브 스폰 흐름 (코루틴)
    // ============================================================

    /// <summary>설정 유효성 검증 — 스폰 시작 전 호출</summary>
    private bool ValidateSettings()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
        {
            Debug.LogError("[StageEnemySpawner] 적 프리팹이 설정되지 않았습니다.");
            return false;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[StageEnemySpawner] 스폰 포인트가 설정되지 않았습니다.");
            return false;
        }

        if (totalWaves <= 0 || enemiesPerWave <= 0)
        {
            Debug.LogError($"[StageEnemySpawner] totalWaves({totalWaves})와 enemiesPerWave({enemiesPerWave})는 1 이상이어야 합니다.");
            return false;
        }

        // WaveComposition 모드면 waves 검증
        if (spawnMode == SpawnMode.WaveComposition)
        {
            if (waves == null || waves.Count == 0)
            {
                Debug.LogError("[StageEnemySpawner] WaveComposition 모드인데 waves가 비어있습니다.");
                return false;
            }
            return true; // 이 모드는 enemyPrefabs/totalWaves 검증 스킵
        }

        return true;
    }

    /// <summary>
    /// 웨이브 순회 메인 코루틴 — 모드에 따라 분기
    /// </summary>
    private IEnumerator WaveSpawnRoutine()
    {
        // 모드별 총 웨이브 수
        int waveCount = (spawnMode == SpawnMode.WaveComposition) ? waves.Count : totalWaves;

        for (int wave = 0; wave < waveCount; wave++)
        {
            if (!isSpawning) yield break;

            // === 웨이브 시작 ===
            currentWaveIndex = wave;
            currentWaveKilledCount = 0;
            OnWaveStarted?.Invoke(wave);

            Debug.Log($"[StageEnemySpawner] === Wave {wave + 1}/{waveCount} 시작 ({spawnMode}) ===");

            // 1. 모드별 스폰
            if (spawnMode == SpawnMode.WaveComposition)
                yield return StartCoroutine(SpawnWaveComposition(waves[wave]));
            else
                yield return StartCoroutine(SpawnWaveEnemies());

            // 2. 전원 처치 대기
            yield return new WaitUntil(() => aliveEnemies.Count == 0 || !isSpawning);
            if (!isSpawning) yield break;

            // === 웨이브 클리어 ===
            OnWaveCleared?.Invoke(wave);
            Debug.Log($"[StageEnemySpawner] === Wave {wave + 1}/{waveCount} 클리어 ===");

            // 3. 마지막 아니면 휴식
            if (wave < waveCount - 1)
                yield return new WaitForSeconds(waveInterval);
        }

        // === 모든 웨이브 종료 ===
        allWavesCompleted = true;
        isSpawning = false;
        OnAllWavesCompleted?.Invoke();

        Debug.Log($"[StageEnemySpawner] 모든 웨이브 종료 — 클리어 트리거 발화");
    }

    /// <summary>
    /// 한 웨이브 분량(enemiesPerWave 마리)을 baseSpawnInterval 간격으로 스폰
    /// - maxAliveEnemies 안전장치 적용 (한 번에 너무 많아지지 않게)
    /// </summary>
    private IEnumerator SpawnWaveEnemies()
    {
        int spawned = 0;

        while (spawned < enemiesPerWave && isSpawning)
        {
            CleanupDeadEnemies();

            // 살아있는 적이 maxAliveEnemies 한도 안이면 스폰 (안전장치)
            // SpawnWaveEnemies 안 — 기존 SpawnOne() 호출을 이렇게
            if (aliveEnemies.Count < maxAliveEnemies)
            {
                // 랜덤 프리팹 뽑아서 스폰
                GameObject randomPrefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
                SpawnOne(randomPrefab);
                spawned++;
            }

            yield return new WaitForSeconds(baseSpawnInterval);
        }
    }

    /// <summary>
    /// 웨이브 구성대로 적 스폰 (WaveComposition 모드)
    /// - 각 entry의 prefab × count만큼 목록 생성 → 셔플 → 순차 스폰
    /// - maxAliveEnemies 안전장치 적용
    /// </summary>
    private IEnumerator SpawnWaveComposition(EnemyWaveData wave)
    {
        // 1. 스폰 목록 빌드 (prefab × count)
        List<GameObject> spawnList = new List<GameObject>();
        foreach (var entry in wave.enemies)
        {
            if (entry.enemyPrefab == null) continue;
            for (int i = 0; i < entry.count; i++)
                spawnList.Add(entry.enemyPrefab);
        }

        // 2. 순서 셔플 (한 종류씩 몰려 나오지 않게)
        for (int i = spawnList.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (spawnList[i], spawnList[j]) = (spawnList[j], spawnList[i]);
        }

        // 3. 순차 스폰
        int spawned = 0;
        while (spawned < spawnList.Count && isSpawning)
        {
            CleanupDeadEnemies();

            if (aliveEnemies.Count < maxAliveEnemies)
            {
                SpawnOne(spawnList[spawned]);
                spawned++;
            }

            yield return new WaitForSeconds(baseSpawnInterval);
        }
    }

    /// <summary>
    /// 적 1마리 스폰 (지정 프리팹)
    /// </summary>
    private void SpawnOne(GameObject prefab)
    {
        if (prefab == null) return;

        // 1. 셔플 풀에서 스폰 포인트
        Transform spawnPoint = GetNextSpawnPoint();
        if (spawnPoint == null) return;

        // 2. 위치 + 랜덤 오프셋
        Vector2 randomCircle = Random.insideUnitCircle * spawnOffsetRadius;
        Vector3 spawnPosition = spawnPoint.position + new Vector3(randomCircle.x, 0, randomCircle.y);

        // 3. 적 생성
        GameObject spawnedObj = Instantiate(prefab, spawnPosition, spawnPoint.rotation);
        Enemy enemy = spawnedObj.GetComponent<Enemy>();

        if (enemy == null)
        {
            Debug.LogError($"[StageEnemySpawner] 프리팹 {prefab.name}에 Enemy 컴포넌트가 없습니다.");
            Destroy(spawnedObj);
            return;
        }

        int effectiveLevel = Mathf.Max(stageLevel, GlobalTimer.CurrentLevel);
        enemy.SetLevel(effectiveLevel);

        // 5. 추적 등록 + 사망 콜백
        aliveEnemies.Add(enemy);
        enemy.SetDeathCallback(() => OnEnemyDied(enemy));

        UpdateAliveCount();
    }

    // ============================================================
    // 셔플 풀 (스폰 포인트 골고루 분배)
    // ============================================================

    /// <summary>
    /// 다음 스폰 포인트 반환 — 풀에서 차례로 꺼냄
    /// 풀 끝까지 갔으면 자동 재셔플 (8개 포인트 사이클 반복)
    /// </summary>
    private Transform GetNextSpawnPoint()
    {
        // 풀이 비었거나 끝까지 갔으면 재셔플
        if (poolCursor >= shuffledIndexPool.Count)
        {
            ReshuffleSpawnPool();
        }

        // 셔플 후에도 풀이 비어있으면 (유효한 스폰 포인트 0개) null 반환
        if (shuffledIndexPool.Count == 0) return null;

        int idx = shuffledIndexPool[poolCursor++];
        return spawnPoints[idx];
    }

    /// <summary>
    /// 스폰 포인트 인덱스를 Fisher-Yates 알고리즘으로 셔플
    /// - null이 아닌 유효한 포인트만 풀에 추가
    /// - 한 사이클 = spawnPoints.Length 회 (모든 포인트 한 번씩 사용 후 재셔플)
    /// </summary>
    private void ReshuffleSpawnPool()
    {
        shuffledIndexPool.Clear();

        // 유효 인덱스만 추가
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] != null)
                shuffledIndexPool.Add(i);
        }

        // Fisher-Yates 셔플 (뒤에서부터 랜덤 스왑)
        for (int i = shuffledIndexPool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = shuffledIndexPool[i];
            shuffledIndexPool[i] = shuffledIndexPool[j];
            shuffledIndexPool[j] = temp;
        }

        poolCursor = 0;
    }

    // ============================================================
    // 사망 처리
    // ============================================================

    /// <summary>적 사망 콜백 — 리스트 정리 + 현 웨이브 처치 카운트 증가</summary>
    private void OnEnemyDied(Enemy deadEnemy)
    {
        aliveEnemies.Remove(deadEnemy);
        currentWaveKilledCount++;
        UpdateAliveCount();
    }

    /// <summary>죽거나 파괴된 적을 리스트에서 정리 (안전 장치)</summary>
    private void CleanupDeadEnemies()
    {
        aliveEnemies.RemoveAll(e => e == null || e.IsDead);
        UpdateAliveCount();
    }

    /// <summary>인스펙터 디버깅용 카운트 업데이트</summary>
    private void UpdateAliveCount()
    {
        aliveCount = aliveEnemies.Count;
    }

    // ============================================================
    // 디버그 — Scene 뷰에서 스폰 포인트 시각화
    // ============================================================

    /// <summary>Scene 뷰에서 모든 스폰 포인트를 노란 구로 표시</summary>
    private void OnDrawGizmosSelected()
    {
        if (spawnPoints == null) return;

        Gizmos.color = Color.yellow;

        foreach (var sp in spawnPoints)
        {
            if (sp == null) continue;
            Gizmos.DrawSphere(sp.position, 0.3f);
        }
    }
}
