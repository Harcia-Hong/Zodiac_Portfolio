using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 사망 UI
/// - 플레이어 사망 시 화면에 표시
/// - "서재로 귀환" 버튼으로 거점 복귀
/// - 런 데이터 리셋 후 Base 씬으로 이동
/// </summary>
public class DeathUI : MonoBehaviour
{
    public static DeathUI Instance { get; private set; }

    [Header("UI")]
    [SerializeField, Tooltip("사망 UI 전체 패널")]
    private GameObject deathPanel;

    [SerializeField, Tooltip("사망 메시지 텍스트")]
    private TextMeshProUGUI deathMessage;

    [SerializeField, Tooltip("귀환 버튼")]
    private Button returnButton;

    [Header("설정")]
    [SerializeField, Tooltip("사망 후 UI 표시까지 대기 시간")]
    private float showDelay = 2f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (deathPanel != null)
            deathPanel.SetActive(false);
    }

    private void Start()
    {
        if (returnButton != null)
            returnButton.onClick.AddListener(OnReturnToBase);
    }

    /// <summary>
    /// 사망 UI 표시 — PlayerStats.Die()에서 호출
    /// </summary>
    public void ShowDeathUI()
    {
        StartCoroutine(ShowAfterDelay());
    }

    private System.Collections.IEnumerator ShowAfterDelay()
    {
        // 사망 애니메이션 대기
        yield return new WaitForSeconds(showDelay);

        if (deathPanel != null)
            deathPanel.SetActive(true);

        if (deathMessage != null)
            deathMessage.text = "쓰러졌습니다...";

        // 게임 일시정지
        Time.timeScale = 0f;
    }

    /// <summary>
    /// 서재로 귀환 버튼 클릭 시
    /// </summary>
    private void OnReturnToBase()
    {
        Debug.Log("[DeathUI] 서재로 귀환");

        // 시간 복구
        Time.timeScale = 1f;

        // 런 데이터 리셋
        RunManager.ResetRun();

        // 거점으로 이동
        SceneLoader.LoadScene("MainMenu");
    }
}
