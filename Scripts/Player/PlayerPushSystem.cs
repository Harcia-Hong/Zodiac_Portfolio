using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// ============================================================
/// PlayerPushSystem.cs — 변형 후 못 서는 위치의 플레이어/적 밀어내기
/// ============================================================
/// 모듈 토글 후, 가라앉은 모듈 위에 있던 플레이어와 적을
/// 가장 가까운 활성 모듈의 표면으로 밀어내는 시스템.
///
/// 처리 방식:
///   - 플레이어: Rigidbody에 추진력 부여 (입력 잠그지 않음 — 이동 자유)
///   - 적     : NavMeshAgent.Warp로 즉시 이동 (NavMesh 밖에선 동작 불가하므로)
///
/// 핵심 역할:
///   1. PushOutOfHiddenAreas(layoutIndex) — 메인 진입점
///   2. Layout 안의 모든 활성 모듈에서 안전 위치 후보 수집
///   3. 플레이어/적 위치 검사 → 어디 모듈 위도 아니면 → 밀어내기
///   4. 이미 안전한 대상은 건드리지 않음
///
/// 호출 흐름:
///   ModuleToggleManager.ApplyConfigurationRoutine
///     → 토글 + 트윈 완료 후
///     → if 가라앉은 모듈이 있으면 PlayerPushSystem.PushOutOfHiddenAreas() 호출
///
/// 의존성:
///   - ModuleToggleManager (Layout 모듈 조회)
///   - LayoutModuleGroup (점유 검사 + 안전 위치 조회)
///   - Player 태그가 있는 GameObject (Rigidbody 보유)
///   - Enemy 컴포넌트 (NavMeshAgent 보유)
/// ============================================================
public class PlayerPushSystem : MonoBehaviour
{
    public static PlayerPushSystem Instance { get; private set; }

    // ============================================================
    // 인스펙터 설정
    // ============================================================

    [Header("플레이어 추진력")]
    [Tooltip("플레이어를 안전 위치로 밀어낼 때 부여할 추진 속도 (m/s).\n" +
             "중간 강도 = 8~12 정도 권장")]
    [SerializeField] private float playerPushSpeed = 10f;

    [Tooltip("플레이어 위쪽 점프력 — 가라앉은 자리에서 살짝 띄워서 추진.\n" +
             "0이면 수평 추진만")]
    [SerializeField] private float playerPushUpSpeed = 3f;

    [Tooltip("플레이어가 너무 가깝게 안전 위치 위에 있을 때 (이 거리 이내) 밀어내기 스킵")]
    [SerializeField] private float playerPushMinDistance = 0.5f;

    [Header("적 워프")]
    [Tooltip("NavMeshAgent.Warp로 적을 즉시 이동시킬지.\n" +
             "false면 적은 처리 안 함 (디버깅용)")]
    [SerializeField] private bool warpEnemies = true;

    [Tooltip("적 워프 시 안전 위치 위로 살짝 띄울 높이 (NavMesh 정착용)")]
    [SerializeField] private float enemyWarpHeightOffset = 0.5f;

    [Header("디버깅")]
    [SerializeField] private bool verboseLog = true;

    // ============================================================
    // 캐시
    // ============================================================

    /// <summary>플레이어 Rigidbody 캐시 (Player 태그로 검색 후 보관)</summary>
    private Rigidbody cachedPlayerRb;

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

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    #endregion

    // ============================================================
    // 메인 진입점
    // ============================================================

    #region Public Entry Points

    /// <summary>
    /// 지정 Layout에서 가라앉은 모듈 위에 있는 플레이어/적을
    /// 가장 가까운 안전 위치(활성 모듈 표면)로 밀어낸다.
    /// </summary>
    public void PushOutOfHiddenAreas(int layoutIndex)
    {
        // 1. Layout의 모든 모듈 조회
        LayoutModuleGroup[] modules = ResolveLayoutModules(layoutIndex);
        if (modules == null || modules.Length == 0)
        {
            if (verboseLog)
                Debug.LogWarning($"[PlayerPushSystem] Layout_{layoutIndex} 모듈 조회 실패 — 밀어내기 스킵");
            return;
        }

        // 2. 플레이어 처리
        ProcessPlayer(modules);

        // 3. 적 처리
        ProcessEnemies(modules);
    }

