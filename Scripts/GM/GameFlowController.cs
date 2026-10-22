using UnityEngine;

/// <summary>
/// 전체 게임의 흐름을 관리하는 컨트롤러
/// 여러 스테이지 간 전환, 게임 상태 관리 등을 담당
/// </summary>
public class GameFlowController : MonoBehaviour
{
    [Header("스테이지 관리")]
    [SerializeField] private StageManager stageManager;
    [SerializeField] private StageData[] allStages;
    [SerializeField] private int currentStageIndex = 0;

    [Header("게임 상태")]
    [SerializeField] private bool isGameActive = true;

    private void Awake()
    {
        // 자동 참조 설정
        if (stageManager == null)
            stageManager = FindFirstObjectByType<StageManager>();
    }

    private void Update()
    {
        HandleDebugInput();
    }

    /// <summary>
    /// 디버그 입력 처리
    /// </summary>
    private void HandleDebugInput()
    {
        // 개발용 단축키들
        if (Input.GetKeyDown(KeyCode.R))
        {
            RestartCurrentStage();
        }

        if (Input.GetKeyDown(KeyCode.N))
        {
            GoToNextStage();
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            TogglePause();
        }
    }

    /// <summary>
    /// 현재 스테이지 재시작
    /// </summary>
    public void RestartCurrentStage()
    {
        if (stageManager != null)
        {
            stageManager.RestartStage();
        }
    }

    /// <summary>
    /// 다음 스테이지로 진행
    /// </summary>
    public void GoToNextStage()
    {
        if (allStages == null || allStages.Length == 0)
        {
            Debug.LogWarning("[GameFlowController] 설정된 스테이지가 없습니다!");
            return;
        }

        currentStageIndex++;

        if (currentStageIndex >= allStages.Length)
        {
            Debug.Log("[GameFlowController] 모든 스테이지 클리어!");
            currentStageIndex = 0; // 처음부터 다시
        }

        if (stageManager != null && allStages[currentStageIndex] != null)
        {
            stageManager.StartNewStage(allStages[currentStageIndex]);
        }
    }

    /// <summary>
    /// 일시정지 토글
    /// </summary>
    public void TogglePause()
    {
        isGameActive = !isGameActive;
        Time.timeScale = isGameActive ? 1f : 0f;

        Debug.Log($"[GameFlowController] 게임 {(isGameActive ? "재개" : "일시정지")}");
    }

    /// <summary>
    /// 현재 게임 상태 반환
    /// </summary>
    public bool IsGameActive => isGameActive;

    /// <summary>
    /// 현재 스테이지 인덱스 반환
    /// </summary>
    public int GetCurrentStageIndex() => currentStageIndex;
}
