using System.Collections.Generic;
using DG.Tweening;
using Unity.AI.Navigation;
using UnityEngine;

/// ============================================================
/// ArenaMapManager.cs — 아레나 지형 누적형 활성화 매니저
/// ============================================================
/// 두 가지 시작 모드 지원:
///   A. 누적형 모드 (기존, Gatekeeper 풍): startWithAllLayoutsActive = false
///      → Awake에서 모든 Layout 숨김 → StageManager가 단계적으로 ActivateLayout 호출
///   B. 정적 모드 (세그먼트 트윈즈 스타일): startWithAllLayoutsActive = true
///      → Awake에서 모든 Layout 즉시 활성화 → 지형 솟아오름 연출 없음
///
/// 핵심 역할:
///   1. ActivateLayout(index): 단일 Layout을 솟아오르게 (누적)
///   2. DeactivateLayout(index): 단일 Layout을 가라앉게 (보스 등 특수 상황)
///   3. DeactivateAllLayouts(): 모든 활성 Layout 일괄 가라앉힘
///   4. Center(Floor)는 항상 활성 — layoutGroups에 넣지 않음
///   5. 활성/비활성 완료 시 NavMesh 자동 리베이크
///   6. Layout 활성화 이벤트 발행 (StageManager 구독)
///
/// 씬 구성:
///   Arena
///     ├─ Floor (Center) — 항상 활성
///     ├─ Layout_0 — layoutGroups[0]
///     ├─ Layout_1 — layoutGroups[1]
///     └─ Layout_2 — layoutGroups[2]
/// ============================================================
public class ArenaMapManager : MonoBehaviour
{
    public static ArenaMapManager Instance { get; private set; }

    // ============================================================
    // 인스펙터 설정
    // ============================================================

    [Header("시작 모드")]
    [Tooltip("씬 시작 시 모든 Layout을 즉시 활성화 (세그먼트 트윈즈 스타일 — 지형 솟아오름 연출 없음). false면 기존 누적형 동작")]
    [SerializeField] private bool startWithAllLayoutsActive = false;

    [Header("레이아웃 그룹")]
    [Tooltip("각 레이아웃의 루트 Transform (Center 제외). Layout_0, 1, 2 등록")]
    [SerializeField] private Transform[] layoutGroups;

    [Header("아레나 설정")]
    [Tooltip("아레나 중심 위치 (보스 스폰 등에 사용)")]
    [SerializeField] private Transform arenaCenter;

    [Header("NavMesh")]
    [Tooltip("NavMesh 리베이크용 Surface")]
    [SerializeField] private NavMeshSurface navMeshSurface;

    [Header("전환 연출 설정")]
    [Tooltip("가라앉는 Y 오프셋 (이 깊이만큼 아래로 내려감)")]
    [SerializeField] private float sinkDepth = -8f;

    [Tooltip("기본 전환 시간 (호출 시 지정 안 했을 때)")]
    [SerializeField] private float defaultTransitionDuration = 1.5f;

    [Tooltip("솟아오를 때 이징")]
    [SerializeField] private Ease riseEase = Ease.OutBack;

    [Tooltip("가라앉을 때 이징")]
    [SerializeField] private Ease sinkEase = Ease.InBack;

    [Tooltip("전환 시 카메라 흔들림 강도 (0이면 비활성)")]
    [SerializeField] private float shakeIntensity = 0.3f;

    [Tooltip("전환 시 카메라 흔들림 지속 시간")]
    [SerializeField] private float shakeDuration = 0.4f;

    // ============================================================
    // 이벤트
    // ============================================================

    /// <summary>
    /// 특정 Layout이 솟아올라서 활성화 완료된 순간 발행
    /// StageManager가 구독하여 몹 스폰 타이밍 등에 활용
    /// </summary>
    public event System.Action<int> OnLayoutActivated;

    /// <summary>
    /// 특정 Layout이 가라앉아서 비활성화 완료된 순간 발행
    /// </summary>
    public event System.Action<int> OnLayoutDeactivated;

    // ============================================================
    // 런타임 상태
    // ============================================================

    /// <summary>현재 활성화된 레이아웃 인덱스 집합 (누적 관리)</summary>
    private readonly HashSet<int> activeLayoutIndices = new HashSet<int>();

