using UnityEngine;

/// <summary>
/// 공격 텔레그래프 - 몬스터/보스 공격 전 범위 예고 표시
/// 
/// [기본 모드] 바닥에 Indicator를 생성 → 후반부 점멸 → 완료 콜백
/// [Fill 모드] 외곽 테두리 + 내부 Fill 원 조합
///             - 외곽: 최종 범위를 미리 보여줌 (고정 크기)
///             - 내부: Scale 0 → 1로 서서히 채워짐
///             - 채워지는 진행도(Progress)를 외부에서 읽을 수 있음
///             - 현월 보스 스타일의 "안쪽에서 차오르는" 긴장감 연출
/// </summary>
public class AttackTelegraph : MonoBehaviour
{
    /// <summary>텔레그래프 형태 종류</summary>
    public enum TelegraphType
    {
        Circle,   // 원형 (근거리)
        Arrow,    // 화살표 (원거리 투사체 방향)
        Cone      // 부채꼴 (혼합형 근거리)
    }

    [Header("Indicator Prefabs (외곽 테두리)")]
    [SerializeField] private GameObject circleIndicatorPrefab;   // Range Indicator
    [SerializeField] private GameObject arrowIndicatorPrefab;    // Arrow Indicator
    [SerializeField] private GameObject coneIndicatorPrefab;     // Cone Indicator

    [Header("Fill Mode 설정")]
    [Tooltip("Fill 모드 사용 여부 (false면 기존 점멸 방식)")]
    [SerializeField] private bool useFillMode = false;

    [Tooltip("Fill용 원형 프리팹 (안쪽에서 채워지는 원)")]
    [SerializeField] private GameObject circleFillPrefab;

    [Tooltip("Fill용 부채꼴 프리팹")]
    [SerializeField] private GameObject coneFillPrefab;

    [Tooltip("Fill용 직선 프리팹")]
    [SerializeField] private GameObject arrowFillPrefab;

    [Header("Telegraph Settings")]
    [Tooltip("텔레그래프 표시 시간 (이 시간 후 공격 시작)")]
    [SerializeField] private float telegraphDuration = 0.6f;

    [Tooltip("Indicator 바닥 오프셋 (땅에 붙도록)")]
    [SerializeField] private float groundOffset = 0.05f;

    [Header("Fill 시각 설정")]
    [Tooltip("외곽 테두리 알파값 (0~1)")]
    [Range(0f, 1f)]
    [SerializeField] private float borderAlpha = 0.3f;

    [Tooltip("내부 Fill 알파값 (0~1)")]
    [Range(0f, 1f)]
    [SerializeField] private float fillAlpha = 0.6f;

    // 내부 상태
    private GameObject currentIndicator;      // 외곽 테두리 인디케이터
    private GameObject currentFillIndicator;  // 내부 Fill 인디케이터
    private float timer;
    private float targetSize;                 // 최종 크기 (Fill 스케일 목표)
    private bool isShowing = false;
    private System.Action onTelegraphComplete;

    /// <summary>텔레그래프 표시 중인지</summary>
    public bool IsShowing => isShowing;

    /// <summary>텔레그래프 표시 시간</summary>
    public float Duration => telegraphDuration;

    /// <summary>
    /// 현재 텔레그래프 진행도 (0 = 시작, 1 = 완료)
    /// - 외부에서 읽어서 "75%일 때 보스 사라짐" 등 연동 가능
    /// </summary>
    public float Progress { get; private set; }

    private void Update()
    {
        if (!isShowing) return;

        timer -= Time.deltaTime;

        // 진행도 계산 (0 → 1)
        Progress = 1f - (timer / telegraphDuration);
        Progress = Mathf.Clamp01(Progress);

        // Fill 모드 vs 기존 점멸 모드
        if (useFillMode)
        {
            UpdateFillVisual(Progress);
        }
        else
        {
            UpdateBlinkVisual(Progress);
        }

        // 텔레그래프 시간 종료
        if (timer <= 0f)
        {
            Progress = 1f;
            System.Action callback = onTelegraphComplete;
            Hide();
            callback?.Invoke();
        }
    }

