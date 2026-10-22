using UnityEngine;
using Unity.Cinemachine;
using DG.Tweening;
using System.Collections;

/// <summary>
/// 게임 피드백 매니저 (싱글톤)
/// 히트스탑(Time.timeScale 조작)과 카메라 흔들림(Cinemachine Impulse)을 관리한다.
/// 타격감의 핵심 시스템으로, 공격 적중 시 호출하여 임팩트를 강화한다.
///
/// [쿨다운 시스템]
/// 다수 적 동시 타격 시 히트스탑이 중첩/연장되는 문제를 방지.
/// 한번 발동되면 hitStopCooldown 동안 추가 히트스탑 무시 (디바운스).
/// 단, ignoreCooldown = true (보스 사망 등)는 쿨다운 무시하고 강제 발동.
/// </summary>
public class HitStopManager : MonoBehaviour
{
    public static HitStopManager Instance { get; private set; }

    // ============================
    //  히트스탑 설정
    // ============================

    [Header("히트스탑 설정")]
    [SerializeField, Tooltip("일반 공격 히트스탑 지속 시간 (초)")]
    private float lightHitStopDuration = 0.04f;

    [SerializeField, Tooltip("강공격/크리티컬 히트스탑 지속 시간 (초)")]
    private float heavyHitStopDuration = 0.08f;

    [SerializeField, Tooltip("보스/처치 히트스탑 지속 시간 (초)")]
    private float impactHitStopDuration = 0.12f;

    [SerializeField, Tooltip("히트스탑 중 타임스케일 (0에 가까울수록 강하게 멈춤)")]
    private float hitStopTimeScale = 0.05f;

    [SerializeField, Tooltip("히트스탑 재발동 쿨다운 — 이 시간 안에 들어온 추가 요청 무시 (다수 적 동시 타격 시 중첩 방지)")]
    private float hitStopCooldown = 0.1f;

    // ============================
    //  카메라 흔들림 설정
    // ============================

    [Header("카메라 흔들림 설정")]
    [SerializeField, Tooltip("Cinemachine Impulse Source (카메라에 붙여야 함)")]
    private CinemachineImpulseSource impulseSource;

    [SerializeField, Tooltip("일반 공격 흔들림 강도")]
    private float lightShakeForce = 0.15f;

    [SerializeField, Tooltip("강공격/크리티컬 흔들림 강도")]
    private float heavyShakeForce = 0.4f;

    [SerializeField, Tooltip("보스/처치 흔들림 강도")]
    private float impactShakeForce = 0.8f;

    // ============================
    //  런타임 변수
    // ============================

    private bool isHitStopping = false;            // 히트스탑 진행 중 여부
    private Coroutine hitStopCoroutine;
    private float lastHitStopTime = -999f;         // 마지막 히트스탑 발동 시각 (쿨다운 계산용, unscaled 기준)

    // ============================
    //  피드백 강도 열거형
    // ============================

    /// <summary>
    /// 피드백 강도 단계. 공격 종류에 따라 선택한다.
    /// </summary>
    public enum FeedbackIntensity
    {
        Light,   // 일반 공격
        Heavy,   // 크리티컬, 강공격
        Impact   // 보스 처치, 특수 스킬
    }

    // ============================
    //  초기화
    // ============================

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Impulse Source 자동 탐색
        if (impulseSource == null)
            impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    // ============================
    //  공개 API — 외부에서 호출하는 메서드들
    // ============================

    /// <summary>
    /// 히트스탑 + 카메라 흔들림을 동시에 실행한다.
    /// 적 피격 시 이 메서드 하나만 호출하면 된다.
    /// </summary>
    /// <param name="intensity">피드백 강도 (Light/Heavy/Impact)</param>
    /// <param name="ignoreCooldown">쿨다운 무시하고 강제 발동 (보스 사망 등 특수 연출용)</param>
    public void PlayHitFeedback(FeedbackIntensity intensity = FeedbackIntensity.Light, bool ignoreCooldown = false)
    {
        PlayHitStop(intensity, ignoreCooldown);
        PlayCameraShake(intensity);
    }

    /// <summary>
    /// 히트스탑만 실행한다.
    /// 쿨다운 중이면 무시 (ignoreCooldown = true 제외).
    /// </summary>
    /// <param name="intensity">피드백 강도</param>
    /// <param name="ignoreCooldown">쿨다운 무시 여부</param>
    public void PlayHitStop(FeedbackIntensity intensity = FeedbackIntensity.Light, bool ignoreCooldown = false)
    {
        // 쿨다운 체크 — 최근 발동 후 hitStopCooldown 안 지났으면 무시
        // unscaledTime 사용: 히트스탑 중 timeScale이 낮아도 정확히 측정
        if (!ignoreCooldown && Time.unscaledTime - lastHitStopTime < hitStopCooldown)
            return;

        // 발동 시각 기록
        lastHitStopTime = Time.unscaledTime;

        float duration = GetHitStopDuration(intensity);

        // 이미 히트스탑 중이면 기존 것을 취소하고 새로 시작
        if (hitStopCoroutine != null)
            StopCoroutine(hitStopCoroutine);

        hitStopCoroutine = StartCoroutine(HitStopRoutine(duration));
    }

    /// <summary>
    /// 카메라 흔들림만 실행한다.
    /// (흔들림은 Cinemachine이 자연스럽게 블렌드하므로 쿨다운 없음)
    /// </summary>
    public void PlayCameraShake(FeedbackIntensity intensity = FeedbackIntensity.Light)
    {
        if (impulseSource == null) return;

        float force = GetShakeForce(intensity);
        impulseSource.GenerateImpulse(force);
    }

    // ============================
    //  히트스탑 코루틴
    // ============================

    /// <summary>
    /// Time.timeScale을 순간적으로 낮췄다가 복원하는 코루틴.
    /// realtime 기반으로 동작하여 timeScale 변경에 영향받지 않는다.
    /// </summary>
    private IEnumerator HitStopRoutine(float duration)
    {
        isHitStopping = true;

        // 타임스케일 낮추기
        Time.timeScale = hitStopTimeScale;

        // realtime 기반 대기 (timeScale 영향 안 받음)
        yield return new WaitForSecondsRealtime(duration);

        // 타임스케일 복원
        Time.timeScale = 1f;
        isHitStopping = false;
        hitStopCoroutine = null;
    }

    // ============================
    //  강도별 값 반환
    // ============================

    /// <summary>
    /// 강도에 따른 히트스탑 지속 시간 반환
    /// </summary>
    private float GetHitStopDuration(FeedbackIntensity intensity)
    {
        switch (intensity)
        {
            case FeedbackIntensity.Light: return lightHitStopDuration;
            case FeedbackIntensity.Heavy: return heavyHitStopDuration;
            case FeedbackIntensity.Impact: return impactHitStopDuration;
            default: return lightHitStopDuration;
        }
    }

    /// <summary>
    /// 강도에 따른 카메라 흔들림 세기 반환
    /// </summary>
    private float GetShakeForce(FeedbackIntensity intensity)
    {
        switch (intensity)
        {
            case FeedbackIntensity.Light: return lightShakeForce;
            case FeedbackIntensity.Heavy: return heavyShakeForce;
            case FeedbackIntensity.Impact: return impactShakeForce;
            default: return lightShakeForce;
        }
    }
}