    /// <summary>가장 최근에 활성화된 Layout 인덱스 (몹 스폰 대상 후보)</summary>
    private int latestActivatedIndex = -1;

    /// <summary>현재 진행 중인 전환(활성/비활성) 개수 — 동시 진행 가능</summary>
    private int transitioningCount = 0;

    /// <summary>각 레이아웃 그룹의 원래 Y 위치 (복원용)</summary>
    private float[] originalYPositions;

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    #region Unity Lifecycle

    /// <summary>
    /// 싱글톤 등록 + 시작 모드에 따라 레이아웃 초기 상태 결정
    /// </summary>
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        CacheOriginalPositions();

        // 시작 모드 분기 — 정적 / 누적형
        if (startWithAllLayoutsActive)
        {
            ShowAllLayoutsImmediate();
        }
        else
        {
            HideAllLayouts();
        }
    }

    #endregion

    // ============================================================
    // 초기화
    // ============================================================

    #region Initialization

    /// <summary>각 레이아웃 그룹의 원래 Y 위치를 캐싱 (솟아오를 목표 지점)</summary>
    private void CacheOriginalPositions()
    {
        if (layoutGroups == null || layoutGroups.Length == 0)
            return;

        originalYPositions = new float[layoutGroups.Length];

        for (int i = 0; i < layoutGroups.Length; i++)
        {
            if (layoutGroups[i] != null)
                originalYPositions[i] = layoutGroups[i].position.y;
        }
    }

    /// <summary>시작 시 모든 레이아웃을 바닥 아래로 숨김 + 비활성 (누적형 모드)</summary>
    private void HideAllLayouts()
    {
        if (layoutGroups == null) return;

        for (int i = 0; i < layoutGroups.Length; i++)
        {
            if (layoutGroups[i] == null) continue;

            Vector3 pos = layoutGroups[i].position;
            pos.y = originalYPositions[i] + sinkDepth;
            layoutGroups[i].position = pos;
            layoutGroups[i].gameObject.SetActive(false);
        }

        activeLayoutIndices.Clear();
        latestActivatedIndex = -1;
        transitioningCount = 0;
    }

    /// <summary>
    /// 시작 시 모든 레이아웃을 원래 위치에 즉시 활성화 (세그먼트 트윈즈 모드)
    /// - 애니메이션/카메라 흔들림 없이 즉시 표시
    /// - 활성 집합에 전체 등록 + NavMesh 한 번만 리베이크 (효율)
    /// </summary>
    private void ShowAllLayoutsImmediate()
    {
        if (layoutGroups == null) return;

        for (int i = 0; i < layoutGroups.Length; i++)
        {
            if (layoutGroups[i] == null) continue;

            // 원래 위치로 즉시 이동 + 활성화
            Vector3 pos = layoutGroups[i].position;
            pos.y = originalYPositions[i];
            layoutGroups[i].position = pos;
            layoutGroups[i].gameObject.SetActive(true);

            // 활성 집합에 등록
            activeLayoutIndices.Add(i);
        }

        // 마지막 인덱스를 최근 활성화로 표시 (외부 시스템 호환용)
        latestActivatedIndex = layoutGroups.Length - 1;
        transitioningCount = 0;

        // NavMesh 한 번만 리베이크 (전체 처리 후)
        RebakeNavMesh();

        Debug.Log($"[ArenaMapManager] 시작 시 모든 Layout 즉시 활성화 (세그먼트 트윈즈 모드) — 활성: {activeLayoutIndices.Count}개");
    }

    #endregion

    // ============================================================
    // Layout 활성화 (핵심 — 누적형)
    // ============================================================

    #region Layout Activation

    /// <summary>
    /// 지정한 Layout을 솟아오르게 활성화 (기존 활성 Layout은 그대로 유지 — 누적)
    /// </summary>
    /// <param name="index">활성화할 레이아웃 인덱스</param>
    /// <param name="duration">전환 시간 (0 이하면 기본값 사용)</param>
    public void ActivateLayout(int index, float duration = 0f)
    {
        if (!IsValidIndex(index))
        {
            Debug.LogWarning($"[ArenaMapManager] 유효하지 않은 레이아웃 인덱스: {index}");
            return;
        }

        if (activeLayoutIndices.Contains(index))
        {
            Debug.Log($"[ArenaMapManager] Layout_{index}는 이미 활성 상태입니다.");
            return;
        }

        float transitionTime = duration > 0f ? duration : defaultTransitionDuration;

        RiseLayout(index, transitionTime);
        PlayTransitionShake();

        latestActivatedIndex = index;
        activeLayoutIndices.Add(index);

        Debug.Log($"[ArenaMapManager] Layout_{index} 활성화 시작 ({transitionTime}초) / 현재 활성: {activeLayoutIndices.Count}개");
    }

    /// <summary>
    /// 지정한 Layout을 가라앉혀서 비활성화 (보스 진입 등 특수 상황)
    /// </summary>
    public void DeactivateLayout(int index, float duration = 0f)
    {
        if (!IsValidIndex(index)) return;
        if (!activeLayoutIndices.Contains(index)) return;

        float transitionTime = duration > 0f ? duration : defaultTransitionDuration;

        SinkLayout(index, transitionTime);
        activeLayoutIndices.Remove(index);

        // 최근 활성 인덱스 갱신 (제거된 게 최신이었다면 집합에서 가장 큰 것 선택)
        if (latestActivatedIndex == index)
            latestActivatedIndex = GetHighestActiveIndex();

        Debug.Log($"[ArenaMapManager] Layout_{index} 비활성화 시작 / 남은 활성: {activeLayoutIndices.Count}개");
    }

    /// <summary>
    /// 현재 활성화된 모든 Layout을 일괄 가라앉힘 (보스 페이즈 진입 시 사용)
    /// </summary>
    public void DeactivateAllLayouts(float duration = 0f)
    {
        if (activeLayoutIndices.Count == 0) return;

        // 복사본으로 순회 (순회 중 컬렉션 변경 방지)
        int[] snapshot = new int[activeLayoutIndices.Count];
        activeLayoutIndices.CopyTo(snapshot);

        foreach (int idx in snapshot)
            DeactivateLayout(idx, duration);
    }

    /// <summary>레이아웃 그룹을 솟아오르게 하는 연출 (활성화)</summary>
    private void RiseLayout(int index, float duration)
    {
        Transform group = layoutGroups[index];
        if (group == null) return;

        // 시작 위치 = 바닥 아래, 목표 위치 = 원래 Y
        Vector3 startPos = group.position;
        startPos.y = originalYPositions[index] + sinkDepth;
        group.position = startPos;

        group.gameObject.SetActive(true);

        transitioningCount++;

        group.DOMoveY(originalYPositions[index], duration)
            .SetEase(riseEase)
            .OnComplete(() =>
            {
                transitioningCount = Mathf.Max(0, transitioningCount - 1);

                RebakeNavMesh();
                OnLayoutActivated?.Invoke(index);

                Debug.Log($"[ArenaMapManager] Layout_{index} 활성화 완료 → NavMesh 리베이크 + 이벤트 발행");
            });
    }

    /// <summary>레이아웃 그룹을 가라앉게 하는 연출 (비활성화)</summary>
    private void SinkLayout(int index, float duration)
    {
        Transform group = layoutGroups[index];
        if (group == null) return;

        float targetY = originalYPositions[index] + sinkDepth;

        transitioningCount++;

        group.DOMoveY(targetY, duration)
            .SetEase(sinkEase)
            .OnComplete(() =>
            {
                transitioningCount = Mathf.Max(0, transitioningCount - 1);

                group.gameObject.SetActive(false);

                RebakeNavMesh();
                OnLayoutDeactivated?.Invoke(index);

                Debug.Log($"[ArenaMapManager] Layout_{index} 비활성화 완료");
            });
    }

    /// <summary>NavMesh 리베이크 — 지형 변화 후 적 경로 재계산</summary>
    private void RebakeNavMesh()
    {
        if (navMeshSurface != null)
            navMeshSurface.BuildNavMesh();
        else
            Debug.LogWarning("[ArenaMapManager] NavMeshSurface가 설정되지 않았습니다.");
    }

    /// <summary>전환 시 카메라 흔들림 연출</summary>
    private void PlayTransitionShake()
    {
        if (shakeIntensity <= 0f) return;

        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        mainCam.transform.DOShakePosition(shakeDuration, shakeIntensity)
            .SetEase(Ease.OutQuad);
    }

    /// <summary>
    /// 즉시 Layout 활성화 (애니메이션 없음) — 게임 시작 시 초기 세팅용
    /// </summary>
    public void SetLayoutActiveImmediate(int index)
    {
        if (!IsValidIndex(index)) return;

        Transform group = layoutGroups[index];
        if (group == null) return;

        Vector3 pos = group.position;
        pos.y = originalYPositions[index];
        group.position = pos;
        group.gameObject.SetActive(true);

        activeLayoutIndices.Add(index);
        latestActivatedIndex = index;

        RebakeNavMesh();
    }

    #endregion

    // ============================================================
    // 외부 인터페이스
    // ============================================================

    #region Public Interface

    /// <summary>아레나 중심 좌표 반환 (보스 스폰 위치 등)</summary>
    public Vector3 GetArenaCenter()
    {
        return arenaCenter != null ? arenaCenter.position : transform.position;
    }

    /// <summary>가장 최근에 활성화된 Layout 인덱스 (몹 스폰 대상 선정용)</summary>
    public int GetLatestActivatedIndex() => latestActivatedIndex;

    /// <summary>현재 활성화된 Layout 개수</summary>
    public int GetActiveLayoutCount() => activeLayoutIndices.Count;

    /// <summary>특정 Layout이 활성 상태인지 확인</summary>
    public bool IsLayoutActive(int index) => activeLayoutIndices.Contains(index);

    /// <summary>현재 전환 진행 중인지 (활성/비활성 어느 쪽이든)</summary>
    public bool IsAnyTransitioning() => transitioningCount > 0;

    /// <summary>총 등록된 레이아웃 개수</summary>
    public int GetLayoutCount() => layoutGroups != null ? layoutGroups.Length : 0;

    /// <summary>특정 Layout의 Transform 반환 (스폰 포인트 탐색 등에 사용)</summary>
    public Transform GetLayoutTransform(int index)
    {
        if (!IsValidIndex(index)) return null;
        return layoutGroups[index];
    }

    /// <summary>
    /// 전체 리셋 (스테이지 재시작 시) — 모든 트윈 제거 + 시작 모드에 따라 재초기화
    /// </summary>
    public void ResetAll()
    {
        if (layoutGroups != null)
        {
            foreach (Transform group in layoutGroups)
            {
                if (group != null)
                    group.DOKill();
            }
        }

        // 시작 모드에 따라 재초기화
        if (startWithAllLayoutsActive)
        {
            ShowAllLayoutsImmediate();
        }
        else
        {
            HideAllLayouts();
        }

        Debug.Log("[ArenaMapManager] 전체 리셋 완료");
    }

    /// <summary>인덱스 유효성 검사</summary>
    private bool IsValidIndex(int index)
    {
        return layoutGroups != null && index >= 0 && index < layoutGroups.Length;
    }

    /// <summary>활성 집합에서 가장 큰 인덱스 반환 (없으면 -1)</summary>
    private int GetHighestActiveIndex()
    {
        int highest = -1;
        foreach (int idx in activeLayoutIndices)
        {
            if (idx > highest) highest = idx;
        }
        return highest;
    }

    #endregion

    // ============================================================
    // 레거시 호환 (StageManager 수정 전까지 유지)
    // ============================================================

    #region Legacy Compatibility

    /// <summary>[레거시] 과거 교체형 API — 내부적으로 ActivateLayout 호출</summary>
    public void TransitionToLayout(int newIndex, float duration = 0f)
    {
        ActivateLayout(newIndex, duration);
    }

    /// <summary>[레거시] IsTransitioning — IsAnyTransitioning으로 위임</summary>
    public bool IsTransitioning() => IsAnyTransitioning();

    /// <summary>[레거시] GetCurrentLayoutIndex — 가장 최근 활성 인덱스 반환</summary>
    public int GetCurrentLayoutIndex() => latestActivatedIndex;

    /// <summary>[레거시] OnTransitionComplete — OnLayoutActivated로 대체됨</summary>
    public event System.Action<int> OnTransitionComplete
    {
        add { OnLayoutActivated += value; }
        remove { OnLayoutActivated -= value; }
    }

    /// <summary>[레거시] SetLayoutImmediate</summary>
    public void SetLayoutImmediate(int index) => SetLayoutActiveImmediate(index);

    #endregion
}
