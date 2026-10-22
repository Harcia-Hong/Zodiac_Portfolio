using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

/// <summary>
/// 통합 사운드 매니저 (싱글톤 + DontDestroyOnLoad)
///
/// 기능:
///   - BGM 1개 재생 (페이드 인/아웃 지원)
///   - SFX 풀 N개 (동시 재생 OK, 라운드로빈)
///   - 볼륨 조절 (Master / BGM / SFX 분리)
///   - 씬 진입 시 자동 BGM 재생 (Inspector에 씬-BGM 매핑)
///
/// 사용:
///   SoundManager.Instance.PlayBGM(bgmClip);
///   SoundManager.Instance.PlaySFX(sfxClip);
///   SoundManager.Instance.PlaySFX(sfxClip, 0.7f);  // 볼륨 70%
///   SoundManager.Instance.StopBGM();
/// </summary>
public class SoundManager : MonoBehaviour
{
    // ============================================================
    // 싱글톤
    // ============================================================

    public static SoundManager Instance { get; private set; }

    // ============================================================
    // 씬-BGM 매핑 (Inspector 셋업)
    // ============================================================

    /// <summary>씬 이름과 BGM AudioClip의 매핑 (자동 재생용)</summary>
    [System.Serializable]
    public class SceneBGM
    {
        [Tooltip("씬 이름 (정확히 일치해야 함, 예: 'MainMenu')")]
        public string sceneName;

        [Tooltip("해당 씬에서 재생할 BGM")]
        public AudioClip clip;
    }

    [Header("씬 진입 시 자동 재생할 BGM 매핑")]
    [SerializeField, Tooltip("매핑 없는 씬은 BGM 변경 안 함 (이전 BGM 그대로 유지)")]
    private List<SceneBGM> sceneBGMs = new List<SceneBGM>();

    // ============================================================
    // SFX 풀 설정
    // ============================================================

    [Header("SFX 풀 설정")]
    [SerializeField, Tooltip("동시에 재생 가능한 SFX 채널 수 (라운드로빈)")]
    private int sfxPoolSize = 10;

    // ============================================================
    // 볼륨 설정 (0~1)
    // ============================================================

    [Header("초기 볼륨 (0~1)")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1.0f;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1.0f;

    [Header("페이드 기본 시간 (초)")]
    [SerializeField] private float defaultFadeInDuration = 1.0f;
    [SerializeField] private float defaultFadeOutDuration = 1.0f;

    // ============================================================
    // 런타임 상태
    // ============================================================

    private AudioSource bgmSource;
    private AudioSource[] sfxPool;
    private int sfxCursor = 0;
    private AudioClip currentBGM;
    private Tween bgmFadeTween;

    // ============================================================
    // Unity 생명주기
    // ============================================================

    /// <summary>
    /// 싱글톤 초기화 + DDOL + AudioSource 풀 자동 생성
    /// </summary>
    private void Awake()
    {
        // 중복 인스턴스 처리
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeAudioSources();
    }

    /// <summary>씬 로드 이벤트 구독 — 자동 BGM 재생용</summary>
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ============================================================
    // 초기화
    // ============================================================

    /// <summary>BGM 1개 + SFX 풀 N개 AudioSource를 자식으로 생성</summary>
    private void InitializeAudioSources()
    {
        // BGM AudioSource 생성 (loop, 페이드 인용 시작 볼륨 0)
        GameObject bgmObj = new GameObject("BGM_AudioSource");
        bgmObj.transform.SetParent(transform);
        bgmSource = bgmObj.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.volume = 0f;

        // SFX 풀 생성 (loop 없음, 한 번 재생)
        sfxPool = new AudioSource[sfxPoolSize];
        for (int i = 0; i < sfxPoolSize; i++)
        {
            GameObject sfxObj = new GameObject($"SFX_AudioSource_{i:D2}");
            sfxObj.transform.SetParent(transform);
            AudioSource src = sfxObj.AddComponent<AudioSource>();
            src.loop = false;
            src.playOnAwake = false;
            sfxPool[i] = src;
        }
    }

    // ============================================================
    // 씬 로드 콜백 — 자동 BGM 재생
    // ============================================================

    /// <summary>
    /// 씬 진입 시 sceneBGMs 매핑에서 BGM 찾아 자동 재생
    /// 매핑 없으면 BGM 그대로 유지 / 동일 BGM이면 재시작 안 함
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AudioClip targetBGM = FindBGMForScene(scene.name);

        if (targetBGM == null) return;
        if (targetBGM == currentBGM) return;

        PlayBGM(targetBGM);
    }

