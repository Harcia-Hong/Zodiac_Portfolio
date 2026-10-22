using DG.Tweening;
using UnityEngine;

/// ============================================================
/// BlockUnit.cs — 변형 가능한 개별 블록 컴포넌트
/// ============================================================
/// 한 Layout 안의 개별 블록(타일)에 부착되어, 솟아오르거나
/// 가라앉을 수 있는 단위로 만들어주는 컴포넌트.
///
/// 핵심 역할:
///   1. 시작 위치(originalPosition) 캐싱 → "Visible 상태의 기준점"
///   2. RaiseUp() / SinkDown() / Toggle() 으로 상태 전환
///   3. 진행 중인 트윈 재호출 시 안전 처리 (DOKill 후 재시작)
///   4. 콜라이더 정보 기반 공간 헬퍼 제공 (플레이어 발 밑 검사용)
///
/// 사용 흐름:
///   Layout 안의 변형하고 싶은 블록 GameObject에 이 컴포넌트 부착
///     → Awake에서 시작 위치 자동 캐싱
///     → LayoutBlockGroup이 자식에서 자동 수집 (또는 인스펙터 등록)
///     → BlockTransformManager가 패턴에 따라 RaiseUp/SinkDown 호출
///
/// 셋업 조건:
///   - 콜라이더 1개 이상 필요 (Bounds 계산용)
///   - 처음 씬에 배치된 위치 = Visible 상태의 기준 Y로 자동 인식
/// ============================================================
public class BlockUnit : MonoBehaviour
{
    // ============================================================
    // 블록 상태
    // ============================================================

    /// <summary>블록의 시각적 상태</summary>
    public enum BlockState
    {
        Visible,        // 정상 위치 (솟아 있음)
        Hidden,         // 가라앉아 있음
        Transitioning   // 전환 중 (트윈 진행 중)
    }

    // ============================================================
    // 인스펙터 설정
    // ============================================================

    [Header("가라앉음 깊이")]
    [Tooltip("가라앉을 때 원래 Y 위치에서 얼마나 더 내려갈지 (음수 권장).\n" +
             "ArenaMapManager의 sinkDepth와 동일하게 맞추는 것을 추천")]
    [SerializeField] private float sinkDepth = -8f;

    [Header("전환 시간")]
    [Tooltip("기본 전환 시간 (초). 호출 시 별도 지정하지 않으면 사용됨")]
    [SerializeField] private float defaultDuration = 0.6f;

    [Header("이징")]
    [Tooltip("솟아오를 때 이징 곡선")]
    [SerializeField] private Ease riseEase = Ease.OutBack;

    [Tooltip("가라앉을 때 이징 곡선")]
    [SerializeField] private Ease sinkEase = Ease.InBack;

    [Header("초기 상태")]
    [Tooltip("게임 시작 시 가라앉아 있는 상태로 시작할지.\n" +
             "true면 Awake에서 즉시 sinkDepth 위치로 이동하고 Hidden 상태가 됨")]
    [SerializeField] private bool startHidden = false;

    // ============================================================
    // 런타임 상태
    // ============================================================

    /// <summary>블록의 원본(Visible) 위치 — Awake에서 캐싱</summary>
    private Vector3 originalPosition;

    /// <summary>현재 블록 상태 (디버깅 가능하도록 인스펙터에 노출)</summary>
    [Header("현재 상태 (디버깅용)")]
    [SerializeField] private BlockState currentState = BlockState.Visible;

    /// <summary>현재 진행 중인 트윈 — 재호출 시 kill하기 위해 보관</summary>
    private Tween activeTween;

    /// <summary>이 블록의 콜라이더 캐시 (Bounds 계산용)</summary>
    private Collider cachedCollider;

    // ============================================================
    // 외부 접근용 프로퍼티
    // ============================================================

    public BlockState State => currentState;
    public bool IsVisible => currentState == BlockState.Visible;
    public bool IsHidden => currentState == BlockState.Hidden;
    public bool IsTransitioning => currentState == BlockState.Transitioning;
    public Vector3 OriginalPosition => originalPosition;

    /// <summary>Visible 상태일 때의 표면 Y 좌표 (스폰 높이 등에 활용 가능)</summary>
    public float SurfaceY => originalPosition.y;

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    #region Unity Lifecycle

    private void Awake()
    {
        // 시작 위치를 Visible 상태의 기준점으로 캐싱
        originalPosition = transform.position;
        cachedCollider = GetComponentInChildren<Collider>();

        // 옵션: 시작 시 가라앉은 상태로 강제 설정
        if (startHidden)
            ApplyHiddenStateImmediate();
    }

    private void OnDestroy()
    {
        // 트윈이 살아있는 동안 오브젝트가 파괴되면 NRE 위험 — 정리
        KillActiveTween();
    }

    #endregion

    // ============================================================
    // 상태 전환 — 핵심 동작
    // ============================================================

    #region State Transitions

