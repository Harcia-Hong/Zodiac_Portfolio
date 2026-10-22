// SpikeFloor.cs — 주기 발동 가시 함정

using System.Collections;
using UnityEngine;

/// <summary>
/// 일정 시간마다 솟아오르는 가시 함정 (압력판 X, 주기 발동)
///
/// [동작 흐름]
/// 1. 초기 지연 후 루프 시작
/// 2. 루프:
///    a. (선택) 경고 텔레그래프 표시 (warningDuration 동안)
///    b. 가시 솟음 (애니메이션 트리거)
///    c. 데미지 적용 (모드에 따라 단발 또는 도트)
///    d. 가시 가라앉음 (애니 후반)
///    e. 다음 발동까지 쿨다운
/// 3. 반복
///
/// [데미지 모드]
/// - OnImpact: 가시 솟는 순간 1회 데미지 (단발 강타)
/// - DuringActive: 활성 시간 동안 주기적 데미지 (도트)
///
/// [Animator 요구사항]
/// - "doActivate" 트리거 1개만 있으면 됨
/// - Idle/Active 별도 상태 불필요 (기존 단일 상태 그대로 OK)
/// - 애니 재생 후 자동 종료되면 자연스럽게 다음 사이클로
/// </summary>
public class SpikeFloor : MonoBehaviour
{
    // =========================================================
    // 데미지 모드 enum
    // =========================================================

    /// <summary>
    /// 데미지 적용 방식
    /// </summary>
    public enum DamageMode
    {
        /// <summary>가시 솟는 순간 1회만 데미지 (단발 강타)</summary>
        OnImpact,

        /// <summary>활성 시간 동안 주기적 데미지 (도트, 추천)</summary>
        DuringActive
    }

    // =========================================================
    // 주기 타이밍
    // =========================================================

    [Header("주기 타이밍")]
    [SerializeField, Tooltip("첫 발동 전 지연 — 여러 트랩이 한 번에 발동 안 되게 분산용")]
    private float initialDelay = 0f;

    [SerializeField, Tooltip("경고 텔레그래프 표시 시간 (0 = 경고 없이 즉시 발동)")]
    private float warningDuration = 0.3f;

    [SerializeField, Tooltip("애니 시작 → 가시 다 솟아오를 때까지 (애니 길이의 초반부)")]
    private float impactDelay = 0.15f;

    [SerializeField, Tooltip("가시 올라온 채로 유지 시간 (애니 중간부)")]
    private float activeDuration = 1.0f;

    [SerializeField, Tooltip("다음 발동까지 쿨다운 (사이클 사이 휴식)")]
    private float cooldownDuration = 1.5f;

    // =========================================================
    // 데미지 설정
    // =========================================================

    [Header("데미지 설정")]
    [SerializeField, Tooltip("데미지 적용 방식 (OnImpact = 단발 / DuringActive = 도트)")]
    private DamageMode damageMode = DamageMode.DuringActive;

    [SerializeField, Tooltip("1회 데미지량 (OnImpact는 한 방 / DuringActive는 1틱당)")]
    private int damage = 8;

    [SerializeField, Tooltip("DuringActive 모드 데미지 주기 (초)")]
    private float damageTickInterval = 0.5f;

    [SerializeField, Tooltip("데미지 판정 반경")]
    private float damageRadius = 1.5f;

    [SerializeField, Tooltip("플레이어 레이어")]
    private LayerMask playerLayer;

    // =========================================================
    // 애니메이션
    // =========================================================

    [Header("애니메이션")]
    [SerializeField, Tooltip("가시 모델의 Animator (자동 검색 가능)")]
    private Animator animator;

    [SerializeField, Tooltip("활성 애니 트리거 이름")]
    private string activateTrigger = "doActivate";

    [SerializeField, Tooltip("애니 재생 속도 (1.0 = 원본, 1.5 = 빨리, 가시 빨리 사라지게)")]
    private float animationSpeed = 1.0f;

    // =========================================================
    // 텔레그래프
    // =========================================================

    [Header("텔레그래프")]
    [SerializeField, Tooltip("위험 표시 프리팹 (warningDuration > 0일 때만 사용)")]
    private GameObject telegraphPrefab;

    // =========================================================
    // 동작 옵션
    // =========================================================

    [Header("동작 옵션")]
    [SerializeField, Tooltip("씬 시작 시 자동 발동")]
    private bool autoStart = true;

    // =========================================================
    // 내부 상태
    // =========================================================

    private bool isRunning = false;
    private GameObject activeTelegraph;

    // =========================================================
    // Unity 생명주기
    // =========================================================

