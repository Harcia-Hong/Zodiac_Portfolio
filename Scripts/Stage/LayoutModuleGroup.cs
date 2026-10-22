using System.Collections.Generic;
using UnityEngine;

/// ============================================================
/// LayoutModuleGroup.cs — Layout 안의 모듈 단위 그룹
/// ============================================================
/// 한 Layout(예: Layout_0)은 "Core + Module 여러 개"로 구성됨.
/// 이 컴포넌트는 한 모듈의 루트에 부착되어, 그 모듈에 속한
/// 블록들과 스폰 포인트를 관리하고 모듈 단위 활성/비활성을 처리한다.
///
/// 핵심 역할:
///   1. 모듈 ID 보유 (WaveData에서 명시할 식별자)
///   2. 자식 BlockUnit 자동 수집 + 일괄 부착 도구
///   3. 모듈 활성/비활성 토글 (모든 BlockUnit 동시 RaiseUp/SinkDown)
///   4. 스폰 포인트 제공 (활성일 때만)
///   5. 점유 검사 (월드 좌표가 이 모듈 블록 위에 있는지)
///
/// 씬 구조 예시:
///   Layout_0
///     ├ Core (LayoutModuleGroup, ID = -1, 항상 활성)
///     │   ├ Cube들 (변형 안 함, BlockUnit 미부착)
///     │   └ SpawnPoints
///     ├ Module_0 (LayoutModuleGroup, ID = 0)
///     │   ├ Cube들 (BlockUnit 부착)
///     │   └ SpawnPoints
///     ├ Module_1 (LayoutModuleGroup, ID = 1)
///     └ ...
///
/// 호출 흐름:
///   ModuleToggleManager
///     → 활성 모듈 ID 셋 결정 (WaveData 기반)
///     → 각 모듈에 ActivateModule() / DeactivateModule() 호출
///     → 변형 완료 후 PlayerPushSystem이 IsPointAboveModule()로 검사
/// ============================================================
public class LayoutModuleGroup : MonoBehaviour
{
    // ============================================================
    // 모듈 식별
    // ============================================================

    [Header("모듈 식별")]
    [Tooltip("이 모듈의 고유 ID. 같은 Layout 안에서 중복되면 안 됨.\n" +
             "Core 모듈은 -1로 설정 (항상 활성, 토글 대상 아님).\n" +
             "일반 모듈은 0, 1, 2, ...")]
    [SerializeField] private int moduleId = 0;

    [Tooltip("Core 모듈 여부. true면 활성/비활성 토글 무시되고 항상 활성 유지")]
    [SerializeField] private bool isCore = false;

    // ============================================================
    // 블록 수집
    // ============================================================

    [Header("블록 수집")]
    [Tooltip("이 모듈에 속한 변형 가능 블록 목록.\n" +
             "비워두면 Awake에서 자식 BlockUnit을 자동 수집함.\n" +
             "Core 모듈은 변형 안 하므로 비워두면 됨")]
    [SerializeField] private BlockUnit[] blocks;

    [Tooltip("blocks 배열이 비어있을 때 자식 BlockUnit을 자동으로 찾을지")]
    [SerializeField] private bool autoCollectChildren = true;

    [Tooltip("자동 수집 시 비활성 자식도 포함할지")]
    [SerializeField] private bool includeInactive = true;

    // ============================================================
    // 스폰 포인트
    // ============================================================

    [Header("몬스터 스폰 포인트")]
    [Tooltip("이 모듈에서 몬스터가 스폰될 위치들.\n" +
             "비워두면 Awake에서 자식 중 'SpawnPoint' 이름이 들어간 것들을 자동 수집")]
    [SerializeField] private Transform[] enemySpawnPoints;

    [Tooltip("스폰 포인트 자동 수집 활성화 여부")]
    [SerializeField] private bool autoCollectSpawnPoints = true;

    [Tooltip("자동 수집 시 이 키워드가 포함된 자식만 스폰 포인트로 인식 (대소문자 무시)")]
    [SerializeField] private string spawnPointNameKeyword = "SpawnPoint";

    // ============================================================
    // 일괄 부착 도구 (에디터)
    // ============================================================

