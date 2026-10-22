using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshPro 사용

public class SkillCooldownSlot : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Image cooldownOverlay; // 검은색 반투명 이미지 (Radial 360)
    [SerializeField] private TextMeshProUGUI timeText; // 남은 시간 텍스트

    private float cooldownDuration;
    private float currentCooldownTime;
    private bool isCoolingDown = false;

    private void Awake()
    {
        // 시작할 때 초기화
        if (cooldownOverlay != null) cooldownOverlay.fillAmount = 0f;
        if (timeText != null) timeText.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!isCoolingDown) return;

        currentCooldownTime -= Time.deltaTime;

        // 1. Overlay 갱신 (남은 비율만큼 채우기)
        if (cooldownOverlay != null)
        {
            cooldownOverlay.fillAmount = currentCooldownTime / cooldownDuration;
        }

        // 2. 텍스트 갱신
        if (timeText != null)
        {
            // [수정됨] 0보다 크면 무조건 소수점 1자리까지 표시 (예: 5.0, 4.3, 0.5)
            if (currentCooldownTime > 0f)
                timeText.text = currentCooldownTime.ToString("F1");
            else
                timeText.text = "";
        }

        // 3. 쿨타임 종료 체크
        if (currentCooldownTime <= 0f)
        {
            EndCooldown();
        }
    }

    /// <summary>
    /// 외부에서 이 함수를 호출하면 쿨타임 UI 시작
    /// </summary>
    /// <param name="duration">쿨타임 시간(초)</param>
    public void StartCooldown(float duration)
    {
        cooldownDuration = duration;
        currentCooldownTime = duration;
        isCoolingDown = true;

        if (cooldownOverlay != null) cooldownOverlay.fillAmount = 1f;
        if (timeText != null)
        {
            timeText.gameObject.SetActive(true);
            timeText.text = duration.ToString();
        }
    }

    private void EndCooldown()
    {
        isCoolingDown = false;
        currentCooldownTime = 0f;

        if (cooldownOverlay != null) cooldownOverlay.fillAmount = 0f;
        if (timeText != null) timeText.gameObject.SetActive(false);
    }
}
