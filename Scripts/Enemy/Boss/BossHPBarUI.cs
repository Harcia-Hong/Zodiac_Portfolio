using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// 보스 전용 체력바 UI
/// 
/// 기능:
/// - 이름/칭호 표시
/// - 체력바 + 데미지 잔상바 (부드러운 감소 연출)
/// - 페이즈 경계선 마커 자동 생성
/// - 체력 수치 텍스트 (현재/최대)
/// - 페이즈 전환 시 강조 효과
/// 
/// 구조 (Canvas 하위):
///   BossHPBar
///   ├── NameText (보스 이름)
///   ├── TitleText (보스 칭호)
///   ├── HPBarBackground (배경)
///   │   ├── DamageBar (잔상바, 흰색/노란색)
///   │   ├── HPBar (실제 체력바, 빨간색)
///   │   └── PhaseMarkerContainer (경계선 부모)
///   └── HPText (수치 텍스트)
/// </summary>
public class BossHPBarUI : MonoBehaviour
{
    #region UI 참조

    [Header("텍스트")]
    [SerializeField, Tooltip("보스 이름 텍스트")]
    private TextMeshProUGUI nameText;

    [SerializeField, Tooltip("보스 칭호 텍스트")]
    private TextMeshProUGUI titleText;

    [SerializeField, Tooltip("체력 수치 텍스트 (현재/최대)")]
    private TextMeshProUGUI hpText;

    [Header("체력바")]
    [SerializeField, Tooltip("실제 체력바 (빨간색)")]
    private Image hpFillImage;

    [SerializeField, Tooltip("데미지 잔상바 (천천히 줄어듦)")]
    private Image damageFillImage;

    [SerializeField, Tooltip("체력바 전체 영역 (경계선 위치 계산용)")]
    private RectTransform hpBarRect;

    [Header("페이즈 경계선")]
    [SerializeField, Tooltip("경계선 마커 프리팹 (세로 선 이미지)")]
    private GameObject phaseMarkerPrefab;

    [SerializeField, Tooltip("경계선 마커 부모 Transform")]
    private RectTransform phaseMarkerContainer;

    [Header("연출 설정")]
    [SerializeField, Tooltip("체력바 감소 속도")]
    private float hpLerpSpeed = 5f;

    [SerializeField, Tooltip("잔상바 감소 딜레이 (초)")]
    private float damageBarDelay = 0.5f;

    [SerializeField, Tooltip("잔상바 감소 속도")]
    private float damageBarLerpSpeed = 2f;

    #endregion

    #region 내부 변수

    // 체력 추적
    private int maxHealth;
    private int currentHealth;
    private float displayedHP;          // 체력바 표시용 (부드러운 보간)
    private float displayedDamageHP;    // 잔상바 표시용
    private bool isDamageBarWaiting;    // 잔상바 딜레이 중 여부

    // 페이즈 마커 목록
    private List<GameObject> phaseMarkers = new List<GameObject>();

    // 잔상바 코루틴
    private Coroutine damageBarCoroutine;

    #endregion

    #region 초기화

    /// <summary>
    /// 보스 HP바 초기화 (Boss.cs에서 호출)
    /// - 이름/칭호 설정, 체력바 초기화, 페이즈 경계선 생성
    /// </summary>
    /// <param name="bossData">보스 데이터 SO</param>
    /// <param name="bossMaxHealth">보스 최대 체력</param>
    public void Initialize(BossDataSO bossData, int bossMaxHealth)
    {
        maxHealth = bossMaxHealth;
        currentHealth = bossMaxHealth;
        displayedHP = 1f;
        displayedDamageHP = 1f;

        // 이름/칭호 설정
        if (nameText != null)
            nameText.text = bossData.bossName;

        if (titleText != null)
            titleText.text = !string.IsNullOrEmpty(bossData.bossTitle)
                ? $"< {bossData.bossTitle} >"
                : "";

        // 체력 수치 텍스트
        UpdateHPText();

        // 체력바 초기값
        if (hpFillImage != null)
            hpFillImage.fillAmount = 1f;

        if (damageFillImage != null)
            damageFillImage.fillAmount = 1f;

        // 페이즈 경계선 생성
        CreatePhaseMarkers(bossData);

        // UI 활성화
        gameObject.SetActive(true);

        Debug.Log($"[BossHPBarUI] 초기화 완료 - {bossData.bossName} (HP: {bossMaxHealth})");
    }

    #endregion

    #region 체력 업데이트

    /// <summary>
    /// 체력 업데이트 (Boss.cs에서 매 프레임 호출)
    /// </summary>
    /// <param name="curHealth">현재 체력</param>
    public void UpdateHP(int curHealth)
    {
        int previousHealth = currentHealth;
        currentHealth = curHealth;

        // 데미지를 받았을 때만 잔상바 딜레이 시작
        if (curHealth < previousHealth)
        {
            TriggerDamageBar();
        }

        // 체력 수치 텍스트 갱신
        UpdateHPText();
    }

