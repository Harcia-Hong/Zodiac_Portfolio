using UnityEngine;

/// ============================================================
/// LayoutSpawnData.cs — Layout별 스폰 포인트 관리
/// ============================================================
/// 각 Layout(Layout_0, 1, 2 등)에 부착되어 몬스터, 보상 오브젝트의
/// 스폰 위치를 관리한다. Layout이 솟아오를 때 스폰 포인트도 함께
/// 올라오므로 지형에 맞는 위치에 엔티티가 생성된다.
///
/// 구조 예시:
///   Layout_0 (이 컴포넌트 부착)
///     ├─ (바닥 타일들)
///     ├─ EnemySpawnPoints (가장자리 배치)
///     │    ├─ SpawnPoint_North
///     │    ├─ SpawnPoint_South
///     │    ├─ SpawnPoint_East
///     │    └─ SpawnPoint_West
///     ├─ RewardPoints
///     │    ├─ RewardPoint_0
///     │    ├─ RewardPoint_1
///     │    └─ RewardPoint_2
///     └─ PortalPoint (레거시, 미사용)
///
/// 사용:
///   MonsterSpanwer → GetRandomEnemySpawnPoint()로 스폰 위치 조회
///   StageManager  → GetRewardPoints()로 보상 스폰 위치 조회 (추후)
/// ============================================================
public class LayoutSpawnData : MonoBehaviour
{
    // ============================================================
    // 몬스터 스폰 포인트 (가장자리)
    // ============================================================

    [Header("몬스터 스폰 포인트 (가장자리)")]
    [Tooltip("이 Layout의 가장자리에 배치된 몬스터 스폰 위치들. " +
             "킬 나이트 스타일로 바닥 경계에서 기어오르는 연출에 사용")]
    [SerializeField] private Transform[] enemySpawnPoints;

    // ============================================================
    // 보상 오브젝트 스폰 포인트
    // ============================================================

    [Header("보상 오브젝트 스폰 포인트")]
    [Tooltip("이 Layout에서 보상 오브젝트가 스폰될 위치들")]
    [SerializeField] private Transform[] rewardSpawnPoints;

    // ============================================================
    // 포탈 스폰 포인트 (레거시 — 현재 미사용, 추후 재활용 가능)
    // ============================================================

    [Header("포탈 스폰 포인트 (레거시)")]
    [Tooltip("레거시 — 포탈 시스템 폐지됨. 추후 다른 용도로 재활용 가능")]
    [SerializeField] private Transform portalSpawnPoint;

    // ============================================================
    // 디버그
    // ============================================================

    [Header("디버그")]
    [SerializeField, Tooltip("씬 뷰에 스폰 포인트 표시")]
    private bool showGizmos = true;

    [SerializeField] private Color enemyGizmoColor = new Color(1f, 0.3f, 0.3f, 0.7f);
    [SerializeField] private Color rewardGizmoColor = new Color(0.3f, 1f, 0.3f, 0.7f);
    [SerializeField] private Color portalGizmoColor = new Color(0.3f, 0.5f, 1f, 0.7f);

    // ============================================================
    // 몬스터 스폰 포인트 API
    // ============================================================

    #region Enemy Spawn Points

    /// <summary>이 Layout의 몬스터 스폰 포인트 배열 반환</summary>
    public Transform[] GetEnemySpawnPoints() => enemySpawnPoints;

    /// <summary>몬스터 스폰 포인트 개수</summary>
    public int GetEnemySpawnPointCount()
    {
        return enemySpawnPoints != null ? enemySpawnPoints.Length : 0;
    }

    /// <summary>
    /// 몬스터 스폰 포인트 중 랜덤으로 1개 반환
    /// MonsterSpanwer가 스폰 시 호출
    /// </summary>
    public Transform GetRandomEnemySpawnPoint()
    {
        if (enemySpawnPoints == null || enemySpawnPoints.Length == 0)
            return null;

        return enemySpawnPoints[Random.Range(0, enemySpawnPoints.Length)];
    }

    /// <summary>특정 인덱스의 몬스터 스폰 포인트 반환</summary>
    public Transform GetEnemySpawnPoint(int index)
    {
        if (enemySpawnPoints == null) return null;
        if (index < 0 || index >= enemySpawnPoints.Length) return null;
        return enemySpawnPoints[index];
    }

    #endregion

    // ============================================================
    // 보상 스폰 포인트 API (기존 유지)
    // ============================================================

    #region Reward Spawn Points

    /// <summary>이 Layout의 보상 스폰 포인트 배열 반환</summary>
    public Transform[] GetRewardPoints() => rewardSpawnPoints;

    /// <summary>보상 스폰 포인트 개수</summary>
    public int GetRewardPointCount()
    {
        return rewardSpawnPoints != null ? rewardSpawnPoints.Length : 0;
    }

    /// <summary>특정 인덱스의 보상 스폰 포인트 반환</summary>
    public Transform GetRewardPoint(int index)
    {
        if (rewardSpawnPoints == null) return null;
        if (index < 0 || index >= rewardSpawnPoints.Length) return null;
        return rewardSpawnPoints[index];
    }

    #endregion

    // ============================================================
    // 포탈 스폰 포인트 API (레거시)
    // ============================================================

    #region Portal Spawn Point (Legacy)

    /// <summary>[레거시] 포탈 스폰 포인트 반환</summary>
    public Transform GetPortalPoint() => portalSpawnPoint;

    #endregion

    // ============================================================
    // 기즈모 (씬 뷰 시각화)
    // ============================================================

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // 몬스터 스폰 포인트 (빨강)
        DrawSpawnPointGizmos(enemySpawnPoints, enemyGizmoColor, 0.5f);

        // 보상 스폰 포인트 (초록)
        DrawSpawnPointGizmos(rewardSpawnPoints, rewardGizmoColor, 0.4f);

        // 포탈 스폰 포인트 (파랑)
        if (portalSpawnPoint != null)
        {
            Gizmos.color = portalGizmoColor;
            Gizmos.DrawSphere(portalSpawnPoint.position, 0.6f);
            Gizmos.DrawLine(portalSpawnPoint.position, portalSpawnPoint.position + Vector3.up * 2f);
        }
    }

    /// <summary>스폰 포인트 배열을 기즈모로 그리는 공용 메서드</summary>
    private void DrawSpawnPointGizmos(Transform[] points, Color color, float radius)
    {
        if (points == null) return;

        Gizmos.color = color;
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] == null) continue;

            Gizmos.DrawSphere(points[i].position, radius);
            Gizmos.DrawLine(points[i].position, points[i].position + Vector3.up * 1.5f);
        }
    }

    #endregion
}