    #region Public Show Methods

    /// <summary>
    /// 원형 텔레그래프 표시 (근거리 공격용)
    /// </summary>
    public void ShowCircle(Vector3 center, float radius, System.Action onComplete = null)
    {
        if (circleIndicatorPrefab == null)
        {
            onComplete?.Invoke();
            return;
        }

        Show(circleIndicatorPrefab, circleFillPrefab, center, Vector3.forward, radius, onComplete);
    }

    /// <summary>
    /// 화살표 텔레그래프 표시 (원거리 공격용)
    /// </summary>
    public void ShowArrow(Vector3 origin, Vector3 direction, float length, System.Action onComplete = null)
    {
        if (arrowIndicatorPrefab == null)
        {
            onComplete?.Invoke();
            return;
        }

        Vector3 center = origin + direction * (length * 0.5f);
        Show(arrowIndicatorPrefab, arrowFillPrefab, center, direction, 1f, onComplete);

        // Arrow 전용 스케일 적용
        if (currentIndicator != null)
        {
            currentIndicator.transform.localScale = new Vector3(0.3f, length, 0.1f);
        }
        if (currentFillIndicator != null)
        {
            // Fill도 동일한 비율로 설정 (시작은 Z=0에서 커짐)
            currentFillIndicator.transform.localScale = new Vector3(0.3f, 0f, 0.1f);
        }
    }

    /// <summary>
    /// 부채꼴 텔레그래프 표시 (혼합형 근거리용)
    /// - origin에서 direction 방향으로 부채꼴이 펼쳐짐
    /// - 보스가 부채꼴 꼭짓점(뒤쪽)에 위치하도록 중심을 앞으로 오프셋
    /// </summary>
    public void ShowCone(Vector3 origin, Vector3 direction, float radius, System.Action onComplete = null)
    {
        if (coneIndicatorPrefab == null)
        {
            onComplete?.Invoke();
            return;
        }

        // 보스가 꼭짓점에 오도록 중심을 direction 방향으로 절반만큼 이동
        Vector3 center = origin + direction.normalized * (radius * 0.5f);

        Show(coneIndicatorPrefab, coneFillPrefab, center, direction, radius, onComplete);
    }

    #endregion

    #region Internal Show / Hide