    #endregion

    // ============================================================
    // 플레이어 처리
    // ============================================================

    #region Player Processing

    /// <summary>플레이어를 안전 위치로 밀어내는 처리</summary>
    private void ProcessPlayer(LayoutModuleGroup[] modules)
    {
        Rigidbody playerRb = ResolvePlayerRigidbody();
        if (playerRb == null) return;

        Vector3 playerPos = playerRb.position;

        // 이미 활성 모듈 위에 있으면 처리 안 함
        if (IsPointAboveAnyActiveModule(playerPos, modules))
            return;

        // 가장 가까운 안전 위치 찾기
        Vector3? safePoint = FindNearestSafePoint(playerPos, modules);
        if (!safePoint.HasValue)
        {
            if (verboseLog)
                Debug.LogWarning("[PlayerPushSystem] 안전 위치 없음 — 플레이어 밀어내기 실패");
            return;
        }

        // 너무 가까우면 스킵 (떨림 방지)
        Vector3 toSafe = safePoint.Value - playerPos;
        toSafe.y = 0f;

        if (toSafe.magnitude < playerPushMinDistance)
            return;

        // 추진력 부여 — 입력은 잠그지 않음 (요구사항)
        ApplyPlayerPushVelocity(playerRb, toSafe);

        if (verboseLog)
            Debug.Log($"[PlayerPushSystem] 플레이어 밀어내기 → {safePoint.Value}");
    }

    /// <summary>
    /// 플레이어 Rigidbody에 추진 속도 적용
    /// 수평 방향 + 약간의 위쪽 점프력으로 가라앉은 자리에서 띄워올림
    /// </summary>
    private void ApplyPlayerPushVelocity(Rigidbody rb, Vector3 horizontalDir)
    {
        Vector3 dir = horizontalDir.normalized;

        Vector3 newVelocity = dir * playerPushSpeed;
        newVelocity.y = playerPushUpSpeed;

        // PlayerController가 매 FixedUpdate에서 linearVelocity를 덮어쓰므로
        // AddForce(VelocityChange)로 누적 추진력을 줘서 한 프레임에 즉각 반영
        // 이후엔 PlayerController의 일반 이동에 자연스럽게 통합됨
        rb.AddForce(newVelocity, ForceMode.VelocityChange);
    }

    /// <summary>플레이어 Rigidbody 조회 (캐시 우선)</summary>
    private Rigidbody ResolvePlayerRigidbody()
    {
        if (cachedPlayerRb != null) return cachedPlayerRb;

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return null;

        cachedPlayerRb = player.GetComponent<Rigidbody>();
        return cachedPlayerRb;
    }

    #endregion

    // ============================================================
    // 적 처리
    // ============================================================

    #region Enemy Processing

    /// <summary>모든 적을 검사해서 못 서는 위치에 있으면 워프</summary>
    private void ProcessEnemies(LayoutModuleGroup[] modules)
    {
        if (!warpEnemies) return;

        // 씬에서 모든 활성 Enemy 조회
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        if (enemies == null || enemies.Length == 0) return;

        int warpedCount = 0;

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null) continue;
            if (!enemy.gameObject.activeInHierarchy) continue;

