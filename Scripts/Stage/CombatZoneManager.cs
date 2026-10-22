using UnityEngine;

/// ============================================================
/// CombatZone.cs — 전투 시작 트리거 + 중앙 관리자
/// ============================================================
/// 누적형 아레나에서 "플레이어가 전투 지역에 도착했을 때"
/// 페이즈 전투를 시작시키는 시스템.
///
/// 왜 LayoutZone과 분리되어 있는가:
///   - LayoutZone: Layout 전체 범위 감지 (스폰 위치 결정용)
///   - CombatZone: 전투 지역만 감지 (페이즈 시작 트리거)
///   - 분리 이유: Layout 안에 이동 구간(징검다리, 시작 지점)이 있어도
///     플레이어가 실제 전투 공간에 도착해야 전투가 시작되도록
///
/// 핵심 역할:
///   1. 플레이어 진입 감지 → 지정된 Phase Index로 전투 시작 신호 발행
///   2. 일회성 트리거 — 한 번 발동되면 비활성화 (재진입 시 재시작 X)
///   3. StageManager가 OnCombatZoneTriggered 이벤트 구독
///
/// 씬 세팅:
///   Layout_0/CombatZone_Phase0  (Phase Index = 0)
///   Layout_1/CombatZone_Phase1  (Phase Index = 1)
///   Layout_2/CombatZone_Phase2  (Phase Index = 2, 또는 보스)
///
///   각 CombatZone에 BoxCollider(isTrigger=true) + CombatZone 스크립트
/// ============================================================
public class CombatZoneManager : MonoBehaviour
{
    public static CombatZoneManager Instance { get; private set; }

    // ============================================================
    // 이벤트
    // ============================================================

    /// <summary>
    /// 플레이어가 전투 지역에 진입한 순간 발행 (int = phaseIndex)
    /// StageManager가 구독하여 해당 페이즈를 시작함
    /// </summary>
    public event System.Action<int> OnCombatZoneTriggered;

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
    // CombatZone으로부터의 통보 (내부용 API)
    // ============================================================

    #region Internal Notification API

    /// <summary>
    /// CombatZone이 플레이어 진입을 감지했을 때 통보
    /// - 이벤트 발행 → StageManager가 해당 페이즈 전투 시작
    /// </summary>
    public void NotifyZoneTriggered(int phaseIndex)
    {
        Debug.Log($"[CombatZoneManager] 전투 지역 트리거됨 → Phase {phaseIndex} 시작 신호 발행");
        OnCombatZoneTriggered?.Invoke(phaseIndex);
    }

    #endregion
}
