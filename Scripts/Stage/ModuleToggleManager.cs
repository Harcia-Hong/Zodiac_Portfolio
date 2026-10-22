using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

/// ============================================================
/// ModuleToggleManager.cs — Layout 모듈 토글 매니저 (싱글톤)
/// ============================================================
/// 한 Layout 안의 여러 모듈(LayoutModuleGroup) 중 어떤 모듈을
/// 활성/비활성 시킬지 결정하고 동시에 트윈을 시작시키는 매니저.
///
/// 핵심 역할:
///   1. ApplyModuleConfiguration(layoutIndex, activeIds) — 메인 진입점
///   2. 활성화할 모듈은 RaiseUp, 비활성화할 모듈은 SinkDown (교차 페이드)
///   3. Core 모듈은 항상 활성 유지 (자체적으로 토글 무시함)
///   4. 트윈 완료 대기 → PlayerPushSystem 호출 → NavMesh 리베이크
///   5. 변형 시작/종료 이벤트 발행
///
/// 호출 흐름:
///   StageManager.WaveTransitionRoutine
///     → yield return ModuleToggleManager.Instance.ApplyModuleConfiguration(...)
///     ↓
///   1. ArenaMapManager에서 Layout Transform 조회
///   2. Layout의 모든 LayoutModuleGroup 수집 (캐시)
///   3. 각 모듈을 ID 기준으로 활성/비활성 분류
///   4. 동시에 트윈 시작 (교차 페이드)
///   5. transformDuration 대기
///   6. PlayerPushSystem.PushOutOfHiddenAreas() 호출
///   7. NavMesh 리베이크 + OnToggleCompleted 발행
///
/// 의존성:
///   - ArenaMapManager (Layout Transform 조회)
///   - LayoutModuleGroup (모듈 단위 관리)
///   - PlayerPushSystem (밀어내기 — 다음 단계에서 구현)
///   - NavMeshSurface (인스펙터 등록)
/// ============================================================
public class ModuleToggleManager : MonoBehaviour
{
    public static ModuleToggleManager Instance { get; private set; }

    // ============================================================
    // 인스펙터 설정
    // ============================================================

    [Header("전환 시간")]
    [Tooltip("모듈 솟음/가라앉음 트윈 지속 시간 (초)")]
    [SerializeField] private float transformDuration = 0.6f;

    [Tooltip("토글 완료 후 NavMesh 리베이크까지 추가 여유 시간 (초).\n" +
             "트윈 완전 종료 보장용")]
    [SerializeField] private float postTransformBuffer = 0.1f;

    [Header("동시성 정책")]
    [Tooltip("이미 토글 진행 중일 때 새 호출 처리 방식.\n" +
             "true: 기존 중단하고 새로 시작 / false: 새 호출 무시")]
    [SerializeField] private bool allowInterrupt = false;

    [Header("NavMesh")]
    [Tooltip("토글 후 리베이크할 NavMeshSurface (ArenaMapManager와 동일한 것 권장)")]
    [SerializeField] private NavMeshSurface navMeshSurface;

    [Header("디버깅용 현재 상태")]
    [SerializeField] private bool isToggling = false;
    [SerializeField] private int lastProcessedLayoutIndex = -1;

    // ============================================================
    // 런타임 상태
    // ============================================================

    /// <summary>Layout 인덱스별 모듈 그룹 캐시 (매번 GetComponentsInChildren 방지)</summary>
    private readonly Dictionary<int, LayoutModuleGroup[]> moduleGroupCache
        = new Dictionary<int, LayoutModuleGroup[]>();

    /// <summary>현재 진행 중인 토글 코루틴</summary>
    private Coroutine activeToggleCoroutine;

    // ============================================================
    // 이벤트
    // ============================================================

    /// <summary>토글 시작 시 발행 (인자: layoutIndex)</summary>
    public event System.Action<int> OnToggleStarted;

    /// <summary>토글 완료 시 발행 (인자: layoutIndex)</summary>
    public event System.Action<int> OnToggleCompleted;

    /// <summary>현재 토글이 진행 중인지</summary>
    public bool IsToggling => isToggling;

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
    /// 지정 Layout의 모듈을 주어진 ID 셋에 따라 토글한다.
    /// activeModuleIds에 포함된 ID = 활성, 그 외 = 비활성.
    /// Core 모듈은 항상 활성 유지 (ID 명시 불필요).
    ///
    /// activeModuleIds가 null이거나 빈 배열이면:
    ///   → 일반 모듈 모두 비활성화 (Core만 남음)
    ///
    /// StageManager가 yield return으로 완료까지 대기 가능.
    /// </summary>
    /// <returns>토글 코루틴. 무시된 경우 null</returns>
    public Coroutine ApplyModuleConfiguration(int layoutIndex, int[] activeModuleIds)
    {
        // 동시성 처리
        if (isToggling)
        {
            if (allowInterrupt && activeToggleCoroutine != null)
            {
                StopCoroutine(activeToggleCoroutine);
                Debug.Log("[ModuleToggleManager] 기존 토글 중단 후 새 토글 시작");
            }
            else
            {
                Debug.LogWarning("[ModuleToggleManager] 이미 토글 진행 중 — 새 호출 무시");
                return null;
            }
        }

        activeToggleCoroutine = StartCoroutine(
            ApplyConfigurationRoutine(layoutIndex, activeModuleIds));
        return activeToggleCoroutine;
    }

