using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 사용자 설정 패널 — 해상도/창모드/배경음/효과음/음소거.
/// 모든 항목 좌우 화살표 버튼으로 변경.
/// 사운드는 20% 단위 6단계 (0, 20, 40, 60, 80, 100).
/// 
/// PlayerPrefs로 자동 저장·복원. Awake에서 즉시 적용.
/// 인스펙터에서 이 GameObject를 '활성' 상태로 둬야 Awake 호출됨.
/// </summary>
public class SettingsPanelUI : MonoBehaviour
{
    // ========================================================================
    // 해상도
    // ========================================================================

    [Header("해상도")]
    [SerializeField] private TextMeshProUGUI resolutionText;
    [SerializeField] private Button resolutionLeftButton;
    [SerializeField] private Button resolutionRightButton;

    private readonly Vector2Int[] resolutionOptions =
    {
        new Vector2Int(1280, 720),
        new Vector2Int(1600, 900),
        new Vector2Int(1920, 1080),
        new Vector2Int(2560, 1440),
    };

    private int currentResolutionIndex = 1;

    // ========================================================================
    // 창모드
    // ========================================================================

    [Header("창모드")]
    [SerializeField] private TextMeshProUGUI windowModeText;
    [SerializeField] private Button windowModeLeftButton;
    [SerializeField] private Button windowModeRightButton;

    private bool isFullscreen = true;

    // ========================================================================
    // 배경음
    // ========================================================================

    [Header("배경음")]
    [SerializeField] private TextMeshProUGUI bgmValueText;
    [SerializeField] private Button bgmLeftButton;
    [SerializeField] private Button bgmRightButton;

    private int bgmVolumePercent = 60;

    // ========================================================================
    // 효과음
    // ========================================================================

    [Header("효과음")]
    [SerializeField] private TextMeshProUGUI sfxValueText;
    [SerializeField] private Button sfxLeftButton;
    [SerializeField] private Button sfxRightButton;

    private int sfxVolumePercent = 100;

    // ========================================================================
    // 음소거
    // ========================================================================

    [Header("음소거")]
    [SerializeField] private TextMeshProUGUI muteText;
    [SerializeField] private Button muteLeftButton;
    [SerializeField] private Button muteRightButton;

    private bool isMuted = false;

    // ========================================================================
    // 상수 / PlayerPrefs 키
    // ========================================================================

    /// <summary>사운드 변경 단위 — 20% 단위로 0~100 사이 6단계.</summary>
    private const int VOLUME_STEP = 20;

    private const string PREF_RESOLUTION_INDEX = "Settings_ResolutionIndex";
    private const string PREF_FULLSCREEN = "Settings_Fullscreen";
    private const string PREF_BGM_VOLUME = "Settings_BGMVolume";
    private const string PREF_SFX_VOLUME = "Settings_SFXVolume";
    private const string PREF_MUTED = "Settings_Muted";

    // ========================================================================
    // 라이프사이클
    // ========================================================================

    private void Awake()
    {
        LoadSettings();
        SetupListeners();
        UpdateAllUI();
    }

    /// <summary>PlayerPrefs에서 값 읽어 Screen / SoundManager에 즉시 적용.</summary>
    private void LoadSettings()
    {
        currentResolutionIndex = PlayerPrefs.GetInt(PREF_RESOLUTION_INDEX, 1);
        isFullscreen = PlayerPrefs.GetInt(PREF_FULLSCREEN, 1) == 1;
        isMuted = PlayerPrefs.GetInt(PREF_MUTED, 0) == 1;

        // 사운드는 20% 단위로 클램프 (이전 버전 슬라이더 값이 남아 있어도 안전 처리)
        bgmVolumePercent = ClampToStep(PlayerPrefs.GetInt(PREF_BGM_VOLUME, 60));
        sfxVolumePercent = ClampToStep(PlayerPrefs.GetInt(PREF_SFX_VOLUME, 100));

        ApplyResolution();
        ApplyFullscreen();
        ApplyBGMVolume();
        ApplySFXVolume();
        ApplyMute();
    }

    /// <summary>전달 값을 0~100 사이 20단위로 정규화.</summary>
    private int ClampToStep(int value)
    {
        value = Mathf.Clamp(value, 0, 100);
        return (value / VOLUME_STEP) * VOLUME_STEP;
    }

