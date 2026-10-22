using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

/// ============================================================
/// BlockTransformManager.cs — Layout 내 블록 변형 매니저 (싱글톤)
/// ============================================================
/// 한 Layout 안에서 일부 블록을 솟아오름/가라앉음 시켜
/// 같은 지역인데 매번 다른 지형으로 느껴지게 만드는 시스템.
///
/// 핵심 역할:
///   1. PerformTransform(layoutIndex, patternId)로 변형 실행
///   2. 패턴별 블록 선택 (Default / InvertSubset / SinkRandom / RiseRandom)
///   3. 플레이어 발 밑 블록은 자동으로 변형 대상에서 제외
///   4. 트윈 완료 대기 후 NavMesh 리베이크
///   5. 변형 시작/종료 이벤트 발행 (사운드, 카메라 등이 구독 가능)
///
/// 호출 흐름:
///   StageManager.WaveTransitionRoutine
///     → yield return BlockTransformManager.Instance.PerformTransform(...)
///     ↓
///   1. ArenaMapManager에서 Layout Transform 조회
///   2. Layout에서 LayoutBlockGroup 조회 (캐시)
///   3. 플레이어 발 밑 블록 검출
///   4. 패턴별 블록 선택 + Sink/Rise 트윈 실행
///   5. transformDuration 대기
///   6. NavMesh 리베이크 + OnTransformCompleted 발행
///
/// 의존성:
///   - ArenaMapManager (Layout Transform 조회)
///   - LayoutBlockGroup (블록 묶음 + 발 밑 검사)
///   - BlockUnit (개별 블록 트윈)
///   - NavMeshSurface (인스펙터 등록 — ArenaMapManager와 동일한 것을 사용)
/// ============================================================
public class BlockTransformManager : MonoBehaviour
{
    public static BlockTransformManager Instance { get; private set; }

    // ============================================================
    // 변형 패턴 정의
    // ============================================================

    /// <summary>변형 패턴 식별자</summary>
    public enum TransformPatternId
    {
        /// <summary>기본 패턴 — 내부적으로 InvertSubset과 동일</summary>
        Default,

        /// <summary>Visible 일부 가라앉힘 + Hidden 일부 솟게 (가장 다이나믹)</summary>
        InvertSubset,

        /// <summary>Visible 블록 중 일부만 가라앉힘 (지형이 점점 깎임)</summary>
        SinkRandom,

        /// <summary>Hidden 블록 중 일부만 솟게 (지형이 회복/확장)</summary>
        RiseRandom
    }

    // ============================================================
    // 인스펙터 설정
    // ============================================================

    [Header("변형 비율")]
    [Tooltip("변형 후보 블록 중 실제로 변형할 비율 (0.0~1.0).\n" +
             "예: 0.3이면 후보의 약 30% 블록이 변형됨")]
    [SerializeField, Range(0.05f, 1f)] private float transformRatio = 0.3f;

    [Tooltip("최소 변형 블록 수 — 비율 계산 결과가 이보다 작으면 강제로 이 값 사용.\n" +
             "후보가 적어도 시각적 변화를 보장")]
    [SerializeField, Min(0)] private int minBlocksToTransform = 1;

    [Header("전환 시간")]
    [Tooltip("개별 블록의 솟음/가라앉음 트윈 지속 시간 (초)")]
    [SerializeField] private float transformDuration = 0.6f;

    [Tooltip("변형 시작 후 NavMesh 리베이크까지의 추가 여유 시간 (초). " +
             "트윈이 완전히 끝난 뒤에 리베이크되도록 보장")]
    [SerializeField] private float postTransformBuffer = 0.1f;

    [Header("동시성")]
    [Tooltip("이미 변형이 진행 중일 때 새 PerformTransform 호출의 처리 방식.\n" +
             "true: 기존 변형 중단하고 새 변형 시작 / false: 새 호출 무시")]
    [SerializeField] private bool allowInterrupt = false;

    [Header("NavMesh")]
    [Tooltip("변형 후 리베이크할 NavMeshSurface (ArenaMapManager와 동일한 것 권장)")]
    [SerializeField] private NavMeshSurface navMeshSurface;

    [Header("플레이어 참조")]
    [Tooltip("플레이어 Transform. null이면 Player 태그로 자동 검색")]
    [SerializeField] private Transform playerTransform;

    [Header("디버깅용 현재 상태")]
    [SerializeField] private bool isTransforming = false;

    // ============================================================
    // 런타임 상태
    // ============================================================

    /// <summary>Layout 인덱스별 LayoutBlockGroup 캐시 (매번 GetComponent 방지)</summary>
    private readonly Dictionary<int, LayoutBlockGroup> blockGroupCache
        = new Dictionary<int, LayoutBlockGroup>();

