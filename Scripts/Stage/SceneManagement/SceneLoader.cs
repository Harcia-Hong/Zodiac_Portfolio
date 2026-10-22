using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 전환 매니저
/// - LoadScene("씬이름"): Loading 씬을 거쳐 목표 씬으로 이동 (오래 걸림 - 메인 메뉴, 거점 복귀용)
/// - LoadStageTransition("씬이름", "메시지"): 페이드 컷씬으로 전환 (짧음 - Stage 간 이동용)
/// - LoadSceneImmediate("씬이름"): 즉시 전환 (가벼운 전환용)
/// </summary>
public static class SceneLoader
{
    // Loading 씬에서 이 값을 읽어서 다음 씬을 로드함
    public static string NextSceneName { get; private set; }

    /// <summary>
    /// 로딩 화면을 거쳐 목표 씬으로 이동 (오래 걸리는 전환용)
    /// </summary>
    /// <param name="sceneName">목표 씬 이름 (예: "Base", "MainMenu")</param>
    public static void LoadScene(string sceneName)
    {
        NextSceneName = sceneName;
        SceneManager.LoadScene("Loading");
    }

    /// <summary>
    /// 페이드 컷씬으로 짧게 씬 전환 (Stage 간 이동용)
    /// - FadeTransition 인스턴스가 있으면 페이드 + 텍스트 표시
    /// - 없으면 일반 로딩 화면으로 fallback
    /// </summary>
    /// <param name="sceneName">목표 씬 이름 (예: "Stage_1-2")</param>
    /// <param name="message">검정 화면에서 표시할 메시지 (예: "ADVANCING TO SECTOR 1...")</param>
    public static void LoadStageTransition(string sceneName, string message = "")
    {
        // FadeTransition 인스턴스가 살아있으면 페이드 컷씬으로 전환
        if (FadeTransition.Instance != null)
        {
            FadeTransition.Instance.PlayTransition(sceneName, message);
        }
        // 인스턴스 없으면 (MainMenu를 거치지 않고 진입한 경우 등) 일반 로딩으로 fallback
        else
        {
            Debug.LogWarning("[SceneLoader] FadeTransition 인스턴스 없음. 일반 로딩으로 fallback합니다.");
            LoadScene(sceneName);
        }
    }

    /// <summary>
    /// 로딩 없이 즉시 씬 전환 (타이틀 등 가벼운 전환용)
    /// </summary>
    public static void LoadSceneImmediate(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}