    /// <summary>
    /// Indicator 생성 및 표시 시작
    /// - 외곽 테두리 + (Fill 모드 시) 내부 Fill 인디케이터 동시 생성
    /// </summary>
    private void Show(GameObject borderPrefab, GameObject fillPrefab,
        Vector3 position, Vector3 direction, float size, System.Action onComplete)
    {
        // 기존 정리
        Hide();

        // 바닥 위치
        Vector3 spawnPos = new Vector3(position.x, position.y + groundOffset, position.z);

        // 회전 계산 (Y축 회전)
        Quaternion rotation;
        if (direction != Vector3.zero)
        {
            Vector3 flatDir = direction;
            flatDir.y = 0;
            float angle = Mathf.Atan2(flatDir.x, flatDir.z) * Mathf.Rad2Deg;
            rotation = Quaternion.Euler(90f, angle, 0f);
        }
        else
        {
            rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        // 1. 외곽 테두리 생성 (size = 반지름이므로, 지름으로 변환하여 스케일 적용)
        currentIndicator = Instantiate(borderPrefab, spawnPos, rotation);
        currentIndicator.transform.localScale = Vector3.one * size * 2f;

        // Fill 모드: 외곽 테두리 알파 낮춤 + Fill 인디케이터 생성
        if (useFillMode)
        {
            SetIndicatorAlpha(currentIndicator, borderAlpha);

            // 2. 내부 Fill 인디케이터 생성 (Scale 0에서 시작)
            if (fillPrefab != null)
            {
                currentFillIndicator = Instantiate(fillPrefab, spawnPos, rotation);
                currentFillIndicator.transform.localScale = Vector3.zero;
                SetIndicatorAlpha(currentFillIndicator, fillAlpha);
            }
        }

        // 상태 설정
        targetSize = size;
        timer = telegraphDuration;
        isShowing = true;
        Progress = 0f;
        onTelegraphComplete = onComplete;
    }

    /// <summary>
    /// 텔레그래프 숨기기 및 정리
    /// </summary>
    public void Hide()
    {
        if (currentIndicator != null)
        {
            Destroy(currentIndicator);
            currentIndicator = null;
        }

        if (currentFillIndicator != null)
        {
            Destroy(currentFillIndicator);
            currentFillIndicator = null;
        }

        isShowing = false;
        Progress = 0f;
        onTelegraphComplete = null;
    }

    /// <summary>
    /// 즉시 취소 (피격 등으로 공격이 캔슬된 경우)
    /// </summary>
    public void Cancel()
    {
        Hide();
    }

    #endregion

    #region Visual Update

    /// <summary>
    /// [Fill 모드] 내부 Fill 원의 Scale을 진행도에 따라 키움
    /// - 0% → Scale 0 (안 보임)
    /// - 100% → targetSize (외곽과 동일)
    /// - 90% 이후 외곽 테두리도 점멸 추가 (긴박감)
    /// </summary>
    private void UpdateFillVisual(float progress)
    {
        if (currentFillIndicator != null)
        {
            // targetSize는 반지름이므로, 지름으로 변환
            float currentSize = Mathf.Lerp(0f, targetSize * 2f, progress);
            currentFillIndicator.transform.localScale = Vector3.one * currentSize;
        }

        if (progress > 0.9f && currentIndicator != null)
        {
            float blinkSpeed = 12f;
            float alpha = Mathf.Lerp(borderAlpha, 0.8f, Mathf.Abs(Mathf.Sin(Time.time * blinkSpeed)));
            SetIndicatorAlpha(currentIndicator, alpha);
        }
    }

    /// <summary>
    /// [기존 모드] 후반부 점멸 효과 (하위 호환)
    /// </summary>
    private void UpdateBlinkVisual(float progress)
    {
        if (currentIndicator == null) return;

        if (progress > 0.7f)
        {
            float blinkSpeed = Mathf.Lerp(4f, 12f, (progress - 0.7f) / 0.3f);
            float alpha = Mathf.Abs(Mathf.Sin(Time.time * blinkSpeed));

            Renderer[] renderers = currentIndicator.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                foreach (var mat in renderer.materials)
                {
                    if (mat.HasProperty("_Color"))
                    {
                        Color color = mat.color;
                        color.a = Mathf.Lerp(0.3f, 1f, alpha);
                        mat.color = color;
                    }
                }
            }
        }
    }

    #endregion

    #region Utility

    /// <summary>
    /// 인디케이터의 모든 렌더러 알파값 일괄 설정
    /// </summary>
    private void SetIndicatorAlpha(GameObject indicator, float alpha)
    {
        if (indicator == null) return;

        Renderer[] renderers = indicator.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.materials)
            {
                if (mat.HasProperty("_BaseColor"))
                {
                    // URP Lit/Unlit
                    Color color = mat.GetColor("_BaseColor");
                    color.a = alpha;
                    mat.SetColor("_BaseColor", color);
                }
                else if (mat.HasProperty("_Color"))
                {
                    // Built-in
                    Color color = mat.color;
                    color.a = alpha;
                    mat.color = color;
                }
            }
        }
    }

    /// <summary>
    /// 외부에서 Fill 모드를 런타임에 전환
    /// - BossPatternExecutor가 패턴 스텝별로 Fill 여부를 제어할 때 사용
    /// </summary>
    public void SetFillMode(bool enabled)
    {
        useFillMode = enabled;
    }

    private void OnDestroy()
    {
        Hide();
    }

    #endregion
}