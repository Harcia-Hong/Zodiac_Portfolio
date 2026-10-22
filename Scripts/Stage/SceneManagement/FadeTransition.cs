using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;

/// <summary>
/// 페이드 컷씬 매니저 (게이트키퍼식 Stage 간 전환용)
/// - 검정 페이드 + 텍스트 표시 + 비동기 씬 로드
/// - DontDestroyOnLoad로 씬 전환 중에도 유지
/// - 호출: FadeTransition.Instance.PlayTransition("Stage_1-2", "ADVANCING TO SECTOR 1...")
/// </summary>
public class FadeTransition : MonoBehaviour
{
    public static FadeTransition Instance { get; private set; }

    [Header("UI 참조")]
    [SerializeField, Tooltip("검정 페이드 패널 (자식에 텍스트 포함)")]
    private CanvasGroup fadePanel;

    [SerializeField, Tooltip("전환 텍스트 (예: ADVANCING TO SECTOR 1...)")]
    private TextMeshProUGUI transitionText;

    [Header("페이드 시간 설정")]
    [SerializeField, Tooltip("페이드 인 시간 (검정 화면 됨)")]
    private float fadeInDuration = 0.5f;

    [SerializeField, Tooltip("검정 화면 정지 시간 (텍스트 표시)")]
    private float holdDuration = 1f;

    [SerializeField, Tooltip("페이드 아웃 시간 (검정 화면 사라짐)")]
    private float fadeOutDuration = 0.5f;

    // 중복 전환 방지 플래그
    private bool isTransitioning = false;

    /// <summary>
    /// 싱글톤 초기화 + DontDestroyOnLoad 설정
    /// </summary>
    private void Awake()
    {
        // 이미 인스턴스가 있으면 자기 자신 파괴 (씬마다 중복 생성 방지)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 시작 시 페이드 패널 투명 + 클릭 통과
        if (fadePanel != null)
        {
            fadePanel.alpha = 0f;
            fadePanel.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// 페이드 컷씬과 함께 씬 전환 시작
    /// </summary>
    /// <param name="nextSceneName">전환할 씬 이름 (예: "Stage_1-2")</param>
    /// <param name="message">검정 화면에서 표시할 메시지 (예: "ADVANCING TO SECTOR 1...")</param>
    public void PlayTransition(string nextSceneName, string message)
    {
        // 이미 전환 중이면 무시 (중복 호출 방지)
        if (isTransitioning) return;

        StartCoroutine(TransitionCoroutine(nextSceneName, message));
    }

    /// <summary>
    /// 페이드 인 → 정지 → 씬 로드 → 페이드 아웃 흐름
    /// </summary>
    private IEnumerator TransitionCoroutine(string nextSceneName, string message)
    {
        isTransitioning = true;

        // 1. 표시할 텍스트 설정 (아직 화면에 안 보임 - alpha 0)
        if (transitionText != null)
            transitionText.text = message;

        // 2. 페이드 인 (화면이 검정으로 변하면서 텍스트도 같이 보임)
        // SetUpdate(true) = unscaledDeltaTime 사용 (게임 일시정지 중에도 작동)
        fadePanel.blocksRaycasts = true; // 클릭 차단
        yield return fadePanel.DOFade(1f, fadeInDuration).SetUpdate(true).WaitForCompletion();

        // 3. 비동기 씬 로드 시작 (백그라운드에서 다음 씬 준비)
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(nextSceneName);
        loadOp.allowSceneActivation = false; // 자동 활성화 방지 (수동 제어)

        // 4. 정지 시간 + 로드 완료까지 대기
        // - 최소 holdDuration만큼은 검정 화면 유지 (텍스트 읽을 시간 확보)
        // - 로드가 90% 진행되면 활성화 가능 (Unity AsyncOp 특성)
        float elapsed = 0f;
        while (elapsed < holdDuration || loadOp.progress < 0.9f)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // 5. 새 씬 활성화 (실제 씬 전환 발생)
        loadOp.allowSceneActivation = true;

        // 씬 전환 완료까지 대기
        while (!loadOp.isDone)
        {
            yield return null;
        }

        // 6. 페이드 아웃 (검정 화면 사라지면서 새 씬 보임)
        yield return fadePanel.DOFade(0f, fadeOutDuration).SetUpdate(true).WaitForCompletion();
        fadePanel.blocksRaycasts = false; // 클릭 다시 허용

        isTransitioning = false;
    }
}