    /// <summary>
    /// HashSet 버전 오버로드 — Contains 검사가 빈번하면 성능 유리.
    /// 호출 측에서 HashSet을 이미 보유 중이면 이 버전 사용 권장.
    /// </summary>
    public Coroutine ApplyModuleConfiguration(int layoutIndex, HashSet<int> activeModuleIds)
    {
        int[] asArray = activeModuleIds != null
            ? new List<int>(activeModuleIds).ToArray()
            : null;

        return ApplyModuleConfiguration(layoutIndex, asArray);
    }

    #endregion

    // ============================================================
    // 핵심 토글 루틴
    // ============================================================

    #region Toggle Routine

    /// <summary>토글 메인 루틴 — 모듈 분류 → 트윈 동시 시작 → 대기 → 후처리</summary>
    private IEnumerator ApplyConfigurationRoutine(int layoutIndex, int[] activeModuleIds)
    {
        isToggling = true;
        lastProcessedLayoutIndex = layoutIndex;
        OnToggleStarted?.Invoke(layoutIndex);

        // 1. Layout의 모듈 수집
        LayoutModuleGroup[] modules = GetCachedModules(layoutIndex);
        if (modules == null || modules.Length == 0)
        {
            Debug.LogWarning($"[ModuleToggleManager] Layout_{layoutIndex}에 모듈 없음 — 토글 스킵");
            FinishToggle(layoutIndex);
            yield break;
        }

        // 2. 활성 ID 셋을 HashSet으로 변환 (빠른 Contains 검사)
        HashSet<int> activeSet = BuildActiveSet(activeModuleIds);

        // 3. 모듈을 분류해서 동시에 토글 시작
        int activatedCount = 0;
        int deactivatedCount = 0;

        foreach (LayoutModuleGroup module in modules)
        {
            if (module == null) continue;

            // Core는 항상 활성 — 토글 호출 자체를 안 함
            if (module.IsCore) continue;

            bool shouldBeActive = activeSet.Contains(module.ModuleId);

            if (shouldBeActive && !module.IsActive)
            {
                module.ActivateModule(transformDuration);
                activatedCount++;
            }
            else if (!shouldBeActive && module.IsActive)
            {
                module.DeactivateModule(transformDuration);
                deactivatedCount++;
            }
        }

        Debug.Log($"[ModuleToggleManager] Layout_{layoutIndex}: " +
                  $"활성화 {activatedCount}개 / 비활성화 {deactivatedCount}개");

        // 4. 변화가 없었으면 즉시 종료 (트윈 대기 불필요)
        if (activatedCount == 0 && deactivatedCount == 0)
        {
            FinishToggle(layoutIndex);
            yield break;
        }

        // 5. 트윈 완료 + 안전 버퍼 대기
        yield return new WaitForSeconds(transformDuration + postTransformBuffer);

        // 6. 밀어내기 — 가라앉은 모듈 위에 있는 플레이어/적 처리
        if (deactivatedCount > 0 && PlayerPushSystem.Instance != null)
            PlayerPushSystem.Instance.PushOutOfHiddenAreas(layoutIndex);

        // 7. NavMesh 리베이크
        RebakeNavMesh();

        FinishToggle(layoutIndex);
    }

    /// <summary>토글 루틴 종료 처리</summary>
    private void FinishToggle(int layoutIndex)
    {
        isToggling = false;
        activeToggleCoroutine = null;
        OnToggleCompleted?.Invoke(layoutIndex);
    }

    /// <summary>활성 ID 배열을 HashSet으로 변환 (null/empty 안전 처리)</summary>
    private HashSet<int> BuildActiveSet(int[] activeModuleIds)
    {
        HashSet<int> set = new HashSet<int>();
        if (activeModuleIds == null) return set;

        foreach (int id in activeModuleIds)
            set.Add(id);

        return set;
    }

    #endregion

    // ============================================================
    // 모듈 조회 (캐시)
    // ============================================================

    #region Module Lookup