    /// <summary>현재 진행 중인 변형 코루틴 — 중단 처리용</summary>
    private Coroutine activeTransformCoroutine;

    // ============================================================
    // 이벤트
    // ============================================================

    /// <summary>변형 시작 시 발행 (인자: layoutIndex, patternId)</summary>
    public event System.Action<int, TransformPatternId> OnTransformStarted;

    /// <summary>변형 완료 시 발행 (인자: layoutIndex)</summary>
    public event System.Action<int> OnTransformCompleted;

    /// <summary>현재 변형이 진행 중인지</summary>
    public bool IsTransforming => isTransforming;

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
    /// 지정된 Layout에 변형 패턴을 적용한다.
    /// StageManager가 yield return으로 완료까지 대기 가능.
    ///
    /// 진행 중인 다른 변형이 있으면:
    ///   - allowInterrupt = true: 기존 중단 후 새로 시작
    ///   - allowInterrupt = false: 새 호출은 무시되고 null 반환
    /// </summary>
    /// <param name="layoutIndex">대상 Layout 인덱스</param>
    /// <param name="patternIdString">패턴 ID 문자열 (WaveData.transformPatternId).
    ///   비어있거나 매칭 실패 시 Default로 폴백</param>
    /// <returns>변형 코루틴 — yield 가능. 무시된 경우 null</returns>
    public Coroutine PerformTransform(int layoutIndex, string patternIdString)
    {
        TransformPatternId pattern = ResolvePatternId(patternIdString);
        return PerformTransform(layoutIndex, pattern);
    }

    /// <summary>패턴 enum 직접 지정 버전 (오버로드)</summary>
    public Coroutine PerformTransform(int layoutIndex, TransformPatternId pattern)
    {
        // 진행 중 처리
        if (isTransforming)
        {
            if (allowInterrupt && activeTransformCoroutine != null)
            {
                StopCoroutine(activeTransformCoroutine);
                Debug.Log("[BlockTransformManager] 기존 변형 중단 후 새 변형 시작");
            }
            else
            {
                Debug.LogWarning("[BlockTransformManager] 이미 변형 진행 중 — 새 호출 무시");
                return null;
            }
        }

        activeTransformCoroutine = StartCoroutine(PerformTransformRoutine(layoutIndex, pattern));
        return activeTransformCoroutine;
    }

    #endregion

    // ============================================================
    // 핵심 변형 루틴 (코루틴)
    // ============================================================

    #region Transform Routine

    /// <summary>변형 메인 루틴 — 블록 선택 → 트윈 실행 → 완료 대기 → NavMesh</summary>
    private IEnumerator PerformTransformRoutine(int layoutIndex, TransformPatternId pattern)
    {
        isTransforming = true;
        OnTransformStarted?.Invoke(layoutIndex, pattern);

        // 1. Layout의 BlockGroup 조회
        LayoutBlockGroup group = GetCachedBlockGroup(layoutIndex);
        if (group == null || !group.HasBlocks)
        {
            Debug.LogWarning($"[BlockTransformManager] Layout_{layoutIndex}에 LayoutBlockGroup이 " +
                             "없거나 블록이 없음 — 변형 스킵");
            FinishTransform(layoutIndex);
            yield break;
        }

        // 2. 플레이어 발 밑 블록 검출 (변형 대상에서 제외)
        Transform player = ResolvePlayerTransform();
        BlockUnit safeBlock = (player != null) ? group.GetBlockUnderPlayer(player) : null;

        if (safeBlock != null)
            Debug.Log($"[BlockTransformManager] 안전 블록 검출: {safeBlock.name} — 변형에서 제외");

        // 3. 패턴별 블록 선택 + 트윈 실행
        int transformedCount = ApplyPattern(group, pattern, safeBlock);

        if (transformedCount == 0)
        {
            Debug.Log("[BlockTransformManager] 변형할 블록 없음 (모두 보호 또는 후보 없음)");
            FinishTransform(layoutIndex);
            yield break;
        }

        Debug.Log($"[BlockTransformManager] Layout_{layoutIndex}: " +
                  $"패턴 {pattern} / 변형 블록 {transformedCount}개");

        // 4. 트윈 완료 대기 + 안전 버퍼
        yield return new WaitForSeconds(transformDuration + postTransformBuffer);

        // 5. NavMesh 리베이크 (적 경로 갱신)
        RebakeNavMesh();

        FinishTransform(layoutIndex);
    }

    /// <summary>변형 루틴 종료 처리 (성공/실패 공통)</summary>
    private void FinishTransform(int layoutIndex)
    {
        isTransforming = false;
        activeTransformCoroutine = null;
        OnTransformCompleted?.Invoke(layoutIndex);
    }

    #endregion

    // ============================================================
    // 패턴 적용
    // ============================================================

