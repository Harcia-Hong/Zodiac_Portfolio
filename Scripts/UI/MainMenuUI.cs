using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면 UI 컨트롤러
/// - 새 게임, 이어하기, 설정, 종료 버튼 처리
/// - SceneLoader를 통해 씬 전환
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("버튼")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Header("패널")]
    [SerializeField, Tooltip("설정 패널 (나중에 연결)")]
    private GameObject settingsPanel;

    private void Start()
    {
        // 버튼 이벤트 연결
        if (newGameButton != null)
            newGameButton.onClick.AddListener(OnNewGame);

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinue);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettings);

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuit);

        // 세이브 데이터 없으면 이어하기 비활성화
        // TODO: 세이브 시스템 구현 후 조건 추가
        // if (!SaveSystem.HasSaveData())
        //     continueButton.interactable = false;
    }

    /// <summary>
    /// 새 게임 시작 — Stage 1-1로 페이드 컷씬 전환
    /// 글로벌 타이머 리셋 + 새 런 시작
    /// </summary>
    private void OnNewGame()
    {
        Debug.Log("[MainMenu] 새 게임 시작 - Stage 1-1 진입");

        // 이전 런 잔여 시간 제거 (Chapter 1을 0초부터 새로 시작)
        GlobalTimer.Instance?.ResetTimer();

        RunManager.ResetRun();

        // 이전 런의 스킬·불씨 정적 백업 클리어 — 깨끗한 슬롯으로 시작.
        //   사망 → ResetRun() 흐름에서도 클리어되지만, 메뉴 직진입 케이스(예: 다른 런 종료 후 메뉴로 빠져나옴)
        //   안전망으로 여기서도 한 번 더 호출.
        SkillManager.ClearPersistedSlots();

        // 페이드 컷씬으로 Stage 1-1 진입
        SceneLoader.LoadStageTransition("Stage0", "MISSION START");
    }

    /// <summary>
    /// 이어하기 — 데모 버전에서는 새 게임이랑 동일 처리
    /// (정식판에서는 메타 데이터 로드 후 Stage 1-1 진입으로 확장 예정)
    /// </summary>
    private void OnContinue()
    {
        Debug.Log("[MainMenu] 이어하기 - 데모 버전 (새 게임과 동일)");

        GlobalTimer.Instance?.ResetTimer();
        SceneLoader.LoadStageTransition("Stage0", "MISSION START");
    }

    /// <summary>
    /// 설정 패널 열기
    /// </summary>
    private void OnSettings()
    {
        Debug.Log("[MainMenu] 설정 열기");

        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    /// <summary>
    /// 게임 종료
    /// </summary>
    private void OnQuit()
    {
        Debug.Log("[MainMenu] 게임 종료");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }
}
