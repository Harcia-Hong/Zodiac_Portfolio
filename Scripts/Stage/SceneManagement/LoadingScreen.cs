using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 로딩 화면 컨트롤러
/// - Loading 씬에 배치
/// - SceneLoader.NextSceneName을 읽어서 목표 씬을 비동기 로드
/// - 진행 바 + 랜덤 팁 표시
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    [Header("UI")]
    [SerializeField, Tooltip("로딩 진행 바")]
    private Slider progressBar;

    [SerializeField, Tooltip("진행률 텍스트 (예: 75%)")]
    private TextMeshProUGUI progressText;

    [SerializeField, Tooltip("팁 텍스트")]
    private TextMeshProUGUI tipText;

    [Header("설정")]
    [SerializeField, Tooltip("최소 로딩 시간 (너무 빨리 넘어가면 어색하니까)")]
    private float minimumLoadTime = 1.5f;

    // 팁 목록
    private readonly string[] tips = new string[]
    {
        "회피 중에는 무적 판정이 있습니다.",
        "탑의 10층마다 별자리 수호자가 기다리고 있습니다.",
        "별가루는 쓰러져도 사라지지 않습니다.",
        "스킬은 최대 5개까지 장착할 수 있습니다.",
        "멀린에게 말을 걸면 유용한 정보를 얻을 수 있습니다.",
        "상점에서 스킬을 구매할 수 있습니다.",
        "재앙이 붙은 층은 더 어렵지만 보상도 큽니다.",
        "별가루 제단에서 영구적으로 강해질 수 있습니다."
    };

    private void Start()
    {
        // 팁 랜덤 표시
        if (tipText != null)
        {
            tipText.text = tips[Random.Range(0, tips.Length)];
        }

        // 비동기 로딩 시작
        StartCoroutine(LoadSceneAsync());
    }

    /// <summary>
    /// 목표 씬 비동기 로드 + 진행률 업데이트
    /// </summary>
    private IEnumerator LoadSceneAsync()
    {
        string targetScene = SceneLoader.NextSceneName;

        // 목표 씬이 없으면 MainMenu로 복귀
        if (string.IsNullOrEmpty(targetScene))
        {
            Debug.LogWarning("[LoadingScreen] 목표 씬이 없습니다. MainMenu로 이동합니다.");
            targetScene = "MainMenu";
        }

        // 비동기 로드 시작 (자동 전환 비활성화)
        AsyncOperation operation = SceneManager.LoadSceneAsync(targetScene);
        operation.allowSceneActivation = false;

        float elapsed = 0f;

        // 로딩 진행
        while (!operation.isDone)
        {
            elapsed += Time.deltaTime;

            // AsyncOperation.progress는 0~0.9 범위 (0.9 = 로드 완료)
            float progress = Mathf.Clamp01(operation.progress / 0.9f);

            // UI 업데이트
            if (progressBar != null)
                progressBar.value = progress;

            if (progressText != null)
                progressText.text = $"{(int)(progress * 100)}%";

            // 로드 완료 + 최소 시간 경과 시 전환
            if (operation.progress >= 0.9f && elapsed >= minimumLoadTime)
            {
                // 100% 표시
                if (progressBar != null)
                    progressBar.value = 1f;

                if (progressText != null)
                    progressText.text = "100%";

                // 잠깐 대기 후 전환
                yield return new WaitForSeconds(0.3f);

                operation.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}