    [Header("일괄 부착 도구 (에디터 전용)")]
    [Tooltip("이 prefix로 시작하는 자식에만 BlockUnit을 일괄 부착.\n" +
             "비워두면 모든 자식 대상 (위험)")]
    [SerializeField] private string attachNamePrefix = "Cube";

    [Tooltip("Trigger 콜라이더가 부착된 자식은 일괄 부착에서 제외")]
    [SerializeField] private bool excludeTriggers = true;

    [Tooltip("자식의 자식까지 재귀적으로 검색할지")]
    [SerializeField] private bool searchRecursive = false;

    // ============================================================
    // 런타임 상태
    // ============================================================

    [Header("디버깅용 현재 상태")]
    [SerializeField] private bool isActive = true;
    [SerializeField] private int registeredBlockCount;
    [SerializeField] private int registeredSpawnPointCount;

    /// <summary>변형 후보 블록 임시 작업 리스트 (GC 감소용)</summary>
    private readonly List<BlockUnit> workBuffer = new List<BlockUnit>();

    // ============================================================
    // 외부 접근용 프로퍼티
    // ============================================================

    public int ModuleId => moduleId;
    public bool IsCore => isCore;
    public bool IsActive => isActive;
    public int BlockCount => blocks != null ? blocks.Length : 0;
    public int SpawnPointCount => enemySpawnPoints != null ? enemySpawnPoints.Length : 0;
    public bool HasBlocks => BlockCount > 0;

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    #region Unity Lifecycle

    private void Awake()
    {
        // BlockUnit 자동 수집
        if ((blocks == null || blocks.Length == 0) && autoCollectChildren)
            CollectChildBlocks();

        // 스폰 포인트 자동 수집
        if ((enemySpawnPoints == null || enemySpawnPoints.Length == 0) && autoCollectSpawnPoints)
            CollectSpawnPoints();

        registeredBlockCount = blocks != null ? blocks.Length : 0;
        registeredSpawnPointCount = enemySpawnPoints != null ? enemySpawnPoints.Length : 0;

        // Core는 항상 활성으로 시작
        isActive = true;
    }

    /// <summary>자식에서 BlockUnit 자동 수집</summary>
    private void CollectChildBlocks()
    {
        BlockUnit[] found = GetComponentsInChildren<BlockUnit>(includeInactive);
        blocks = found;
    }