    /// <summary>
    /// Animator 자동 검색 + 재생 속도 설정
    /// </summary>
    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // 애니 재생 속도 설정 (가시 사라지는 속도 조절)
        if (animator != null)
            animator.speed = animationSpeed;
    }

    /// <summary>
    /// 씬 시작 시 자동 발동
    /// </summary>
    private void Start()
    {
        if (autoStart)
            StartTrap();
    }

    /// <summary>
    /// 비활성화 시 정리 (텔레그래프 제거)
    /// </summary>
    private void OnDisable()
    {
        StopTrap();
    }

    // =========================================================
    // Public API
    // =========================================================

    /// <summary>
    /// 트랩 가동 시작
    /// </summary>
    public void StartTrap()
    {
        if (isRunning) return;
        isRunning = true;
        StartCoroutine(SpikeLoop());
    }

    /// <summary>
    /// 트랩 강제 정지
    /// </summary>
    public void StopTrap()
    {
        isRunning = false;
        StopAllCoroutines();

        if (activeTelegraph != null)
        {
            Destroy(activeTelegraph);
            activeTelegraph = null;
        }
    }

    // =========================================================
    // 메인 루프
    // =========================================================

    /// <summary>
    /// 가시 트랩 무한 사이클 — 경고 → 활성 → 쿨다운 반복
    /// </summary>
    private IEnumerator SpikeLoop()
    {
        // 초기 지연 (여러 트랩 시간차 분산용)
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        while (isRunning)
        {
            // 1. 경고 단계 (선택적)
            if (warningDuration > 0f)
                yield return WarningPhase();

            // 2. 활성 단계 (애니 + 데미지)
            yield return ActivePhase();

            // 3. 쿨다운
            yield return new WaitForSeconds(cooldownDuration);
        }
    }

    // =========================================================
    // 단계별 처리
    // =========================================================

    /// <summary>
    /// 경고 단계 — 텔레그래프 표시 → 회피 시간 → 제거
    /// </summary>
    private IEnumerator WarningPhase()
    {
        if (telegraphPrefab != null)
        {
            Vector3 spawnPos = transform.position + Vector3.up * 0.05f;

            activeTelegraph = Instantiate(
                telegraphPrefab,
                spawnPos,
                Quaternion.Euler(90f, 0f, 0f)
            );

            activeTelegraph.transform.localScale = Vector3.one * damageRadius * 2f;
        }

        yield return new WaitForSeconds(warningDuration);

        if (activeTelegraph != null)
        {
            Destroy(activeTelegraph);
            activeTelegraph = null;
        }
    }

    /// <summary>
    /// 활성 단계 — 가시 솟아오름 + 데미지 적용
    /// 데미지 모드에 따라 단발 또는 도트
    /// </summary>
    private IEnumerator ActivePhase()
    {
        // 가시 애니메이션 트리거
        if (animator != null)
            animator.SetTrigger(activateTrigger);

        // 가시 솟는 시간 대기 (데미지 판정 직전까지)
        yield return new WaitForSeconds(impactDelay);

        // 데미지 적용
        if (damageMode == DamageMode.OnImpact)
        {
            // 단발 데미지 (가시 솟는 순간)
            DealDamage();

            // 가시 유지 시간 대기 (데미지 없이)
            yield return new WaitForSeconds(activeDuration);
        }
        else // DuringActive
        {
            // 도트 데미지 (활성 시간 동안 주기적)
            yield return DealDamageOverTime(activeDuration);
        }
    }

    /// <summary>
    /// 활성 시간 동안 주기적 데미지 (도트 모드)
    /// damageTickInterval마다 데미지 판정
    /// </summary>
    private IEnumerator DealDamageOverTime(float duration)
    {
        float elapsed = 0f;
        float nextTickTime = 0f;

        while (elapsed < duration)
        {
            // 틱 타이밍에 데미지 판정
            if (elapsed >= nextTickTime)
            {
                DealDamage();
                nextTickTime = elapsed + damageTickInterval;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // =========================================================
    // 데미지 처리
    // =========================================================

    /// <summary>
    /// 트랩 중심 기준 OverlapSphere로 플레이어 감지 → 데미지
    /// </summary>
    private void DealDamage()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, damageRadius, playerLayer);

        foreach (var hit in hits)
        {
            PlayerHealth ph = hit.GetComponent<PlayerHealth>();
            if (ph != null && !ph.isDead)
            {
                ph.TakeDamage(damage);
                return; // 1마리만 (싱글 플레이어)
            }
        }
    }

    // =========================================================
    // 디버그 시각화
    // =========================================================

    /// <summary>
    /// 씬 뷰에서 데미지 판정 범위 표시 (와이어 빨간 원)
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, damageRadius);
    }
}
