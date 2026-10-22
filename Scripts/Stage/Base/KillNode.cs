using UnityEngine;

/// ============================================================
/// KillNode.cs — 적 처치 카운트로 활성화되는 노드 (NodeBase 상속)
/// ============================================================
/// Stage 1-2 NodeStage에서 사용되는 노드 타입 중 하나.
///
/// [작동 방식]
///   - Enemy.OnEnemyDeath 이벤트를 구독해서 적 처치 알림 수신
///   - requirePlayerInRange == true면 플레이어가 노드 영역 안에 있을 때만 카운트
///   - currentKillCount가 targetKillCount에 도달하면 100% → 완료 처리
///
/// [게이트키퍼식 디펜스]
///   플레이어가 노드 근처에 머물면서 싸워야 충전됨 →
///   4개 노드가 자연스럽게 동선 분산 효과를 가짐
///
/// [부착 위치]
///   - GameObject에 SphereCollider(Trigger) + KillNode 부착
///   - 색상 / 영역 반경 / targetKillCount는 인스펙터에서 설정
/// ============================================================
public class KillNode : NodeBase
{
    // ============================================================
    // 인스펙터 설정
    // ============================================================

    [Header("KillNode 설정")]
    [SerializeField, Tooltip("활성화에 필요한 적 처치 수")]
    private int targetKillCount = 5;

    [SerializeField, Tooltip("플레이어가 영역 안에 있을 때만 처치를 카운트할지 여부 " +
                             "(true = 게이트키퍼식 디펜스, false = 어디서 죽이든 카운트)")]
    private bool requirePlayerInRange = true;

    // ============================================================
    // 런타임 상태
    // ============================================================

    /// <summary>현재까지 카운트된 처치 수</summary>
    private int currentKillCount = 0;

    // ============================================================
    // 공개 프로퍼티 (UI / 디버깅용)
    // ============================================================

    public int CurrentKillCount => currentKillCount;
    public int TargetKillCount => targetKillCount;

    // ============================================================
    // Unity Lifecycle — 이벤트 구독 / 해제
    // ============================================================

    /// <summary>활성 시 Enemy 사망 이벤트 구독</summary>
    private void OnEnable()
    {
        Enemy.OnEnemyDeath += OnEnemyKilled;
    }

    /// <summary>비활성 시 이벤트 해제 (메모리 누수 방지)</summary>
    private void OnDisable()
    {
        Enemy.OnEnemyDeath -= OnEnemyKilled;
    }

    // ============================================================
    // 적 사망 콜백
    // ============================================================

    /// <summary>
    /// Enemy.OnEnemyDeath 이벤트 콜백.
    /// - 노드가 이미 완료됐으면 무시
    /// - requirePlayerInRange 옵션이 켜져 있고 플레이어가 영역 밖이면 무시
    /// - 그 외에는 처치 카운트 +1
    /// </summary>
    private void OnEnemyKilled()
    {
        if (IsCompleted) return;

        // 게이트키퍼 모드: 플레이어가 영역 안에 있을 때만 카운트
        if (requirePlayerInRange && !IsPlayerInRange) return;

        IncrementKill();
    }

    // ============================================================
    // 진행도 갱신
    // ============================================================

    /// <summary>
    /// NodeBase의 추상 메서드 구현.
    /// 외부에서 강제로 카운트를 1 올리고 싶을 때 호출 (디버그/치트용).
    /// </summary>
    public override void TickProgress()
    {
        IncrementKill();
    }

    /// <summary>
    /// 처치 카운트를 1 증가시키고 진행도 갱신.
    /// targetKillCount에 도달하면 SetProgress(1f)로 NodeBase가 자동 완료 처리.
    /// </summary>
    private void IncrementKill()
    {
        if (IsCompleted) return;

        currentKillCount = Mathf.Min(currentKillCount + 1, targetKillCount);

        // 정규화된 진행도 (0~1)로 NodeBase에 전달
        float progress = (float)currentKillCount / targetKillCount;
        SetProgress(progress);

        Debug.Log($"[KillNode] '{NodeName}' 처치 카운트: {currentKillCount}/{targetKillCount}");
    }
}