    /// <summary>
    /// 블록을 솟아오르게 한다 (Hidden → Visible).
    /// 이미 Visible이면 무시 (중복 호출 방지).
    /// 진행 중인 다른 트윈은 자동으로 중단되고 새 트윈으로 교체.
    /// </summary>
    /// <param name="duration">전환 시간 (0 이하면 defaultDuration 사용)</param>
    /// <returns>실행된 Tween 객체 (트래킹 용도). 무시된 경우 null</returns>
    public Tween RaiseUp(float duration = 0f)
    {
        if (currentState == BlockState.Visible) return null;

        float time = duration > 0f ? duration : defaultDuration;

        KillActiveTween();
        currentState = BlockState.Transitioning;

        activeTween = transform.DOMoveY(originalPosition.y, time)
            .SetEase(riseEase)
            .OnComplete(() =>
            {
                currentState = BlockState.Visible;
                activeTween = null;
            });

        return activeTween;
    }

    /// <summary>
    /// 블록을 가라앉힌다 (Visible → Hidden).
    /// 이미 Hidden이면 무시.
    /// 진행 중인 다른 트윈은 자동으로 중단되고 새 트윈으로 교체.
    /// </summary>
    /// <param name="duration">전환 시간 (0 이하면 defaultDuration 사용)</param>
    /// <returns>실행된 Tween 객체. 무시된 경우 null</returns>
    public Tween SinkDown(float duration = 0f)
    {
        if (currentState == BlockState.Hidden) return null;

        float time = duration > 0f ? duration : defaultDuration;

        KillActiveTween();
        currentState = BlockState.Transitioning;

        float targetY = originalPosition.y + sinkDepth;
        activeTween = transform.DOMoveY(targetY, time)
            .SetEase(sinkEase)
            .OnComplete(() =>
            {
                currentState = BlockState.Hidden;
                activeTween = null;
            });

        return activeTween;
    }

    /// <summary>
    /// 현재 상태를 토글한다 (Visible ↔ Hidden).
    /// Transitioning 중에는 무시.
    /// </summary>
    public Tween Toggle(float duration = 0f)
    {
        switch (currentState)
        {
            case BlockState.Visible: return SinkDown(duration);
            case BlockState.Hidden: return RaiseUp(duration);
            default: return null;   // Transitioning은 무시
        }
    }

    /// <summary>
    /// 즉시 Visible 상태로 강제 설정 (애니메이션 없음).
    /// 리셋, 디버그, 초기화 등에 사용.
    /// </summary>
    public void SetVisibleImmediate()
    {
        KillActiveTween();
        transform.position = originalPosition;
        currentState = BlockState.Visible;
    }

    /// <summary>
    /// 즉시 Hidden 상태로 강제 설정 (애니메이션 없음).
    /// </summary>
    public void SetHiddenImmediate()
    {
        KillActiveTween();
        ApplyHiddenStateImmediate();
    }

    /// <summary>위치만 Hidden으로 옮기고 상태를 갱신 — 내부 헬퍼</summary>
    private void ApplyHiddenStateImmediate()
    {
        Vector3 pos = transform.position;
        pos.y = originalPosition.y + sinkDepth;
        transform.position = pos;
        currentState = BlockState.Hidden;
    }

    /// <summary>진행 중인 트윈이 있으면 중단 (재호출 안전 처리)</summary>
    private void KillActiveTween()
    {
        if (activeTween != null && activeTween.IsActive())
        {
            activeTween.Kill();
            activeTween = null;
        }
    }

    #endregion

    // ============================================================
    // 공간 헬퍼 — 플레이어 발 밑 검사 등에 활용
    // ============================================================

    #region Spatial Helpers

    /// <summary>
    /// 이 블록의 월드 좌표계 Bounds 반환.
    /// 콜라이더가 없으면 transform.position 기준 영(零) Bounds 반환.
    /// </summary>
    public Bounds GetWorldBounds()
    {
        if (cachedCollider != null)
            return cachedCollider.bounds;

        return new Bounds(transform.position, Vector3.zero);
    }

    /// <summary>
    /// 주어진 월드 좌표(XZ 평면 기준)가 이 블록의 위에 있는지 검사.
    /// 플레이어 발 밑 블록 검출에 사용 — Y는 무시하고 평면 영역만 비교.
    /// </summary>
    public bool IsPointAboveXZ(Vector3 worldPoint)
    {
        if (cachedCollider == null) return false;

        Bounds b = cachedCollider.bounds;
        return worldPoint.x >= b.min.x && worldPoint.x <= b.max.x
            && worldPoint.z >= b.min.z && worldPoint.z <= b.max.z;
    }

    #endregion

    // ============================================================
    // 디버그
    // ============================================================

    #region Debug

    /// <summary>씬 뷰에서 블록 상태를 색으로 시각화 (선택됐을 때만)</summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = currentState switch
        {
            BlockState.Visible => new Color(0.3f, 1f, 0.3f, 0.4f),
            BlockState.Hidden => new Color(1f, 0.3f, 0.3f, 0.4f),
            _ => new Color(1f, 1f, 0.3f, 0.4f)
        };

        // 런타임이 아닐 땐 현재 transform.position 기준
        Vector3 center = Application.isPlaying ? originalPosition : transform.position;
        Gizmos.DrawSphere(center + Vector3.up * 0.5f, 0.3f);
    }

    [ContextMenu("Test - Toggle")]
    private void DebugToggle() => Toggle();

    [ContextMenu("Test - Reset to Visible")]
    private void DebugReset() => SetVisibleImmediate();

    #endregion
}
