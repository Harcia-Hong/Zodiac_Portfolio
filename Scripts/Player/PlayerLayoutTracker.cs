using System.Collections.Generic;
using UnityEngine;

/// ============================================================
/// PlayerLayoutTracker.cs — 플레이어 Layout 위치 추적기 + Layout 트리거 존
/// ============================================================
/// 누적형 아레나 구조에서 "플레이어가 현재 어느 Layout에 있는가?"를
/// 실시간 추적하는 시스템.
///
/// 핵심 역할:
///   1. 플레이어가 새 Layout에 진입하면 OnPlayerEnteredLayout 이벤트 발행
///      → StageManager가 구독하여 다음 페이즈 시작 트리거로 사용
///   2. 플레이어가 Layout을 떠나면 OnPlayerExitedLayout 이벤트 발행
///   3. MonsterSpawner가 "플레이어가 있는 Layout의 가장자리"에서 스폰하기 위해
///      가장 최근 진입 Layout 인덱스 제공
///
/// 구조:
///   PlayerLayoutTracker (씬에 1개, 싱글톤) — 중앙 관리자
///   LayoutZone (각 Layout 루트에 1개씩) — 트리거 콜라이더로 감지
///
/// 씬 세팅:
///   Layout_0 루트에 BoxCollider(isTrigger) + LayoutZone(layoutIndex=0)
///   Layout_1 루트에 BoxCollider(isTrigger) + LayoutZone(layoutIndex=1)
///   ...
///   Player 오브젝트에 "Player" 태그 필수
///
/// 주의: Center(Floor)는 항상 활성이므로 추적 대상에서 제외
///       (필요하면 Center에도 LayoutZone(layoutIndex=-1)로 별도 처리 가능)
/// ============================================================
public class PlayerLayoutTracker : MonoBehaviour
{
    public static PlayerLayoutTracker Instance { get; private set; }

    // ============================================================
    // 이벤트
    // ============================================================

    /// <summary>
    /// 플레이어가 특정 Layout에 진입한 순간 발행 (int = layoutIndex)
    /// StageManager에서 "대기 중인 다음 Layout에 플레이어가 왔는가?" 판정에 사용
    /// </summary>
    public event System.Action<int> OnPlayerEnteredLayout;

    /// <summary>
    /// 플레이어가 특정 Layout을 벗어난 순간 발행 (int = layoutIndex)
    /// </summary>
    public event System.Action<int> OnPlayerExitedLayout;

    // ============================================================
    // 런타임 상태
    // ============================================================

    /// <summary>
    /// 플레이어가 현재 걸쳐있는 Layout 인덱스 집합
    /// (경계에 있거나 두 Layout에 동시에 발이 걸쳐있을 때를 대비)
    /// </summary>
    private readonly HashSet<int> currentLayouts = new HashSet<int>();

    /// <summary>가장 최근에 진입한 Layout 인덱스 (-1 = 아직 어떤 Layout에도 없음)</summary>
    private int latestEnteredIndex = -1;

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
    // LayoutZone으로부터의 통보 (내부용 API)
    // ============================================================

    #region Internal Notification API

    /// <summary>
    /// 플레이어가 특정 Layout에 진입했음을 LayoutZone이 통보
    /// - 이미 진입 상태인 경우는 중복 이벤트 발행 안 함 (복수 콜라이더 대비)
    /// </summary>
    public void NotifyEnter(int layoutIndex)
    {
        // 이미 들어와 있으면 스킵
        if (currentLayouts.Contains(layoutIndex)) return;

        currentLayouts.Add(layoutIndex);
        latestEnteredIndex = layoutIndex;

        OnPlayerEnteredLayout?.Invoke(layoutIndex);

        Debug.Log($"[PlayerLayoutTracker] 플레이어 → Layout_{layoutIndex} 진입 " +
                  $"(현재 걸친 Layout: {currentLayouts.Count}개)");
    }

    /// <summary>
    /// 플레이어가 특정 Layout을 벗어났음을 LayoutZone이 통보
    /// </summary>
    public void NotifyExit(int layoutIndex)
    {
        if (!currentLayouts.Contains(layoutIndex)) return;

        currentLayouts.Remove(layoutIndex);

        // 이탈한 게 최신이었다면, 남은 중에 하나로 갱신 (없으면 -1)
        if (latestEnteredIndex == layoutIndex)
        {
            latestEnteredIndex = GetAnyCurrentLayout();
        }

        OnPlayerExitedLayout?.Invoke(layoutIndex);

        Debug.Log($"[PlayerLayoutTracker] 플레이어 ← Layout_{layoutIndex} 이탈 " +
                  $"(현재 걸친 Layout: {currentLayouts.Count}개)");
    }

    #endregion

    // ============================================================
    // 외부 쿼리 인터페이스
    // ============================================================

    #region Public Query

    /// <summary>플레이어가 특정 Layout에 현재 있는지 여부</summary>
    public bool IsPlayerInLayout(int layoutIndex)
    {
        return currentLayouts.Contains(layoutIndex);
    }

    /// <summary>플레이어가 어떤 Layout이라도 걸쳐있는지</summary>
    public bool IsPlayerInAnyLayout()
    {
        return currentLayouts.Count > 0;
    }

    /// <summary>
    /// 가장 최근 진입한 Layout 인덱스 (-1 = 어느 Layout에도 없음)
    /// MonsterSpawner가 스폰 대상 Layout 선정에 사용
    /// </summary>
    public int GetLatestEnteredLayout()
    {
        return latestEnteredIndex;
    }

    /// <summary>플레이어가 걸쳐있는 Layout 개수</summary>
    public int GetCurrentLayoutCount()
    {
        return currentLayouts.Count;
    }

    #endregion

    // ============================================================
    // 내부 유틸리티
    // ============================================================

    #region Internal Utility

    /// <summary>currentLayouts 중 임의의 하나를 반환 (없으면 -1)</summary>
    private int GetAnyCurrentLayout()
    {
        foreach (int idx in currentLayouts)
            return idx;
        return -1;
    }

    #endregion

    // ============================================================
    // 디버그
    // ============================================================

    #region Debug

    [ContextMenu("현재 상태 출력")]
    private void LogCurrentStatus()
    {
        Debug.Log("===== PlayerLayoutTracker 상태 =====");
        Debug.Log($"걸친 Layout 수: {currentLayouts.Count}");
        Debug.Log($"최근 진입: Layout_{latestEnteredIndex}");

        foreach (int idx in currentLayouts)
            Debug.Log($"  - Layout_{idx}");
    }

    #endregion
}