    #region Pattern Application

    /// <summary>
    /// 선택된 패턴에 따라 블록을 변형한다.
    /// 안전 블록(safeBlock)은 어떤 패턴에서도 변형되지 않음.
    /// </summary>
    /// <returns>실제로 변형된 블록 개수</returns>
    private int ApplyPattern(LayoutBlockGroup group, TransformPatternId pattern, BlockUnit safeBlock)
    {
        switch (pattern)
        {
            case TransformPatternId.Default:
            case TransformPatternId.InvertSubset:
                return ApplyInvertSubset(group, safeBlock);

            case TransformPatternId.SinkRandom:
                return ApplySinkRandom(group, safeBlock);

            case TransformPatternId.RiseRandom:
                return ApplyRiseRandom(group, safeBlock);

            default:
                Debug.LogWarning($"[BlockTransformManager] 알 수 없는 패턴: {pattern} — Default로 폴백");
                return ApplyInvertSubset(group, safeBlock);
        }
    }

    /// <summary>InvertSubset 패턴 — Visible 일부 가라앉힘 + Hidden 일부 솟게</summary>
    private int ApplyInvertSubset(LayoutBlockGroup group, BlockUnit safeBlock)
    {
        // Visible 후보: 안전 블록 제외
        List<BlockUnit> visibleCandidates = CollectFilteredBlocks(group, BlockUnit.BlockState.Visible, safeBlock);
        // Hidden 후보: 그대로 (Hidden 블록은 어차피 발 밑이 될 수 없음)
        List<BlockUnit> hiddenCandidates = CollectFilteredBlocks(group, BlockUnit.BlockState.Hidden, null);

        int sinkCount = ResolveTransformCount(visibleCandidates.Count);
        int riseCount = ResolveTransformCount(hiddenCandidates.Count);

        ShuffleInPlace(visibleCandidates);
        ShuffleInPlace(hiddenCandidates);

        int total = 0;
        total += SinkBlocks(visibleCandidates, sinkCount);
        total += RaiseBlocks(hiddenCandidates, riseCount);

        return total;
    }

    /// <summary>SinkRandom 패턴 — Visible 블록 일부만 가라앉힘</summary>
    private int ApplySinkRandom(LayoutBlockGroup group, BlockUnit safeBlock)
    {
        List<BlockUnit> candidates = CollectFilteredBlocks(group, BlockUnit.BlockState.Visible, safeBlock);

        int count = ResolveTransformCount(candidates.Count);
        ShuffleInPlace(candidates);

        return SinkBlocks(candidates, count);
    }

    /// <summary>RiseRandom 패턴 — Hidden 블록 일부만 솟게</summary>
    private int ApplyRiseRandom(LayoutBlockGroup group, BlockUnit safeBlock)
    {
        List<BlockUnit> candidates = CollectFilteredBlocks(group, BlockUnit.BlockState.Hidden, null);

        int count = ResolveTransformCount(candidates.Count);
        ShuffleInPlace(candidates);

        return RaiseBlocks(candidates, count);
    }

    /// <summary>
    /// 그룹에서 특정 상태의 블록을 새 List로 수집 (안전 블록 제외).
    /// LayoutBlockGroup의 workBuffer를 거치지 않고 직접 순회 — 안전한 사본 확보.
    /// </summary>
    private List<BlockUnit> CollectFilteredBlocks(
        LayoutBlockGroup group, BlockUnit.BlockState targetState, BlockUnit excludeBlock)
    {
        List<BlockUnit> result = new List<BlockUnit>();
        BlockUnit[] all = group.GetAllBlocks();
        if (all == null) return result;

        foreach (BlockUnit block in all)
        {
            if (block == null) continue;
            if (block == excludeBlock) continue;
            if (block.State != targetState) continue;

            result.Add(block);
        }

        return result;
    }

    /// <summary>후보 수에 비율을 적용해서 실제 변형할 개수 결정 (최소값 보장)</summary>
    private int ResolveTransformCount(int candidateCount)
    {
        if (candidateCount <= 0) return 0;

        int byRatio = Mathf.RoundToInt(candidateCount * transformRatio);
        int finalCount = Mathf.Max(byRatio, minBlocksToTransform);

        return Mathf.Min(finalCount, candidateCount);
    }

    /// <summary>리스트의 앞쪽 count개 블록을 가라앉힘</summary>
    private int SinkBlocks(List<BlockUnit> blocks, int count)
    {
        int actual = Mathf.Min(count, blocks.Count);
        for (int i = 0; i < actual; i++)
        {
            if (blocks[i] != null)
                blocks[i].SinkDown(transformDuration);
        }
        return actual;
    }

