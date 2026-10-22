using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 게임 종료 결과 UI (세그먼트 트윈즈 스타일)
/// - 모드: Clear (게임 클리어) / Death (사망)
/// - 두 모드 디자인 거의 동일, 텍스트/색상만 차이
/// - DontDestroyOnLoad 싱글톤 (FadeTransition, StageIntroUI와 동일 패턴)
///
/// 호출:
///   GameClearUI.Instance.Show(GameClearUI.GameClearMode.Clear);  // 클리어
///   GameClearUI.Instance.Show(GameClearUI.GameClearMode.Death);  // 사망
///
/// 흐름:
///   호출 → 게임 시간 정지 → 페이드 인 → 통계 표시 + 버튼 활성화
///   → "타이틀로 돌아가기" 클릭 → 시간 복구 → MainMenu 씬 로드
/// </summary>
public class GameClearUI : MonoBehaviour
{
    public static GameClearUI Instance { get; private set; }

    /// <summary>표시 모드</summary>
    public enum GameClearMode
    {
        Clear,  // 게임 클리어 (보스 처치 후 포탈 진입)
        Death   // 사망
    }

    // ============================================================
    // Inspector 설정
    // ============================================================

    [Header("UI 참조")]
    [SerializeField, Tooltip("페이드 처리용 CanvasGroup (전체 UI를 감싸야 함)")]
    private CanvasGroup canvasGroup;

    [SerializeField, Tooltip("상단 가로 스크롤 텍스트 (TextMeshProUGUI)")]
    private TextMeshProUGUI topScrollText;

    [SerializeField, Tooltip("하단 가로 스크롤 텍스트 (반대 방향, TextMeshProUGUI)")]
    private TextMeshProUGUI bottomScrollText;

    [SerializeField, Tooltip("플레이 타임 표시용 텍스트 (예: 'PLAY TIME  02:34')")]
    private TextMeshProUGUI playTimeText;

    [SerializeField, Tooltip("'타이틀로 돌아가기' 버튼")]
    private Button backToTitleButton;

    [Header("모드별 스크롤 텍스트")]
    [SerializeField, Tooltip("클리어 시 상/하단 반복 텍스트 (충분히 길게 — 끝 공백 포함)")]
    private string clearScrollText = "GAME CLEAR  GAME CLEAR  GAME CLEAR  GAME CLEAR  GAME CLEAR  GAME CLEAR  ";

    [SerializeField, Tooltip("사망 시 상/하단 반복 텍스트")]
    private string deathScrollText = "GAME OVER  GAME OVER  GAME OVER  GAME OVER  GAME OVER  GAME OVER  GAME OVER  ";

    [Header("Sound")]
    [Tooltip("게임 클리어 시 재생할 효과음 (Clear 모드)")]
    [SerializeField] private AudioClip clearSfx;

    [Tooltip("게임 오버 시 재생할 효과음 (Death 모드)")]
    [SerializeField] private AudioClip gameOverSfx;

    [Header("타이밍 설정")]
    [SerializeField, Tooltip("페이드 인 시간 (초)")]
    private float fadeInDuration = 0.6f;

    [Header("씬 설정")]
    [SerializeField, Tooltip("타이틀로 돌아갈 씬 이름")]
    private string mainMenuSceneName = "MainMenu";

    // ============================================================
    // 런타임 상태
    // ============================================================

    private bool isShowing = false;

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    /// <summary>
    /// 싱글톤 초기화 + DontDestroyOnLoad 설정
    /// 시작 시 UI 숨김 + 버튼 클릭 이벤트 연결
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

