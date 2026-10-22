using UnityEngine;
using TMPro;

public class PlayerStatsUI : MonoBehaviour
{
    [Header("텍스트 연결 (Inspector)")]
    [SerializeField] private TextMeshProUGUI hpText;         // 체력
    [SerializeField] private TextMeshProUGUI atkText;        // 공격력
    [SerializeField] private TextMeshProUGUI spText;         // 주문력 (Spell Power)
    [SerializeField] private TextMeshProUGUI asText;         // 공격속도
    [SerializeField] private TextMeshProUGUI criText;        // 치명타
    [SerializeField] private TextMeshProUGUI cdrText;        // 쿨타임 감소
    [SerializeField] private TextMeshProUGUI spdText;        // 이동속도

    private void Start()
    {
        // 플레이어 스탯이 변경될 때마다 내 UI도 갱신해달라고 구독
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.OnStatsChanged += UpdateUI;
            UpdateUI(); // 시작할 때 한 번 갱신
        }
    }

    private void OnEnable()
    {
        // 패널이 켜질 때마다 최신 값으로 갱신
        UpdateUI();
    }

    private void OnDestroy()
    {
        // 게임 꺼질 때 구독 해제 (에러 방지)
        if (PlayerStats.Instance != null)
            PlayerStats.Instance.OnStatsChanged -= UpdateUI;
    }

    // 실제 값을 텍스트에 꽂아넣는 함수
    public void UpdateUI()
    {
        if (PlayerStats.Instance == null) return;

        var stats = PlayerStats.Instance;

        if (hpText != null) hpText.text = $"{stats.CurrentHP} / {stats.MaxHP}";
        if (atkText != null) atkText.text = $"{stats.AttackPower}";
        if (spText != null) spText.text = $"{stats.SpellPower}";
        if (asText != null) asText.text = $"{stats.AttackSpeed:F2}"; // 소수점 2자리
        if (criText != null) criText.text = $"{stats.CriticalRate:F0}%"; // 퍼센트
        if (cdrText != null) cdrText.text = $"{stats.CooldownReduction:F0}";
        if (spdText != null) spdText.text = $"{stats.MoveSpeed:F1}";
    }
}
