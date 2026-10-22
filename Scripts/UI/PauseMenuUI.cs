using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 중 ESC 키로 호출되는 일시정지 메뉴.
/// 
/// 책임:
///   - ESC 키 토글 (일시정지 ↔ 이어서 하기)
///   - 메인 메뉴 패널 + 3개 서브 패널(사용자 설정/조작법/스테이터스) 전환
///   - 게임 종료 → 런 리셋 후 메인 메뉴 씬으로
/// 
/// ESC 동작 우선순위:
///   1) 일시정지 중 + 서브 패널 열림 → 서브 패널만 닫고 메인 메뉴로 복귀
///   2) 일시정지 중 + 메인 메뉴만 열림 → 이어서 하기 (Resume)
///   3) 게임 중 → 일시정지 (Pause)
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    public static PauseMenuUI Instance { get; private set; }

    // ========================================================================
    // 패널 (인스펙터 연결)
    // ========================================================================

    [Header("패널")]
    [SerializeField, Tooltip("ESC 메인 메뉴 패널 (일시정지 + 5개 버튼)")]
    private GameObject pauseMenuPanel;

    [SerializeField, Tooltip("사용자 설정 서브 패널")]
    private GameObject settingsPanel;

    [SerializeField, Tooltip("조작법 서브 패널")]
    private GameObject controlsPanel;

    [SerializeField, Tooltip("스테이터스 서브 패널")]
    private GameObject statusPanel;

    [SerializeField, Tooltip("일시정지 시 같이 표시될 어두운 배경 오버레이 (Overlay_BG)")]
    private GameObject overlayBackground;

    [SerializeField, Tooltip("일시정지 시 같이 표시될 우측 회전 텍스트 장식 (RightVerticalBar)")]
    private GameObject sidebarDecoration;

    // ========================================================================
    // 버튼 (인스펙터 연결)
    // ========================================================================

    [Header("메인 메뉴 버튼")]
    [SerializeField] private Button resumeButton;       // 이어서 하기
    [SerializeField] private Button settingsButton;     // 사용자 설정
    [SerializeField] private Button controlsButton;     // 조작법
    [SerializeField] private Button statusButton;       // 스테이터스
    [SerializeField] private Button quitButton;         // 게임 종료

    [Header("서브 패널 뒤로가기 버튼")]
    [SerializeField] private Button settingsBackButton;
    [SerializeField] private Button controlsBackButton;
    [SerializeField] private Button statusBackButton;

    // ========================================================================
    // 설정
    // ========================================================================

    [Header("씬 전환")]
    [SerializeField, Tooltip("게임 종료 시 이동할 메인 메뉴 씬 이름")]
    private string mainMenuSceneName = "MainMenu";

    // ========================================================================
    // 상태
    // ========================================================================

    private bool isPaused = false;

    // ========================================================================
    // 라이프사이클
    // ========================================================================

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // 시작 시 모든 패널 닫기 — 인스펙터에서 미리 열어둬도 자동으로 정리됨
        CloseAllPanels();
    }

    private void Start()
    {
        // 버튼 이벤트 연결 (null-safe)
        resumeButton?.onClick.AddListener(Resume);
        settingsButton?.onClick.AddListener(OpenSettings);
        controlsButton?.onClick.AddListener(OpenControls);
        statusButton?.onClick.AddListener(OpenStatus);
        quitButton?.onClick.AddListener(QuitToMainMenu);

        // 서브 패널 뒤로가기 → 메인 메뉴 복귀
        settingsBackButton?.onClick.AddListener(BackToMainMenu);
        controlsBackButton?.onClick.AddListener(BackToMainMenu);
        statusBackButton?.onClick.AddListener(BackToMainMenu);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log($"<color=cyan>[PauseMenuUI]</color> ESC 입력! isPaused={isPaused}, Time.timeScale before={Time.timeScale}");
            HandleEscape();
        }
    }

    // ========================================================================
    // ESC 핸들링
    // ========================================================================

    /// <summary>
    /// ESC 키 입력 우선순위 처리.
    /// </summary>
    private void HandleEscape()
    {
        // 게임 중 → 일시정지 시작
        if (!isPaused)
        {
            Pause();
            return;
        }

        // 일시정지 중 + 서브 패널 열린 상태 → 메인 메뉴로 복귀 (이어서 하기 X)
        if ((settingsPanel != null && settingsPanel.activeSelf)
         || (controlsPanel != null && controlsPanel.activeSelf)
         || (statusPanel != null && statusPanel.activeSelf))
        {
            BackToMainMenu();
            return;
        }

        // 일시정지 중 + 메인 메뉴만 열림 → 이어서 하기
        Resume();
    }

    // ========================================================================
    // 일시정지 / 이어서 하기
    // ========================================================================

    /// <summary>일시정지 시작 — 시간 정지 + 메인 메뉴 패널 표시.</summary>
    public void Pause()
    {
        Debug.Log("<color=yellow>[PauseMenuUI]</color> Pause() 호출 — Time.timeScale = 0");
        isPaused = true;
        Time.timeScale = 0f;

        // 메인 패널 + 배경 오버레이 + 우측 장식 모두 활성
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
        if (overlayBackground != null) overlayBackground.SetActive(true);
        if (sidebarDecoration != null) sidebarDecoration.SetActive(true);
    }

    /// <summary>이어서 하기 — 시간 복구 + 모든 패널 닫기.</summary>
    public void Resume()
    {
        isPaused = false;
        Time.timeScale = 1f;
        CloseAllPanels();
    }

    // ========================================================================
    // 서브 패널 전환
    // ========================================================================

    private void OpenSettings() => SwitchToSubPanel(settingsPanel);
    private void OpenControls() => SwitchToSubPanel(controlsPanel);
    private void OpenStatus() => SwitchToSubPanel(statusPanel);

    /// <summary>메인 메뉴 패널 숨김 + 지정 서브 패널 표시.</summary>
    private void SwitchToSubPanel(GameObject subPanel)
    {
        if (subPanel == null) return;
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        subPanel.SetActive(true);
    }

    /// <summary>서브 패널 모두 닫고 메인 메뉴 패널로 복귀.</summary>
    private void BackToMainMenu()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (controlsPanel != null) controlsPanel.SetActive(false);
        if (statusPanel != null) statusPanel.SetActive(false);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
    }

    // ========================================================================
    // 게임 종료
    // ========================================================================

    /// <summary>
    /// 게임 종료 → 시간 복구 + 런 리셋 + 메인 메뉴 씬 전환.
    /// RunManager.ResetRun이 SkillManager.ClearPersistedSlots 포함 모든 정리 수행.
    /// </summary>
    private void QuitToMainMenu()
    {
        Time.timeScale = 1f;
        isPaused = false;

        RunManager.ResetRun();
        SceneLoader.LoadScene(mainMenuSceneName);
    }

    // ========================================================================
    // 헬퍼
    // ========================================================================

    private void CloseAllPanels()
    {
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (controlsPanel != null) controlsPanel.SetActive(false);
        if (statusPanel != null) statusPanel.SetActive(false);

        // 배경 오버레이 + 우측 장식도 같이 닫음
        if (overlayBackground != null) overlayBackground.SetActive(false);
        if (sidebarDecoration != null) sidebarDecoration.SetActive(false);
    }
}
