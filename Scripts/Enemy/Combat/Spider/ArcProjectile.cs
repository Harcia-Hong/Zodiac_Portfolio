// ArcProjectile.cs

using UnityEngine;

/// <summary>
/// 포물선 투사체
/// - 시작 지점에서 목표 지점까지 포물선 궤도로 이동
/// - 착탄 시 범위 데미지 판정 + VFX
/// - Enemy_Spider_Hybrid_Attack에서 생성하여 사용
/// </summary>
public class ArcProjectile : MonoBehaviour
{
    [Header("비행 설정")]
    [SerializeField, Tooltip("포물선 최대 높이")]
    private float arcHeight = 5f;

    [SerializeField, Tooltip("비행 시간 (초)")]
    private float flightDuration = 0.8f;

    [Header("착탄 설정")]
    [SerializeField, Tooltip("착탄 데미지")]
    private int damage = 20;

    [SerializeField, Tooltip("폭발 데미지 범위")]
    private float explosionRadius = 2.5f;

    [SerializeField, Tooltip("플레이어 레이어")]
    private LayerMask playerLayer;

    [Header("이펙트")]
    [SerializeField, Tooltip("착탄 시 폭발 VFX 프리팹")]
    private GameObject impactVFXPrefab;

    // 비행 상태
    private Vector3 startPos;
    private Vector3 targetPos;
    private float elapsed = 0f;
    private bool initialized = false;
    private bool hasLanded = false;

    /// <summary>
    /// 투사체 초기화 (외부에서 호출)
    /// </summary>
    /// <param name="start">발사 위치</param>
    /// <param name="target">착탄 위치 (텔레그래프 중심점)</param>
    /// <param name="dmg">데미지</param>
    /// <param name="radius">폭발 범위</param>
    /// <param name="height">포물선 높이</param>
    /// <param name="duration">비행 시간</param>
    public void Initialize(Vector3 start, Vector3 target, int dmg, float radius, float height = 5f, float duration = 0.8f)
    {
        startPos = start;
        targetPos = target;
        damage = dmg;
        explosionRadius = radius;
        arcHeight = height;
        flightDuration = duration;

        elapsed = 0f;
        hasLanded = false;
        initialized = true;

        // 시작 위치 설정
        transform.position = start;

        // 안전장치: 비행 시간 초과 시 자동 파괴
        Destroy(gameObject, flightDuration + 2f);
    }

    private void Update()
    {
        if (!initialized || hasLanded) return;

        elapsed += Time.deltaTime;
        float t = elapsed / flightDuration;

        if (t >= 1f)
        {
            // 착탄
            OnImpact();
            return;
        }

        // 포물선 위치 계산
        Vector3 currentPos = CalculateArcPosition(t);
        
        // 투사체가 궤도 방향을 바라보도록 회전
        Vector3 nextPos = CalculateArcPosition(Mathf.Min(t + 0.05f, 1f));
        Vector3 flyDirection = (nextPos - currentPos).normalized;

        if (flyDirection.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(flyDirection);

        transform.position = currentPos;
    }

    /// <summary>
    /// 포물선 궤도 위치 계산
    /// - XZ: 시작 → 목표 선형 보간
    /// - Y: Sin 곡선으로 포물선
    /// </summary>
    private Vector3 CalculateArcPosition(float t)
    {
        // XZ 선형 보간
        Vector3 pos = Vector3.Lerp(startPos, targetPos, t);

        // Y 포물선 (Sin 곡선: 0 → 최대높이 → 0)
        float yOffset = Mathf.Sin(t * Mathf.PI) * arcHeight;
        pos.y += yOffset;

        return pos;
    }

    /// <summary>
    /// 착탄 처리 — 범위 데미지 + VFX + 자기 파괴
    /// </summary>
    private void OnImpact()
    {
        hasLanded = true;

        // 최종 위치 확정
        transform.position = targetPos;

        // 범위 데미지 판정
        Collider[] hits = Physics.OverlapSphere(targetPos, explosionRadius, playerLayer);

        foreach (var hit in hits)
        {
            PlayerHealth playerHealth = hit.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage);
                Debug.Log($"[ArcProjectile] 착탄 데미지: {damage}");
                break; // 플레이어는 1명이니 1회만
            }
        }

        // 폭발 VFX 생성
        if (impactVFXPrefab != null)
        {
            GameObject vfx = Instantiate(impactVFXPrefab, targetPos, Quaternion.identity);
            Destroy(vfx, 3f);
        }

        // 투사체 파괴
        Destroy(gameObject);
    }

    #region 디버그

    private void OnDrawGizmosSelected()
    {
        if (!initialized) return;

        // 포물선 궤도 시각화
        Gizmos.color = Color.cyan;
        Vector3 prev = startPos;

        for (int i = 1; i <= 20; i++)
        {
            float t = i / 20f;
            Vector3 pos = CalculateArcPosition(t);
            Gizmos.DrawLine(prev, pos);
            prev = pos;
        }

        // 착탄 범위
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(targetPos, explosionRadius);
    }

    #endregion
}