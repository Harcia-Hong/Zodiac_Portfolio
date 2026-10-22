using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 몬스터 소환 시스템
/// - BossPhaseController의 페이즈 전환을 구독
/// - 지정 페이즈(summonStartPhase) 진입 시 주기적으로 지정 몬스터 소환
/// - 예: HP 2000(페이즈 1) 도달 → 10초마다 거미 2 + 자폭 1 소환
///
/// [셋업]
///   보스 프리팹에 이 컴포넌트 추가 (BossPhaseController와 같은 오브젝트)
///   소환 몬스터/주기/위치는 인스펙터에서 설정
/// </summary>
[RequireComponent(typeof(BossPhaseController))]
public class BossSummoner : MonoBehaviour
{
    // ============================================================
    // 소환 항목 (몬스터 종류 + 수)
    // ============================================================

    /// <summary>한 종류의 소환 몬스터 + 수</summary>
    [System.Serializable]
    public class SummonEntry
    {
        [Tooltip("소환할 몬스터 프리팹 (Enemy 컴포넌트 필수)")]
        public GameObject enemyPrefab;

        [Tooltip("한 번 소환 시 이 몬스터를 몇 마리")]
        [Min(0)] public int count = 1;
    }

    // ============================================================
    // 인스펙터 설정
    // ============================================================

    [Header("소환 조건")]
    [SerializeField, Tooltip("이 페이즈 인덱스 진입 시 소환 시작 (0=시작부터, 1=HP2000, 2=HP1000)")]
    private int summonStartPhase = 1;

    [Header("소환 몬스터 (종류별 수)")]
    [SerializeField, Tooltip("한 주기마다 소환할 몬스터들 — 각 종류와 수 지정")]
    private List<SummonEntry> summonMonsters = new List<SummonEntry>();

    [Header("소환 주기")]
    [SerializeField, Tooltip("소환 시작 후 첫 소환까지 딜레이 (초)")]
    private float firstSummonDelay = 3f;

    [SerializeField, Tooltip("소환 반복 간격 (초)")]
    private float summonInterval = 10f;

    [Header("소환 위치")]
    [SerializeField, Tooltip("소환 위치들 (비우면 보스 주변 랜덤). 씬에 빈 오브젝트로 배치")]
    private Transform[] summonPoints;

    [SerializeField, Tooltip("소환 포인트 없을 때 보스 주변 소환 반경")]
    private float summonRadius = 4f;

    [Header("안전장치")]
    [SerializeField, Tooltip("소환된 몬스터가 이 수 이상 살아있으면 소환 스킵 (난전 방지)")]
    private int maxSummonedAlive = 6;

    // ============================================================
    // 런타임 상태
    // ============================================================

    private BossPhaseController phaseController;
    private Coroutine summonCoroutine;
    private bool isSummoning = false;

    /// <summary>소환된 몬스터 추적 리스트 (동시 생존 수 제한용)</summary>
    private readonly List<Enemy> summonedEnemies = new List<Enemy>();

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    private void Awake()
    {
        phaseController = GetComponent<BossPhaseController>();
    }

    private void OnEnable()
    {
        if (phaseController != null)
            phaseController.OnPhaseTransitionEnd += OnPhaseChanged;
    }

    private void OnDisable()
    {
        if (phaseController != null)
            phaseController.OnPhaseTransitionEnd -= OnPhaseChanged;

        StopSummoning();
    }

    // ============================================================
    // 페이즈 전환 핸들러
    // ============================================================

    /// <summary>
    /// 페이즈 진입 시 호출 — 지정 페이즈 이상이면 소환 시작
    /// </summary>
    private void OnPhaseChanged(int newPhaseIndex)
    {
        if (newPhaseIndex >= summonStartPhase && !isSummoning)
        {
            StartSummoning();
            Debug.Log($"[BossSummoner] 페이즈 {newPhaseIndex} 진입 — 소환 시작");
        }
    }

    // ============================================================
    // 소환 제어
    // ============================================================

    /// <summary>주기적 소환 시작</summary>
    private void StartSummoning()
    {
        if (isSummoning) return;

        isSummoning = true;
        summonCoroutine = StartCoroutine(SummonRoutine());
    }

    /// <summary>소환 정지 (보스 사망/비활성화 시)</summary>
    private void StopSummoning()
    {
        isSummoning = false;

        if (summonCoroutine != null)
        {
            StopCoroutine(summonCoroutine);
            summonCoroutine = null;
        }
    }

    // ============================================================
    // 소환 루틴
    // ============================================================

    /// <summary>
    /// 주기적 소환 코루틴 — firstSummonDelay 후 summonInterval마다 반복
    /// </summary>
    private IEnumerator SummonRoutine()
    {
        yield return new WaitForSeconds(firstSummonDelay);

        while (isSummoning)
        {
            SummonWave();
            yield return new WaitForSeconds(summonInterval);
        }
    }

    /// <summary>
    /// 한 주기 소환 — 지정 몬스터 전부 소환 (maxSummonedAlive 안전장치)
    /// </summary>
    private void SummonWave()
    {
        CleanupDead();

        // 이미 너무 많으면 이번 주기 스킵
        if (summonedEnemies.Count >= maxSummonedAlive)
        {
            Debug.Log("[BossSummoner] 소환 한도 초과 — 이번 주기 스킵");
            return;
        }

        foreach (var entry in summonMonsters)
        {
            if (entry.enemyPrefab == null) continue;

            for (int i = 0; i < entry.count; i++)
            {
                // 한도 도달 시 중단
                if (summonedEnemies.Count >= maxSummonedAlive) return;
                SummonOne(entry.enemyPrefab);
            }
        }
    }

    /// <summary>몬스터 1마리 소환 + 레벨 적용 + 추적 등록</summary>
    private void SummonOne(GameObject prefab)
    {
        Vector3 pos = GetSummonPosition();
        GameObject obj = Instantiate(prefab, pos, Quaternion.identity);

        Enemy enemy = obj.GetComponent<Enemy>();
        if (enemy == null)
        {
            Debug.LogError($"[BossSummoner] {prefab.name}에 Enemy 컴포넌트가 없습니다.");
            Destroy(obj);
            return;
        }

        // 레벨 적용 (시간 기반 — 다른 적과 동일하게)
        enemy.SetLevel(GlobalTimer.CurrentLevel);

        // 추적 등록 + 사망 콜백
        summonedEnemies.Add(enemy);
        enemy.SetDeathCallback(() => summonedEnemies.Remove(enemy));
    }

    /// <summary>소환 위치 결정 — 포인트 있으면 랜덤, 없으면 보스 주변</summary>
    private Vector3 GetSummonPosition()
    {
        // 지정 포인트 우선
        if (summonPoints != null && summonPoints.Length > 0)
        {
            Transform p = summonPoints[Random.Range(0, summonPoints.Length)];
            if (p != null) return p.position;
        }

        // 없으면 보스 주변 원형 랜덤
        Vector2 circle = Random.insideUnitCircle * summonRadius;
        return transform.position + new Vector3(circle.x, 0f, circle.y);
    }

    /// <summary>죽은 소환수 정리 (생존 수 정확히 유지)</summary>
    private void CleanupDead()
    {
        summonedEnemies.RemoveAll(e => e == null || e.IsDead);
    }
}