    /// <summary>리스트의 앞쪽 count개 블록을 솟아오르게 함</summary>
    private int RaiseBlocks(List<BlockUnit> blocks, int count)
    {
        int actual = Mathf.Min(count, blocks.Count);
        for (int i = 0; i < actual; i++)
        {
            if (blocks[i] != null)
                blocks[i].RaiseUp(transformDuration);
        }
        return actual;
    }

    /// <summary>
    /// Fisher-Yates 셔플 (in-place).
    /// LINQ OrderBy(Random) 보다 빠르고 GC 압박이 적음.
    /// </summary>
    private void ShuffleInPlace<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    #endregion

    // ============================================================
    // 패턴 ID 문자열 변환
    // ============================================================

    #region Pattern ID Resolution

    /// <summary>
    /// WaveData.transformPatternId 문자열을 enum으로 변환.
    /// 비어있거나 매칭 실패 시 Default 반환.
    /// 대소문자 무시.
    /// </summary>
    private TransformPatternId ResolvePatternId(string patternIdString)
    {
        if (string.IsNullOrWhiteSpace(patternIdString))
            return TransformPatternId.Default;

        if (System.Enum.TryParse(patternIdString, ignoreCase: true,
                                 out TransformPatternId result))
            return result;

        Debug.LogWarning($"[BlockTransformManager] 알 수 없는 패턴 문자열: '{patternIdString}' " +
                         "— Default로 폴백");
        return TransformPatternId.Default;
    }

    #endregion

    // ============================================================
    // Layout / Player 참조 해석
    // ============================================================

    #region Reference Resolution

    /// <summary>Layout 인덱스로 LayoutBlockGroup 조회 (캐시 우선, 미스 시 GetComponent)</summary>
    private LayoutBlockGroup GetCachedBlockGroup(int layoutIndex)
    {
        if (blockGroupCache.TryGetValue(layoutIndex, out LayoutBlockGroup cached))
        {
            if (cached != null) return cached;

            // 캐시 항목이 살아있던 컴포넌트가 파괴된 경우 → 재조회
            blockGroupCache.Remove(layoutIndex);
        }

        if (ArenaMapManager.Instance == null) return null;

        Transform layoutTf = ArenaMapManager.Instance.GetLayoutTransform(layoutIndex);
        if (layoutTf == null) return null;

        LayoutBlockGroup group = layoutTf.GetComponent<LayoutBlockGroup>();
        if (group != null)
            blockGroupCache[layoutIndex] = group;

        return group;
    }

    /// <summary>플레이어 Transform 조회. 인스펙터 등록 우선, 없으면 Player 태그로 검색</summary>
    private Transform ResolvePlayerTransform()
    {
        if (playerTransform != null) return playerTransform;

        GameObject go = GameObject.FindWithTag("Player");
        if (go != null)
        {
            playerTransform = go.transform; // 한 번 찾으면 캐싱
            return playerTransform;
        }

        return null;
    }

    /// <summary>NavMesh 리베이크 — 변형으로 인한 경로 변화 반영</summary>
    private void RebakeNavMesh()
    {
        if (navMeshSurface != null)
            navMeshSurface.BuildNavMesh();
        else
            Debug.LogWarning("[BlockTransformManager] NavMeshSurface가 설정되지 않았습니다.");
    }

    #endregion

    // ============================================================
    // 외부 인터페이스
    // ============================================================

    #region Public Interface

    /// <summary>특정 Layout의 BlockGroup을 캐시에 등록 (필요 시 외부 강제 등록용)</summary>
    public void RegisterBlockGroup(int layoutIndex, LayoutBlockGroup group)
    {
        if (group == null) return;
        blockGroupCache[layoutIndex] = group;
    }

    /// <summary>캐시 초기화 (스테이지 재시작 등)</summary>
    public void ClearCache()
    {
        blockGroupCache.Clear();
    }

    /// <summary>모든 등록된 Layout의 블록을 Visible로 리셋</summary>
    public void ResetAllLayouts()
    {
        foreach (var kv in blockGroupCache)
        {
            kv.Value?.ResetAllBlocksToVisible();
        }
    }

    #endregion

    // ============================================================
    // 디버그
    // ============================================================

    #region Debug

    [ContextMenu("Test - Layout 0 InvertSubset")]
    private void DebugInvert0() => PerformTransform(0, TransformPatternId.InvertSubset);

    [ContextMenu("Test - Layout 0 SinkRandom")]
    private void DebugSink0() => PerformTransform(0, TransformPatternId.SinkRandom);

    [ContextMenu("Test - Layout 0 RiseRandom")]
    private void DebugRise0() => PerformTransform(0, TransformPatternId.RiseRandom);

    [ContextMenu("Test - 모든 Layout 리셋")]
    private void DebugResetAll() => ResetAllLayouts();

    #endregion
}