    private void Update()
    {
        // 실제 체력바 부드러운 감소
        float targetRatio = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;

        displayedHP = Mathf.Lerp(displayedHP, targetRatio, Time.deltaTime * hpLerpSpeed);
        if (hpFillImage != null)
            hpFillImage.fillAmount = displayedHP;

        // 잔상바 감소 (딜레이 후)
        if (!isDamageBarWaiting)
        {
            displayedDamageHP = Mathf.Lerp(displayedDamageHP, targetRatio, Time.deltaTime * damageBarLerpSpeed);
            if (damageFillImage != null)
                damageFillImage.fillAmount = displayedDamageHP;
        }
    }

    /// <summary>
    /// 잔상바 딜레이 트리거
    /// 데미지 후 일정 시간 뒤에 잔상바가 줄어들기 시작
    /// </summary>
    private void TriggerDamageBar()
    {
        if (damageBarCoroutine != null)
            StopCoroutine(damageBarCoroutine);

        damageBarCoroutine = StartCoroutine(DamageBarDelayCoroutine());
    }

    /// <summary>
    /// 잔상바 감소 딜레이 코루틴
    /// </summary>
    private IEnumerator DamageBarDelayCoroutine()
    {
        isDamageBarWaiting = true;
        yield return new WaitForSeconds(damageBarDelay);
        isDamageBarWaiting = false;
    }

    /// <summary>
    /// 체력 수치 텍스트 갱신
    /// </summary>
    private void UpdateHPText()
    {
        if (hpText != null)
        {
            hpText.text = $"{Mathf.Max(0, currentHealth):N0} / {maxHealth:N0}";
        }
    }

    #endregion

    #region 페이즈 경계선

    /// <summary>
    /// 페이즈 경계선 마커 자동 생성
    /// BossDataSO의 phases 배열에서 healthThreshold 읽어서 위치 계산
    /// </summary>
    private void CreatePhaseMarkers(BossDataSO bossData)
    {
        // 기존 마커 제거
        ClearPhaseMarkers();

        if (bossData.phases == null || phaseMarkerPrefab == null || hpBarRect == null)
            return;

        float barWidth = hpBarRect.rect.width;

        // 첫 페이즈(100%)는 경계선 불필요, 1번 인덱스부터 생성
        for (int i = 1; i < bossData.phases.Length; i++)
        {
            float threshold = bossData.phases[i].healthThreshold;

            // 체력바 내 위치 계산 (왼쪽 = 0%, 오른쪽 = 100%)
            float xPos = threshold * barWidth;

            // 마커 생성
            GameObject marker = Instantiate(phaseMarkerPrefab, phaseMarkerContainer);
            RectTransform markerRect = marker.GetComponent<RectTransform>();

            if (markerRect != null)
            {
                // 왼쪽 기준 앵커에서 xPos만큼 오른쪽
                markerRect.anchoredPosition = new Vector2(xPos, 0f);
            }

            phaseMarkers.Add(marker);
        }

        Debug.Log($"[BossHPBarUI] 페이즈 경계선 {phaseMarkers.Count}개 생성");
    }

    /// <summary>
    /// 기존 페이즈 마커 전부 제거
    /// </summary>
    private void ClearPhaseMarkers()
    {
        foreach (var marker in phaseMarkers)
        {
            if (marker != null)
                Destroy(marker);
        }
        phaseMarkers.Clear();
    }

    #endregion

    #region 페이즈 전환 연출

    /// <summary>
    /// 페이즈 전환 시 경계선 강조 효과 (외부 호출용)
    /// </summary>
    public void OnPhaseTransition(int newPhaseIndex)
    {
        // 해당 경계선 마커 강조 (깜빡임 등)
        int markerIndex = newPhaseIndex - 1;
        if (markerIndex >= 0 && markerIndex < phaseMarkers.Count)
        {
            StartCoroutine(FlashMarker(phaseMarkers[markerIndex]));
        }
    }

    /// <summary>
    /// 경계선 마커 깜빡임 연출
    /// </summary>
    private IEnumerator FlashMarker(GameObject marker)
    {
        if (marker == null) yield break;

        Image markerImage = marker.GetComponent<Image>();
        if (markerImage == null) yield break;

        Color originalColor = markerImage.color;
        Color flashColor = Color.white;

        // 3회 깜빡임
        for (int i = 0; i < 3; i++)
        {
            markerImage.color = flashColor;
            yield return new WaitForSeconds(0.15f);
            markerImage.color = originalColor;
            yield return new WaitForSeconds(0.15f);
        }
    }

    #endregion

    #region UI 제어

    /// <summary>
    /// HP바 표시
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
    }

    /// <summary>
    /// HP바 숨김
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        ClearPhaseMarkers();
    }

    #endregion
}