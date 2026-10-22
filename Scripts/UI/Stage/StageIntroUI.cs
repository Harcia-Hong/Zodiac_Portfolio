using System;
using System.Collections;
using TMPro;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// 스테이지 시작 인트로 UI (세그먼트 트윈즈 스타일)
/// - 씬 진입 후 잠깐 표시되는 "1-1 / 모든 적을 쓰러뜨려라!" 인트로 화면
/// - 검정 레터박스 + 중앙 텍스트 페이드 인 → 정지 → 페이드 아웃 흐름
/// - DontDestroyOnLoad로 씬 전환 중에도 유지 (FadeTransition과 동일 패턴)
///
/// 호출 예:
///   StageIntroUI.Instance.Show("1-1", "모든 적을 쓰러뜨려라!", () => stage.StartMission());
///
/// 흐름:
///   페이드 인 → 정지 → 페이드 아웃 → onComplete 콜백 호출
/// </summary>
public class StageIntroUI : MonoBehaviour
{
    public static StageIntroUI Instance { get; private set; }

    [Header("UI 참조")]
    [SerializeField, Tooltip("페이드 처리용 CanvasGroup (전체 인트로 UI를 감싸야 함)")]
    private CanvasGroup canvasGroup;

    [SerializeField, Tooltip("스테이지 번호 텍스트 (예: '1-1')")]
    private TextMeshProUGUI stageIdText;

    [SerializeField, Tooltip("미션 문구 텍스트 (예: '모든 적을 쓰러뜨려라!')")]
    private TextMeshProUGUI missionText;

    [Header("타이밍 설정")]
    [SerializeField, Tooltip("페이드 인 시간 (검정 레터박스 + 텍스트 등장)")]
    private float fadeInDuration = 0.4f;

    [SerializeField, Tooltip("정지 시간 (텍스트 읽을 시간)")]
    private float holdDuration = 1.5f;

    [SerializeField, Tooltip("페이드 아웃 시간 (사라짐)")]
    private float fadeOutDuration = 0.4f;

    // 중복 호출 방지 플래그
    private bool isShowing = false;

    /// <summary>
    /// 싱글톤 초기화 + DontDestroyOnLoad 설정
    /// 시작 시 UI는 숨김 상태(alpha 0, 클릭 통과)
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

        // 시작 시 인트로 UI 숨김
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// 인트로 UI 표시 — 외부에서 호출하는 메인 API
    /// </summary>
    /// <param name="stageId">스테이지 번호 (예: "1-1")</param>
    /// <param name="mission">미션 문구 (예: "모든 적을 쓰러뜨려라!")</param>
    /// <param name="onComplete">페이드 아웃 완료 후 호출되는 콜백 (예: StartMission)</param>
    public void Show(string stageId, string mission, Action onComplete = null)
    {
        // 이미 표시 중이면 무시 (중복 호출 방지)
        if (isShowing)
        {
            Debug.LogWarning("[StageIntroUI] 이미 인트로가 표시 중입니다. 중복 호출 무시.");
            return;
        }

        // UI 참조 누락 방어 — 폴백으로 콜백만 호출해 흐름이 끊기지 않게
        if (canvasGroup == null)
        {
            Debug.LogError("[StageIntroUI] canvasGroup이 설정되지 않았습니다. 콜백만 호출하고 종료.", this);
            onComplete?.Invoke();
            return;
        }

        StartCoroutine(ShowRoutine(stageId, mission, onComplete));
    }

    /// <summary>
    /// 인트로 흐름 코루틴 — 텍스트 설정 → 페이드 인 → 정지 → 페이드 아웃 → 콜백
    /// </summary>
    private IEnumerator ShowRoutine(string stageId, string mission, Action onComplete)
    {
        isShowing = true;

        // 1. 텍스트 설정 (아직 화면엔 안 보임 - alpha 0)
        if (stageIdText != null) stageIdText.text = stageId;
        if (missionText != null) missionText.text = mission;

        // 2. 페이드 인 (검정 레터박스 + 텍스트 등장)
        // SetUpdate(true) = unscaledDeltaTime 사용 (게임 일시정지 중에도 작동)
        canvasGroup.blocksRaycasts = true;
        yield return canvasGroup.DOFade(1f, fadeInDuration).SetUpdate(true).WaitForCompletion();

        // 3. 정지 (텍스트 읽을 시간 확보, unscaledTime 누적)
        float elapsed = 0f;
        while (elapsed < holdDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // 4. 페이드 아웃 (사라짐)
        yield return canvasGroup.DOFade(0f, fadeOutDuration).SetUpdate(true).WaitForCompletion();
        canvasGroup.blocksRaycasts = false;

        // 5. 완료 콜백 호출
        isShowing = false;
        onComplete?.Invoke();
    }

    /// <summary>
    /// 강제 종료 — 비상 상황(씬 강제 전환, 디버그 등)에서 인트로 즉시 숨김
    /// </summary>
    public void ForceHide()
    {
        StopAllCoroutines();
        isShowing = false;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