    /// <summary>씬 이름으로 sceneBGMs 리스트에서 BGM 검색</summary>
    private AudioClip FindBGMForScene(string sceneName)
    {
        foreach (var entry in sceneBGMs)
        {
            if (entry.sceneName == sceneName && entry.clip != null)
                return entry.clip;
        }
        return null;
    }

    // ============================================================
    // Public API — BGM
    // ============================================================

    /// <summary>
    /// BGM 재생 (페이드 인). 기존 BGM이 있으면 자동으로 교체
    /// </summary>
    /// <param name="clip">재생할 BGM</param>
    /// <param name="fadeInDuration">페이드 인 시간 (음수면 기본값 사용)</param>
    public void PlayBGM(AudioClip clip, float fadeInDuration = -1f)
    {
        if (clip == null)
        {
            Debug.LogWarning("[SoundManager] PlayBGM: clip이 null입니다.");
            return;
        }

        if (fadeInDuration < 0f) fadeInDuration = defaultFadeInDuration;

        bgmFadeTween?.Kill();

        currentBGM = clip;
        bgmSource.clip = clip;
        bgmSource.volume = 0f;
        bgmSource.Play();

        // unscaledTime — 게임 일시정지 중에도 페이드 진행
        float targetVolume = bgmVolume * masterVolume;
        bgmFadeTween = bgmSource.DOFade(targetVolume, fadeInDuration).SetUpdate(true);

        Debug.Log($"[SoundManager] BGM 재생: {clip.name}");
    }

    /// <summary>BGM 정지 (페이드 아웃)</summary>
    public void StopBGM(float fadeOutDuration = -1f)
    {
        if (bgmSource == null || !bgmSource.isPlaying) return;

        if (fadeOutDuration < 0f) fadeOutDuration = defaultFadeOutDuration;

        bgmFadeTween?.Kill();

        bgmFadeTween = bgmSource.DOFade(0f, fadeOutDuration).SetUpdate(true)
            .OnComplete(() =>
            {
                bgmSource.Stop();
                currentBGM = null;
            });
    }

    // ============================================================
    // Public API — SFX
    // ============================================================

    /// <summary>
    /// SFX 재생 — 라운드로빈으로 풀에서 다음 AudioSource 선택 (2D)
    /// </summary>
    /// <param name="clip">재생할 SFX</param>
    /// <param name="volumeScale">개별 SFX 볼륨 배율 (기본 1, 0~1+)</param>
    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null) return;
        if (sfxPool == null || sfxPool.Length == 0) return;

        AudioSource src = sfxPool[sfxCursor];
        sfxCursor = (sfxCursor + 1) % sfxPool.Length;

        src.clip = clip;
        src.volume = sfxVolume * masterVolume * volumeScale;
        src.Play();
    }

    /// <summary>
    /// 특정 3D 위치에서 SFX 재생 — 거리감/방향감 필요할 때 사용
    /// (예: 적 사망 위치에서 사운드)
    /// </summary>
    public void PlaySFXAt(AudioClip clip, Vector3 position, float volumeScale = 1f)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, position, sfxVolume * masterVolume * volumeScale);
    }

    // ============================================================
    // Public API — 볼륨 조절
    // ============================================================

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        ApplyVolumeChange();
    }

    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        ApplyVolumeChange();
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        // 재생 중인 SFX는 그대로, 다음 재생부터 적용
    }

    /// <summary>현재 재생 중인 BGM의 볼륨을 즉시 갱신</summary>
    private void ApplyVolumeChange()
    {
        if (bgmSource != null && bgmSource.isPlaying)
        {
            bgmSource.volume = bgmVolume * masterVolume;
        }
    }

    // ============================================================
    // 외부 조회용 프로퍼티
    // ============================================================

    public float MasterVolume => masterVolume;
    public float BGMVolume => bgmVolume;
    public float SFXVolume => sfxVolume;
    public AudioClip CurrentBGM => currentBGM;
    public bool IsBGMPlaying => bgmSource != null && bgmSource.isPlaying;
}
