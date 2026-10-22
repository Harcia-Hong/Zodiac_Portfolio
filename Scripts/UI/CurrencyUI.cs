using UnityEngine;
using TMPro;

/// <summary>
/// 재화 UI 관리
/// 골드, 별빛 조각 표시 및 업데이트
/// </summary>
public class CurrencyUI : MonoBehaviour
{
    #region UI References
    [Header("골드 UI")]
    [SerializeField] private TextMeshProUGUI goldText;

    [Header("별빛 조각 UI")]
    [SerializeField] private TextMeshProUGUI starText;
    #endregion

    #region Cached Values
    private int lastGold = -1;
    private int lastStar = -1;
    #endregion

    #region Unity Lifecycle
    private void Update()
    {
        UpdateCurrencyDisplay();
    }
    #endregion

    #region UI Update
    /// <summary>
    /// 재화 표시 업데이트 (값 변경 시에만)
    /// </summary>
    private void UpdateCurrencyDisplay()
    {
        if (PlayerStats.Instance == null) return;

        // 골드 업데이트
        int currentGold = PlayerStats.Instance.Coin;
        if (currentGold != lastGold)
        {
            goldText.text = FormatNumber(currentGold);
            lastGold = currentGold;
        }

        // 별빛 조각 업데이트
        int currentStar = PlayerStats.Instance.Stardust;
        if (currentStar != lastStar)
        {
            starText.text = FormatNumber(currentStar);
            lastStar = currentStar;
        }
    }

    /// <summary>
    /// 숫자 포맷팅 (1000 이상이면 콤마 추가)
    /// </summary>
    private string FormatNumber(int value)
    {
        return value.ToString("N0");
    }
    #endregion
}
