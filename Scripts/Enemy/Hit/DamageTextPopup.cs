using UnityEngine;
using TMPro;
using DG.Tweening;

public class DamageTextPopup : MonoBehaviour
{
    #region Components
    [SerializeField] private TextMeshProUGUI damageText;
    #endregion

    #region Settings
    [Header("===== 애니메이션 설정 =====")]
    [SerializeField] private float floatHeight = 1f;      // 위로 떠오르는 높이
    [SerializeField] private float duration = 0.8f;       // 애니메이션 지속 시간
    [SerializeField] private float fadeStartRatio = 0.5f; // 페이드 시작 시점 (0~1)

    [Header("===== 크기 설정 =====")]
    [SerializeField] private float normalSize = 36f;      // 일반 데미지 폰트 크기
    [SerializeField] private float criticalSize = 54f;    // 크리티컬 폰트 크기

    [Header("===== 색상 설정 =====")]
    [SerializeField] private Color normalColor = Color.black;
    [SerializeField] private Color criticalColor = Color.yellow;
    #endregion

    #region Initialization
    /// <summary>
    /// 데미지 텍스트 초기화 및 애니메이션 시작
    /// </summary>
    /// <param name="damage">표시할 데미지 수치</param>
    /// <param name="isCritical">크리티컬 여부</param>
    public void Setup(int damage, bool isCritical)
    {
        // 텍스트 설정
        damageText.text = damage.ToString();

        // 크리티컬 여부에 따른 스타일 적용
        if (isCritical)
        {
            damageText.fontSize = criticalSize;
            damageText.color = criticalColor;
        }
        else
        {
            damageText.fontSize = normalSize;
            damageText.color = normalColor;
        }

        // 애니메이션 시작
        PlayAnimation();
    }
    #endregion

    #region Animation
    /// <summary>
    /// 위로 떠오르면서 페이드아웃 애니메이션
    /// </summary>
    private void PlayAnimation()
    {
        // 초기 스케일 펀치 효과 (크리티컬은 더 강하게)
        transform.localScale = Vector3.one * 0.5f;
        transform.DOScale(Vector3.one, 0.15f).SetEase(Ease.OutBack);

        // 위로 떠오르기
        transform.DOMoveY(transform.position.y + floatHeight, duration)
            .SetEase(Ease.OutQuad);

        // 페이드아웃 (중간부터 시작)
        damageText.DOFade(0f, duration * (1f - fadeStartRatio))
            .SetDelay(duration * fadeStartRatio)
            .SetEase(Ease.InQuad)
            .OnComplete(() => Destroy(gameObject));
    }
    #endregion
}