            if (TryWarpEnemy(enemy, modules))
                warpedCount++;
        }

        if (verboseLog && warpedCount > 0)
            Debug.Log($"[PlayerPushSystem] 적 워프 완료: {warpedCount}마리");
    }

    /// <summary>적 1마리 워프 처리. 처리됐으면 true 반환</summary>
    private bool TryWarpEnemy(Enemy enemy, LayoutModuleGroup[] modules)
    {
        Vector3 enemyPos = enemy.transform.position;

        // 이미 활성 모듈 위면 처리 안 함
        if (IsPointAboveAnyActiveModule(enemyPos, modules))
            return false;

        // 안전 위치 찾기
        Vector3? safePoint = FindNearestSafePoint(enemyPos, modules);
        if (!safePoint.HasValue) return false;

        // NavMeshAgent.Warp — NavMesh 위로 텔레포트
        NavMeshAgent agent = enemy.nav;
        Vector3 targetPos = safePoint.Value + Vector3.up * enemyWarpHeightOffset;

        if (agent != null && agent.enabled)
        {
            agent.Warp(targetPos);
        }
        else
        {
            // NavMeshAgent가 없거나 비활성이면 그냥 transform 이동
            enemy.transform.position = targetPos;
        }

        return true;
    }

    #endregion

    // ============================================================
    // 안전 위치 검색
    // ============================================================

    #region Safe Point Search

    /// <summary>주어진 좌표가 활성 모듈 중 하나의 블록 위에 있는지</summary>
    private bool IsPointAboveAnyActiveModule(Vector3 worldPoint, LayoutModuleGroup[] modules)
    {
        foreach (LayoutModuleGroup module in modules)
        {
            if (module == null) continue;
            if (!module.IsActive && !module.IsCore) continue;
            if (module.IsPointAboveModule(worldPoint)) return true;
        }
        return false;
    }

    /// <summary>
    /// 모든 활성 모듈을 순회하며 worldPoint와 가장 가까운 표면 좌표 찾기.
    /// Core 모듈도 후보에 포함 (Core는 항상 활성).
    /// </summary>
    private Vector3? FindNearestSafePoint(Vector3 worldPoint, LayoutModuleGroup[] modules)
    {
        Vector3? best = null;
        float bestSqr = float.PositiveInfinity;

        foreach (LayoutModuleGroup module in modules)
        {
            if (module == null) continue;
            if (!module.IsActive && !module.IsCore) continue;

            Vector3? candidate = module.GetNearestSurfacePoint(worldPoint);
            if (!candidate.HasValue) continue;

            float sqr = (candidate.Value - worldPoint).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = candidate;
            }
        }

        return best;
    }

    #endregion

    // ============================================================
    // Layout 모듈 조회
    // ============================================================

    #region Module Resolution

    /// <summary>
    /// Layout의 모든 모듈을 조회.
    /// ModuleToggleManager의 캐시를 재활용 (있으면) 또는 직접 GetComponentsInChildren.
    /// </summary>
    private LayoutModuleGroup[] ResolveLayoutModules(int layoutIndex)
    {
        // 1순위: ModuleToggleManager 캐시 재사용
        if (ModuleToggleManager.Instance != null)
        {
            LayoutModuleGroup[] cached = ModuleToggleManager.Instance.GetModulesOfLayout(layoutIndex);
            if (cached != null && cached.Length > 0) return cached;
        }

        // 2순위: ArenaMapManager에서 직접 조회
        if (ArenaMapManager.Instance == null) return null;

        Transform layoutTf = ArenaMapManager.Instance.GetLayoutTransform(layoutIndex);
        if (layoutTf == null) return null;

        return layoutTf.GetComponentsInChildren<LayoutModuleGroup>(true);
    }

    #endregion

    // ============================================================
    // 디버그
    // ============================================================

    #region Debug

    [ContextMenu("Test - Layout 0 밀어내기 강제 호출")]
    private void DebugPush0() => PushOutOfHiddenAreas(0);

    [ContextMenu("Test - 플레이어 위치에서 안전 위치 검색")]
    private void DebugFindSafePoint()
    {
        Rigidbody rb = ResolvePlayerRigidbody();
        if (rb == null) { Debug.Log("플레이어 없음"); return; }

        LayoutModuleGroup[] modules = ResolveLayoutModules(0);
        if (modules == null) { Debug.Log("Layout_0 모듈 없음"); return; }

        Vector3? p = FindNearestSafePoint(rb.position, modules);
        Debug.Log($"플레이어 위치: {rb.position} / 가장 가까운 안전 위치: {p}");
    }

    #endregion
}