    /// <summary>Layout의 모든 LayoutModuleGroup 반환 (캐시 우선, 미스 시 GetComponentsInChildren)</summary>
    private LayoutModuleGroup[] GetCachedModules(int layoutIndex)
    {
        if (moduleGroupCache.TryGetValue(layoutIndex, out LayoutModuleGroup[] cached))
        {
            // 캐시 항목이 살아있는지 검증 (Destroy로 사라졌을 수 있음)
            if (cached != null && IsCacheValid(cached))
                return cached;

            moduleGroupCache.Remove(layoutIndex);
        }

        if (ArenaMapManager.Instance == null) return null;

        Transform layoutTf = ArenaMapManager.Instance.GetLayoutTransform(layoutIndex);
        if (layoutTf == null) return null;

        // includeInactive: true — 가라앉아서 비활성된 모듈도 포함해야 다시 활성화 가능
        LayoutModuleGroup[] modules = layoutTf.GetComponentsInChildren<LayoutModuleGroup>(true);

        if (modules.Length > 0)
            moduleGroupCache[layoutIndex] = modules;

        return modules;
    }

    /// <summary>캐시된 배열의 항목들이 모두 유효한지 검증</summary>
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
    // 외부 인터페이스
    // ============================================================

    #region Public Interface

    /// <summary>Layout의 모듈 ID 목록 반환 (디버깅, UI 등에 사용)</summary>
    public int[] GetModuleIds(int layoutIndex)
    {
        LayoutModuleGroup[] modules = GetCachedModules(layoutIndex);
        if (modules == null) return System.Array.Empty<int>();

        List<int> ids = new List<int>();
        foreach (LayoutModuleGroup m in modules)
        {
            if (m == null) continue;
            if (m.IsCore) continue;
            ids.Add(m.ModuleId);
        }

        return ids.ToArray();
    }

    /// <summary>Layout의 현재 활성 모듈 ID 목록 반환</summary>
    public int[] GetCurrentActiveModuleIds(int layoutIndex)
    {
        LayoutModuleGroup[] modules = GetCachedModules(layoutIndex);
        if (modules == null) return System.Array.Empty<int>();

        List<int> ids = new List<int>();
        foreach (LayoutModuleGroup m in modules)
        {
            if (m == null) continue;
            if (m.IsCore) continue;
            if (m.IsActive) ids.Add(m.ModuleId);
        }

        return ids.ToArray();
    }

    /// <summary>특정 Layout에서 주어진 좌표가 활성 모듈 위에 있는지 검사</summary>
    public bool IsPointAboveAnyActiveModule(int layoutIndex, Vector3 worldPoint)
    {
        LayoutModuleGroup[] modules = GetCachedModules(layoutIndex);
        if (modules == null) return false;

        foreach (LayoutModuleGroup m in modules)
        {
            if (m == null) continue;
            if (!m.IsActive) continue;
            if (m.IsPointAboveModule(worldPoint)) return true;
        }

        return false;
    }

    /// <summary>특정 Layout의 모든 모듈 그룹 반환 (PlayerPushSystem이 사용)</summary>
    public LayoutModuleGroup[] GetModulesOfLayout(int layoutIndex)
    {
        return GetCachedModules(layoutIndex);
    }

    /// <summary>캐시 초기화 (씬 재시작 등)</summary>
    public void ClearCache()
    {
        moduleGroupCache.Clear();
    }

    /// <summary>NavMesh 리베이크 — 외부에서도 호출 가능 (밀어내기 후 등)</summary>
    public void RebakeNavMesh()
    {
        if (navMeshSurface != null)
            navMeshSurface.BuildNavMesh();
        else
            Debug.LogWarning("[ModuleToggleManager] NavMeshSurface가 설정되지 않았습니다.");
    }

    #endregion

    // ============================================================
    // 디버그
    // ============================================================

    #region Debug

    [ContextMenu("Test - Layout 0 모듈 ID 목록 출력")]
    private void DebugListModules0()
    {
        int[] ids = GetModuleIds(0);
        Debug.Log($"[ModuleToggleManager] Layout_0 모듈 ID: [{string.Join(", ", ids)}]");
    }

    [ContextMenu("Test - Layout 0 활성 모듈 출력")]
    private void DebugListActive0()
    {
        int[] ids = GetCurrentActiveModuleIds(0);
        Debug.Log($"[ModuleToggleManager] Layout_0 활성 모듈: [{string.Join(", ", ids)}]");
    }

    [ContextMenu("Test - Layout 0 모듈 0,1만 활성")]
    private void DebugApply0_01()
    {
        ApplyModuleConfiguration(0, new int[] { 0, 1 });
    }

    [ContextMenu("Test - Layout 0 모듈 2,3만 활성")]
    private void DebugApply0_23()
    {
        ApplyModuleConfiguration(0, new int[] { 2, 3 });
    }

    [ContextMenu("Test - Layout 0 모든 일반 모듈 비활성 (Core만)")]
    private void DebugDisableAll0()
    {
        ApplyModuleConfiguration(0, new int[] { });
    }

    #endregion
}