    /// <summary>자식에서 스폰 포인트 자동 수집 (이름 키워드 매칭)</summary>
    private void CollectSpawnPoints()
    {
        if (string.IsNullOrEmpty(spawnPointNameKeyword))
        {
            enemySpawnPoints = new Transform[0];
            return;
        }

        List<Transform> found = new List<Transform>();
        Transform[] allChildren = GetComponentsInChildren<Transform>(true);

        foreach (Transform child in allChildren)
        {
            if (child == transform) continue;

            if (child.name.IndexOf(spawnPointNameKeyword,
                System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                found.Add(child);
            }
        }

        enemySpawnPoints = found.ToArray();
    }

    #endregion

    // ============================================================
    // 모듈 활성/비활성 토글 (핵심)
    // ============================================================

    #region Module Activation

    /// <summary>
    /// 이 모듈을 활성화 — 모든 BlockUnit을 RaiseUp.
    /// Core 모듈은 호출이 무시되며 항상 활성 상태 유지.
    /// </summary>
    /// <param name="duration">트윈 지속 시간 (0 이하면 BlockUnit 기본값 사용)</param>
    public void ActivateModule(float duration = 0f)
    {
        if (isCore) return;
        if (isActive) return;

        isActive = true;

        if (blocks == null) return;

        foreach (BlockUnit block in blocks)
        {
            if (block != null)
                block.RaiseUp(duration);
        }
    }

    /// <summary>
    /// 이 모듈을 비활성화 — 모든 BlockUnit을 SinkDown.
    /// Core 모듈은 호출이 무시됨.
    /// </summary>
    public void DeactivateModule(float duration = 0f)
    {
        if (isCore) return;
        if (!isActive) return;

        isActive = false;

        if (blocks == null) return;

        foreach (BlockUnit block in blocks)
        {
            if (block != null)
                block.SinkDown(duration);
        }
    }

    /// <summary>활성/비활성을 즉시 강제 적용 (애니메이션 없음, 초기 상태 셋업용)</summary>
    public void SetActiveImmediate(bool active)
    {
        if (isCore)
        {
            isActive = true;
            return;
        }

        isActive = active;

        if (blocks == null) return;

        foreach (BlockUnit block in blocks)
        {
            if (block == null) continue;

            if (active) block.SetVisibleImmediate();
            else block.SetHiddenImmediate();
        }
    }

    #endregion

    // ============================================================
    // 스폰 포인트 API
    // ============================================================

    #region Spawn Point Queries

    /// <summary>
    /// 이 모듈의 스폰 포인트 배열 반환.
    /// 비활성 상태면 빈 배열 반환 (가라앉은 모듈에서 적이 솟아나지 않도록).
    /// </summary>
    public Transform[] GetActiveSpawnPoints()
    {
        if (!isActive) return System.Array.Empty<Transform>();
        if (enemySpawnPoints == null) return System.Array.Empty<Transform>();
        return enemySpawnPoints;
    }

    /// <summary>활성 스폰 포인트 중 랜덤 1개 반환. 없으면 null</summary>
    public Transform GetRandomActiveSpawnPoint()
    {
        Transform[] points = GetActiveSpawnPoints();
        if (points.Length == 0) return null;

        return points[Random.Range(0, points.Length)];
    }

    /// <summary>활성 스폰 포인트 개수</summary>
    public int GetActiveSpawnPointCount()
    {
        return GetActiveSpawnPoints().Length;
    }

    #endregion

    // ============================================================
    // 점유 검사 (밀어내기 시스템에서 사용)
    // ============================================================

    #region Occupancy Check

    /// <summary>
    /// 주어진 월드 좌표(XZ 평면 기준)가 이 모듈의 Visible 블록 위에 있는지 검사.
    /// PlayerPushSystem이 "플레이어가 가라앉은 모듈 위에 있는지" 판단할 때 사용.
    /// </summary>
    public bool IsPointAboveModule(Vector3 worldPoint)
    {
        if (blocks == null) return false;

        foreach (BlockUnit block in blocks)
        {
            if (block == null) continue;
            if (!block.IsVisible) continue;
            if (block.IsPointAboveXZ(worldPoint)) return true;
        }

        return false;
    }

    /// <summary>
    /// 주어진 월드 좌표가 이 모듈의 어느 블록 위에 있는지 반환.
    /// 여러 블록과 겹치면 가장 높은 표면을 가진 것 선택.
    /// </summary>
    public BlockUnit GetBlockAbovePoint(Vector3 worldPoint)
    {
        if (blocks == null) return null;

        BlockUnit best = null;
        float bestSurfaceY = float.NegativeInfinity;

        foreach (BlockUnit block in blocks)
        {
            if (block == null) continue;
            if (!block.IsVisible) continue;
            if (!block.IsPointAboveXZ(worldPoint)) continue;

            if (block.SurfaceY > bestSurfaceY)
            {
                bestSurfaceY = block.SurfaceY;
                best = block;
            }
        }

        return best;
    }

    /// <summary>이 모듈의 모든 Visible 블록 중 worldPoint와 가장 가까운 표면 위치 반환</summary>
    public Vector3? GetNearestSurfacePoint(Vector3 worldPoint)
    {
        if (!isActive) return null;
        if (blocks == null) return null;

        float bestSqr = float.PositiveInfinity;
        Vector3? bestPoint = null;

        foreach (BlockUnit block in blocks)
        {
            if (block == null) continue;
            if (!block.IsVisible) continue;

            // 블록 표면 중심 좌표 (Visible 상태 기준)
            Vector3 surfaceCenter = block.OriginalPosition;
            surfaceCenter.y = block.SurfaceY;

            float sqr = (surfaceCenter - worldPoint).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                bestPoint = surfaceCenter;
            }
        }

        return bestPoint;
    }

    #endregion

    // ============================================================
    // 일괄 도구 (에디터 전용)
    // ============================================================

