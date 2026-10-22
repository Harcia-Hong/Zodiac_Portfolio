using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CastBarUI : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Image fillImage;        // 파란색 게이지
    [SerializeField] private TextMeshProUGUI nameText; // 스킬 이름 텍스트
    [SerializeField] private GameObject panelObject; // 껐다 켰다 할 부모 오브젝트 (CastBar_Panel)

    [Header("Settings")]
    [SerializeField] private Gradient chargeColorGradient; // 단계별 색깔 변화 설정

    // 상태 변수
    private bool isActive = false;
    private bool isChargingMode = false; // true: 차오름, false: 줄어듦
    private float currentTimer = 0f;
    private float maxDuration = 1f;

    private int totalStages = 1;

    private void Awake()
    {
        // 시작할 때 꺼두기
        if (panelObject != null) panelObject.SetActive(false);
    }

    /// <summary>
    /// 캐스팅/차징 바를 시작합니다.
    /// </summary>
    /// <param name="skillName">스킬 이름</param>
    /// <param name="duration">지속/차징 시간</param>
    /// <param name="isCharging">true면 0->1로 차오름, false면 1->0으로 줄어듦</param>
    public void Show(string skillName, float duration, bool isCharging, int stages = 1)
    {
        isActive         = true;
        isChargingMode   = isCharging;
        maxDuration      = duration;
        currentTimer     = isCharging ? 0f : duration; // 차징은 0부터, 채널링은 끝부터 시작

        totalStages = Mathf.Max(1, stages);

        if (nameText != null) nameText.text = skillName;
        if (panelObject != null) panelObject.SetActive(true);

        UpdateBarVisual(isCharging ? 0f : 1f); // 시작 시각화 초기화
    }

    public void Hide()
    {
        isActive = false;
        if (panelObject != null) panelObject.SetActive(false);
    }

    private void Update()
    {
        if (!isActive) return;

        // 1. 시간 계산
        if (isChargingMode)
        {
            // 차징: 시간이 늘어남 (0 -> Max)
            currentTimer += Time.deltaTime;
            if (currentTimer >= maxDuration) currentTimer = maxDuration;
        }
        else
        {
            // 채널링: 시간이 줄어듦 (Max -> 0)
            currentTimer -= Time.deltaTime;
            if (currentTimer <= 0)
            {
                currentTimer = 0;
                Hide(); // 시간 다 되면 자동으로 꺼짐
            }
        }

        // 2. 비율 계산 (0.0 ~ 1.0)
        float ratio = Mathf.Clamp01(currentTimer / maxDuration);

        // 3. UI 업데이트
        UpdateBarVisual(ratio);
    }

    private void UpdateBarVisual(float totalRatio)
    {
        if (fillImage != null)
        {
            // 1. 현재 몇 번째 단계인지 계산 (0 ~ totalStages-1)
            // 예: 3단계일 때, 진행률 0.1 -> 0.3 (0단계의 30%)
            // 진행률 0.4 -> 1.2 (1단계의 20%)
            float floatStage = totalRatio * totalStages;
            int stageIndex = Mathf.Clamp(Mathf.FloorToInt(floatStage), 0, totalStages - 1);

            // 2. 현재 단계 내에서의 진행률 (0.0 ~ 1.0)
            float stageRatio = floatStage - stageIndex;

            // 꽉 찼을 때(100%) 처리
            if (totalRatio >= 1f)
            {
                stageIndex = totalStages - 1;
                stageRatio = 1f;
            }

            fillImage.fillAmount = stageRatio;

            // 3. 단계별 색상 적용 (Gradient 활용)
            if (chargeColorGradient != null)
            {
                // 단계가 여러 개면, 단계 인덱스에 따라 색상을 가져옴
                // 예: 3단계면 0(시작색), 0.5(중간색), 1(끝색) 위치의 색을 가져옴
                float colorTime = (totalStages > 1)
                    ? (float)stageIndex / (totalStages - 1)
                    : totalRatio; // 1단계면 그냥 진행률 따라감

                fillImage.color = chargeColorGradient.Evaluate(colorTime);
            }
        }
    }
}
