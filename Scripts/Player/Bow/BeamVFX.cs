using UnityEngine;

/// <summary>
/// 즉시 빔 시각화 — LineRenderer로 두 점 사이 빛 줄기 표시.
/// Hitscan 사격 시 인스턴스화되어 짧은 시간 표시 후 자동 소멸.
/// 데미지 처리는 PlayerArrowAttack에서 Raycast로 별도 처리하고,
/// 이 컴포넌트는 순수히 시각 효과만 담당.
/// 
/// 사용 흐름:
///   1) PlayerArrowAttack가 빔 프리팹 Instantiate
///   2) BeamVFX.Show(start, end) 호출 → LineRenderer 위치 셋팅
///   3) displayDuration 동안 페이드아웃 → Destroy
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class BeamVFX : MonoBehaviour
{
    [Header("Beam 표시 설정")]
    [SerializeField, Tooltip("빔이 표시되는 전체 시간 (초). 보통 0.1~0.15초가 임팩트 있음")]
    private float displayDuration = 0.12f;

    [SerializeField, Range(0f, 1f), Tooltip("페이드 시작 비율. 0=즉시 페이드, 0.5=중간부터 페이드, 1=마지막에 페이드")]
    private float fadeStartRatio = 0.4f;

    // ============================================================
    // Internal State
    // ============================================================
    private LineRenderer lineRenderer;
    private float elapsed = 0f;
    private Color startColorOriginal;
    private Color endColorOriginal;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer != null)
        {
            // 원본 색상 백업 (페이드 시 복원 기준)
            startColorOriginal = lineRenderer.startColor;
            endColorOriginal = lineRenderer.endColor;
        }
    }

    /// <summary>
    /// 두 점 사이 빔 표시 시작.
    /// PlayerArrowAttack.FireArrow에서 호출.
    /// </summary>
    /// <param name="start">발사 위치 (firePoint)</param>
    /// <param name="end">적중 위치 (Raycast hit) 또는 최대 사거리 위치</param>
    public void Show(Vector3 start, Vector3 end)
    {
        if (lineRenderer == null) return;

        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
        elapsed = 0f;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        // 페이드아웃 처리
        float lifeRatio = elapsed / displayDuration;
        if (lifeRatio > fadeStartRatio && lineRenderer != null)
        {
            // fadeStartRatio 이후부터 페이드
            float fadeProgress = (lifeRatio - fadeStartRatio) / (1f - fadeStartRatio);
            float alpha = Mathf.Clamp01(1f - fadeProgress);

            Color s = startColorOriginal; s.a *= alpha;
            Color e = endColorOriginal; e.a *= alpha;
            lineRenderer.startColor = s;
            lineRenderer.endColor = e;
        }

        // 표시 시간 종료 → 소멸
        if (elapsed >= displayDuration)
        {
            Destroy(gameObject);
        }
    }
}