    #region Editor Tools

    [ContextMenu("자식에 BlockUnit 일괄 부착")]
    private void AttachBlockUnitsToChildren()
    {
        int added = 0;
        int skipped = 0;

        Transform[] candidates = searchRecursive
            ? GetComponentsInChildren<Transform>(true)
            : CollectDirectChildren();

        foreach (Transform child in candidates)
        {
            if (child == transform) continue;

            // 이름 필터
            if (!string.IsNullOrEmpty(attachNamePrefix)
                && !child.name.StartsWith(attachNamePrefix,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                skipped++;
                continue;
            }

            // 이미 BlockUnit이 있으면 스킵
            if (child.GetComponent<BlockUnit>() != null)
            {
                skipped++;
                continue;
            }

            // 콜라이더 없으면 스킵 (BlockUnit은 콜라이더 필수)
            Collider col = child.GetComponent<Collider>();
            if (col == null)
            {
                skipped++;
                continue;
            }

            // Trigger 제외 옵션
            if (excludeTriggers && col.isTrigger)
            {
                skipped++;
                continue;
            }

            AddBlockUnitWithUndo(child.gameObject);
            added++;
        }

        Debug.Log($"[LayoutModuleGroup] {gameObject.name} (Module {moduleId}): " +
                  $"BlockUnit 부착 — 추가: {added}, 스킵: {skipped}");

        // 부착 후 자동 재수집
        CollectChildBlocks();
        registeredBlockCount = blocks != null ? blocks.Length : 0;
    }

    [ContextMenu("자식 BlockUnit 재수집")]
    private void DebugReCollect()
    {
        CollectChildBlocks();
        registeredBlockCount = blocks != null ? blocks.Length : 0;
        Debug.Log($"[LayoutModuleGroup] BlockUnit {registeredBlockCount}개 재수집");
    }

    [ContextMenu("스폰 포인트 재수집")]
    private void DebugReCollectSpawnPoints()
    {
        CollectSpawnPoints();
        registeredSpawnPointCount = enemySpawnPoints != null ? enemySpawnPoints.Length : 0;
        Debug.Log($"[LayoutModuleGroup] 스폰 포인트 {registeredSpawnPointCount}개 재수집");
    }

    [ContextMenu("모듈 활성화 (테스트)")]
    private void DebugActivate() => ActivateModule();

    [ContextMenu("모듈 비활성화 (테스트)")]
    private void DebugDeactivate() => DeactivateModule();

    /// <summary>직계 자식만 Transform 배열로 수집</summary>
    private Transform[] CollectDirectChildren()
    {
        Transform[] result = new Transform[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
            result[i] = transform.GetChild(i);
        return result;
    }

    /// <summary>Undo 지원 BlockUnit 부착 (에디터에서 Ctrl+Z 가능)</summary>
    private void AddBlockUnitWithUndo(GameObject target)
    {
#if UNITY_EDITOR
        UnityEditor.Undo.AddComponent<BlockUnit>(target);
#else
        target.AddComponent<BlockUnit>();
#endif
    }

    #endregion

    // ============================================================
    // 디버그 기즈모
    // ============================================================

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        // 블록과 모듈 루트 연결선 (소속 시각화)
        if (blocks != null)
        {
            Gizmos.color = isCore
                ? new Color(1f, 0.8f, 0.2f, 0.5f)        // 코어: 노란색
                : new Color(0.3f, 0.7f, 1f, 0.4f);       // 일반 모듈: 푸른색

            foreach (BlockUnit block in blocks)
            {
                if (block == null) continue;
                Gizmos.DrawLine(transform.position + Vector3.up * 0.5f,
                                block.transform.position + Vector3.up * 0.5f);
            }
        }

        // 스폰 포인트 시각화 (빨강)
        if (enemySpawnPoints != null)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.7f);
            foreach (Transform pt in enemySpawnPoints)
            {
                if (pt == null) continue;
                Gizmos.DrawSphere(pt.position, 0.4f);
                Gizmos.DrawLine(pt.position, pt.position + Vector3.up * 1.5f);
            }
        }
    }

    #endregion
}