        // 시작 시 UI 숨김
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        // 버튼 클릭 이벤트 등록
        if (backToTitleButton != null)
        {
            backToTitleButton.onClick.RemoveAllListeners();
            backToTitleButton.onClick.AddListener(OnBackToTitleClicked);
        }
    }

    // ============================================================
    // Public API
    // ============================================================

    /// <summary>
    /// 결과 UI 표시 — 외부 호출 메인 진입점
    /// </summary>
    /// <param name="mode">Clear (클리어) 또는 Death (사망)</param>
    public void Show(GameClearMode mode)
    {
        // 중복 호출 방지
        if (isShowing)
        {
            Debug.LogWarning("[GameClearUI] 이미 표시 중입니다. 중복 호출 무시.");
            return;
        }

        // UI 참조 누락 방어
        if (canvasGroup == null)
        {
            Debug.LogError("[GameClearUI] canvasGroup이 설정되지 않았습니다.", this);
            return;
        }

        StartCoroutine(ShowRoutine(mode));
    }

    /// <summary>
    /// 강제 종료 — 비상 상황(메인 메뉴 직접 이동 등) 시 UI 즉시 숨김
    /// </summary>
    public void ForceHide()
    {
        StopAllCoroutines();
        Time.timeScale = 1f;
        isShowing = false;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    // ============================================================
    // 흐름 코루틴
    // ============================================================

    /// <summary>
    /// 결과 UI 흐름 — 모드 텍스트 설정 → 모드별 SFX 재생 → 시간 정지 → 페이드 인
    /// </summary>
    private IEnumerator ShowRoutine(GameClearMode mode)
    {
        isShowing = true;

        // 1. 모드별 상/하단 스크롤 텍스트 설정
        ApplyModeTexts(mode);

        // BGM 페이드 아웃 (결과 SFX 더 잘 들리게)
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.StopBGM(0.5f);
        }

        // 2. 모드별 결과 사운드 재생
        PlayResultSfx(mode);

        // 3. 게임 시간 정지
        Time.timeScale = 0f;

        // 4. 페이드 인
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        yield return canvasGroup.DOFade(1f, fadeInDuration).SetUpdate(true).WaitForCompletion();
    }

    /// <summary>
    /// 모드에 맞는 결과 사운드 재생 (Clear → clearSfx, Death → gameOverSfx)
    /// </summary>
    private void PlayResultSfx(GameClearMode mode)
    {
        AudioClip targetClip = (mode == GameClearMode.Clear) ? clearSfx : gameOverSfx;

        if (targetClip != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(targetClip);
        }
    }

    /// <summary>
    /// 모드에 따른 상/하단 스크롤 텍스트 설정
    /// </summary>
    private void ApplyModeTexts(GameClearMode mode)
    {
        string text = (mode == GameClearMode.Clear) ? clearScrollText : deathScrollText;

        if (topScrollText != null) topScrollText.text = text;
        if (bottomScrollText != null) bottomScrollText.text = text;
    }

    /// <summary>
    /// 플레이 타임 텍스트 갱신 (GlobalTimer의 누적 시간 가져옴)
    /// GlobalTimer 인스턴스가 없으면 '--:--' 표시
    /// </summary>
    private void UpdatePlayTimeText()
    {
        if (playTimeText == null) return;

        playTimeText.text = $"PLAY TIME  {GlobalTimer.GetFormattedTime()}";
    }

    // ============================================================
    // 버튼 핸들러
    // ============================================================

    /// <summary>
    /// "타이틀로 돌아가기" 버튼 클릭 — 런 정리 + 메인 메뉴 로드
    /// 1) Time.timeScale 복구
    /// 2) UI 숨김
    /// 3) 런 데이터 리셋 (RunManager.ResetRun)
    /// 4) GlobalTimer 리셋
    /// 5) DDOL된 Player + 매니저 GameObject 정리 (메인 메뉴에 따라가지 않게)
    /// 6) 메인 메뉴 씬 로드
    /// </summary>
    private void OnBackToTitleClicked()
    {
        // 게임 시간 복구 (씬 전환 후 정상 작동을 위해)
        Time.timeScale = 1f;

        // UI 숨김
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
        isShowing = false;

        // 런 정리 (데이터 리셋 + DDOL GameObject 파괴)
        CleanupRun();

        // 메인 메뉴로 페이드 전환
        SceneLoader.LoadStageTransition(mainMenuSceneName, "");
    }

    /// <summary>
    /// 런 종료 시 데이터 리셋 + DDOL된 GameObject들 정리
    /// - Player + 매니저들이 메인 메뉴에 따라가지 않도록 Destroy
    /// - 다음 런 시작 시 Stage 1-1에서 매니저들이 새로 생성됨
    /// </summary>
    private void CleanupRun()
    {
        // 1. 런 데이터 리셋 (스탯, 스킬, 디버프 모두 초기화)
        RunManager.ResetRun();

        // 2. 글로벌 타이머 리셋 (메인 메뉴에서 시간 흐르지 않게)
        if (GlobalTimer.Instance != null)
            GlobalTimer.Instance.ResetTimer();

        // 3. DDOL된 매니저 GameObject 파괴 (메인 메뉴에 따라가지 않도록)
        //    Player가 root고 매니저가 자식이면 한 번에 정리됨, 별개여도 각자 정리
        SafeDestroy(PlayerStats.Instance);
        SafeDestroy(SkillManager.Instance);
        SafeDestroy(RewardManager.Instance);
        SafeDestroy(FloorEffectManager.Instance);
    }

    /// <summary>
    /// 안전 파괴 헬퍼 — GameObject 즉시 비활성화 + Destroy 예약
    /// SetActive(false)는 즉시 효과라 페이드 화면에 잠깐 보이는 문제 방지
    /// </summary>
    private static void SafeDestroy(MonoBehaviour manager)
    {
        if (manager != null && manager.gameObject != null)
        {
            // 1) 즉시 비활성화 (보이지 않게)
            manager.gameObject.SetActive(false);
            // 2) Destroy 예약 (이번 프레임 끝에 파괴)
            Destroy(manager.gameObject);
        }
    }
}