    private void SetupListeners()
    {
        resolutionLeftButton?.onClick.AddListener(() => ChangeResolution(-1));
        resolutionRightButton?.onClick.AddListener(() => ChangeResolution(+1));

        windowModeLeftButton?.onClick.AddListener(ToggleWindowMode);
        windowModeRightButton?.onClick.AddListener(ToggleWindowMode);

        bgmLeftButton?.onClick.AddListener(() => ChangeBGM(-1));
        bgmRightButton?.onClick.AddListener(() => ChangeBGM(+1));

        sfxLeftButton?.onClick.AddListener(() => ChangeSFX(-1));
        sfxRightButton?.onClick.AddListener(() => ChangeSFX(+1));

        muteLeftButton?.onClick.AddListener(ToggleMute);
        muteRightButton?.onClick.AddListener(ToggleMute);
    }

    // ========================================================================
    // 해상도
    // ========================================================================

    private void ChangeResolution(int delta)
    {
        int n = resolutionOptions.Length;
        currentResolutionIndex = (currentResolutionIndex + delta + n) % n;
        ApplyResolution();
        PlayerPrefs.SetInt(PREF_RESOLUTION_INDEX, currentResolutionIndex);
        UpdateResolutionUI();
    }

    private void ApplyResolution()
    {
        var res = resolutionOptions[currentResolutionIndex];
        var mode = isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        Screen.SetResolution(res.x, res.y, mode);
    }

    // ========================================================================
    // 창모드
    // ========================================================================

    private void ToggleWindowMode()
    {
        isFullscreen = !isFullscreen;
        ApplyFullscreen();
        PlayerPrefs.SetInt(PREF_FULLSCREEN, isFullscreen ? 1 : 0);
        UpdateWindowModeUI();
    }

    private void ApplyFullscreen()
    {
        Screen.fullScreenMode = isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }

    // ========================================================================
    // 배경음 / 효과음 — 20% 단위 6단계 (0, 20, 40, 60, 80, 100)
    // ========================================================================

    private void ChangeBGM(int delta)
    {
        bgmVolumePercent = Mathf.Clamp(bgmVolumePercent + delta * VOLUME_STEP, 0, 100);
        ApplyBGMVolume();
        PlayerPrefs.SetInt(PREF_BGM_VOLUME, bgmVolumePercent);
        UpdateBGMUI();
    }

    private void ApplyBGMVolume()
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.SetBGMVolume(bgmVolumePercent / 100f);
    }

    private void ChangeSFX(int delta)
    {
        sfxVolumePercent = Mathf.Clamp(sfxVolumePercent + delta * VOLUME_STEP, 0, 100);
        ApplySFXVolume();
        PlayerPrefs.SetInt(PREF_SFX_VOLUME, sfxVolumePercent);
        UpdateSFXUI();
    }

    private void ApplySFXVolume()
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.SetSFXVolume(sfxVolumePercent / 100f);
    }

    // ========================================================================
    // 음소거
    // ========================================================================

    private void ToggleMute()
    {
        isMuted = !isMuted;
        ApplyMute();
        PlayerPrefs.SetInt(PREF_MUTED, isMuted ? 1 : 0);
        UpdateMuteUI();
    }

    private void ApplyMute()
    {
        AudioListener.volume = isMuted ? 0f : 1f;
    }

    // ========================================================================
    // UI 갱신
    // ========================================================================

    private void UpdateAllUI()
    {
        UpdateResolutionUI();
        UpdateWindowModeUI();
        UpdateBGMUI();
        UpdateSFXUI();
        UpdateMuteUI();
    }

    private void UpdateResolutionUI()
    {
        if (resolutionText == null) return;
        var res = resolutionOptions[currentResolutionIndex];
        resolutionText.text = $"{res.x} x {res.y}";
    }

    private void UpdateWindowModeUI()
    {
        if (windowModeText != null) windowModeText.text = isFullscreen ? "ON" : "OFF";
    }

    private void UpdateBGMUI()
    {
        if (bgmValueText != null) bgmValueText.text = $"{bgmVolumePercent}%";
    }

    private void UpdateSFXUI()
    {
        if (sfxValueText != null) sfxValueText.text = $"{sfxVolumePercent}%";
    }

    private void UpdateMuteUI()
    {
        if (muteText != null) muteText.text = isMuted ? "ON" : "OFF";
    }
}
