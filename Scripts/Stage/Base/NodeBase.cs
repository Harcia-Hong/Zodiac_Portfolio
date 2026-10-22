using System;
using System.Collections;
using UnityEngine;

/// ============================================================
/// NodeBase.cs — Stage 1-2 NodeStage용 노드 추상 베이스
/// ============================================================
/// 모든 노드 타입(KillNode, CastingNode)이 상속받는 공통 베이스.
///
/// [책임 — 단일 책임 원칙]
///   1. 활성화 진행도(0~1) 관리
///   2. 상태 전이 (Idle → Charging → Completed)
///   3. 시각 피드백 (Emission 색상 / 발광 강도)
///   4. 플레이어 영역 감지 (SphereCollider Trigger)
///   5. 완료 시 외부에 이벤트 발행
///
/// [의도적으로 책임지지 않는 것]
///   - 잡몹 스폰 트리거 → NodeStage가 담당
///   - 환경 해저드 발동 → NodeStage가 담당
///   - F 인터랙트 입력 → NodeInteract가 담당
///
/// [상속 가이드]
///   - TickProgress() : 자식이 반드시 구현 (진행도 갱신 방식)
///   - OnPlayerEnter/Exit : 자식이 필요시 override
///   - 진행도는 SetProgress(value) 호출로 갱신 (Clamp/완료처리 자동)
/// ============================================================
public abstract class NodeBase : MonoBehaviour
{
    // ============================================================
    // 노드 상태
    // ============================================================

    /// <summary>노드 진행 상태</summary>
    public enum NodeState
    {
        Idle,       // 시작 전 — 어두운 색
        Charging,   // 활성화 진행 중 — 진행도 따라 발광 ↑
        Completed   // 완료 — 시안색 고정
    }

    // ============================================================
    // 인스펙터 설정
    // ============================================================

    [Header("노드 식별")]
    [SerializeField, Tooltip("노드 이름 (디버깅용)")]
    protected string nodeName = "Node";

    [Header("영역 감지")]
    [SerializeField, Tooltip("플레이어 감지용 트리거 콜라이더 (없으면 자동 탐색)")]
    protected SphereCollider triggerCollider;

    [Header("시각 피드백 — 발광 대상")]
    [SerializeField, Tooltip("Emission이 적용될 Renderer들 (비어있으면 자식 전체 자동 탐색)")]
    protected Renderer[] glowRenderers;

    [Header("시각 피드백 — 색상")]
    [SerializeField, Tooltip("Idle 상태 색상 (어둡게)")]
    protected Color idleColor = new Color(0.2f, 0.2f, 0.2f);

    [SerializeField, Tooltip("Charging 상태 색상 (진행도에 따라 발광)")]
    protected Color chargingColor = new Color(1f, 0.9f, 0.3f);

    [SerializeField, Tooltip("Completed 상태 색상 (밝게 고정)")]
    protected Color completedColor = new Color(0.3f, 0.9f, 1f);

    [Header("시각 피드백 — 강도")]
    [SerializeField, Tooltip("최대 Emission 강도")]
    protected float maxEmissionIntensity = 2f;

    [SerializeField, Tooltip("상태 전환 시 색상 페이드 시간")]
    protected float colorTransitionDuration = 0.4f;

    // ============================================================
    // 런타임 상태
    // ============================================================

    /// <summary>현재 노드 상태</summary>
    protected NodeState currentState = NodeState.Idle;

    /// <summary>현재 진행도 (0.0 ~ 1.0)</summary>
    protected float currentProgress = 0f;

    /// <summary>플레이어가 영역 안에 있는지</summary>
    protected bool isPlayerInRange = false;

    // ============================================================
    // 시각 피드백 내부 상태 (MaterialPropertyBlock 기반)
    // ============================================================

    private MaterialPropertyBlock propertyBlock;
    private Coroutine emissionTweenCoroutine;
    private float currentEmissionIntensity = 0f;
    private Color currentEmissionColor;

    // 셰이더 프로퍼티 ID 캐싱 (All In 1 3D Shader)
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionSelfGlowID = Shader.PropertyToID("_EmissionSelfGlow");

    // ============================================================
    // 외부 이벤트
    // ============================================================

    /// <summary>노드 활성화 완료 시 발생 (NodeStage가 구독)</summary>
    public event Action<NodeBase> OnNodeCompleted;

    // ============================================================
    // 공개 프로퍼티
    // ============================================================

    public string NodeName => nodeName;
    public NodeState CurrentState => currentState;
    public float Progress => currentProgress;
    public bool IsCompleted => currentState == NodeState.Completed;
    public bool IsCharging => currentState == NodeState.Charging;
    public bool IsPlayerInRange => isPlayerInRange;

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    /// <summary>
    /// 컴포넌트 캐싱 + 누락된 참조 자동 탐색.
    /// 자식 클래스에서 override 시 반드시 base.Awake() 호출.
    /// </summary>
    protected virtual void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();

        // 트리거 콜라이더 자동 탐색
        if (triggerCollider == null)
            triggerCollider = GetComponent<SphereCollider>();

        // 트리거 자동 보정 (실수 방지)
        if (triggerCollider != null && !triggerCollider.isTrigger)
        {
            Debug.LogWarning($"[NodeBase] '{nodeName}': SphereCollider가 Trigger가 아닙니다. 자동 보정합니다.");
            triggerCollider.isTrigger = true;
        }

        // Renderer 자동 탐색 (수동 할당이 비어있을 때만)
        if (glowRenderers == null || glowRenderers.Length == 0)
            glowRenderers = GetComponentsInChildren<Renderer>();
    }

    /// <summary>
    /// 시작 시 Idle 상태 + 어두운 발광으로 초기화.
    /// 자식 클래스에서 override 시 반드시 base.Start() 호출.
    /// </summary>
    protected virtual void Start()
    {
        currentState = NodeState.Idle;
        currentProgress = 0f;
        ApplyEmissionInstant(idleColor, 0f);
    }

    // ============================================================
    // 트리거 — 플레이어 영역 진입/이탈
    // ============================================================

    /// <summary>플레이어가 영역에 들어왔을 때 자식 콜백 호출</summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (IsCompleted) return;

        isPlayerInRange = true;
        OnPlayerEnter();
    }

    /// <summary>플레이어가 영역에서 나갔을 때 자식 콜백 호출</summary>
    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (IsCompleted) return;

        isPlayerInRange = false;
        OnPlayerExit();
    }

    /// <summary>
    /// 플레이어가 영역에 들어왔을 때 자식이 처리할 로직.
    /// 기본 구현은 비어있음 (KillNode는 사용 안 함, CastingNode는 override).
    /// </summary>
    protected virtual void OnPlayerEnter() { }

    /// <summary>
    /// 플레이어가 영역에서 나갔을 때 자식이 처리할 로직.
    /// 기본 구현은 비어있음 (CastingNode가 override해서 진행도 리셋).
    /// </summary>
    protected virtual void OnPlayerExit() { }

    // ============================================================
    // 진행도 갱신 (자식이 호출)
    // ============================================================

    /// <summary>
    /// 진행도 갱신 — 자식 클래스가 자기 방식대로 호출.
    /// - KillNode: 적 처치 이벤트 시 호출 (외부에서도 호출 가능)
    /// - CastingNode: Update에서 매 프레임 호출 (시간 누적)
    /// </summary>
    public abstract void TickProgress();

    /// <summary>
    /// 진행도를 직접 설정 (0~1 자동 Clamp).
    /// 자식 클래스에서 진행도 갱신 시 이 메서드를 통해 일관성 있게 처리.
    /// 100% 도달 시 자동으로 CompleteNode() 호출.
    /// </summary>
    protected void SetProgress(float value)
    {
        if (IsCompleted) return;

        // Idle → Charging 전이 (첫 진행 시점)
        if (currentState == NodeState.Idle && value > 0f)
            EnterChargingState();

        currentProgress = Mathf.Clamp01(value);

        // 충전 중이면 시각 갱신 (진행도 비례 발광)
        if (currentState == NodeState.Charging)
            UpdateChargingVisual();

        // 100% 도달 → 완료 처리
        if (currentProgress >= 1f)
            CompleteNode();
    }

    /// <summary>
    /// 진행도 리셋 — Charging 중에만 동작 (Completed는 무시).
    /// CastingNode가 영역 이탈/피격 시 호출.
    /// </summary>
    public virtual void ResetProgress()
    {
        if (IsCompleted) return;
        if (currentState == NodeState.Idle) return;

        currentProgress = 0f;
        currentState = NodeState.Idle;

        // 시각: Idle 색상으로 부드럽게 페이드
        TweenEmission(idleColor, 0f, colorTransitionDuration);

        Debug.Log($"[NodeBase] '{nodeName}' 진행도 리셋");
    }

    // ============================================================
    // 상태 전이 (내부)
    // ============================================================

    /// <summary>Idle → Charging 전이</summary>
    private void EnterChargingState()
    {
        currentState = NodeState.Charging;
        Debug.Log($"[NodeBase] '{nodeName}' 충전 시작");
    }

    /// <summary>
    /// 노드 완료 처리.
    /// - 상태 Completed 고정
    /// - 발광 색상 시안색으로 페이드
    /// - 외부에 OnNodeCompleted 이벤트 발행
    /// </summary>
    protected virtual void CompleteNode()
    {
        if (IsCompleted) return;

        currentState = NodeState.Completed;
        currentProgress = 1f;

        // 완료 색상으로 부드럽게 전환 (밝게)
        TweenEmission(completedColor, maxEmissionIntensity, colorTransitionDuration);

        Debug.Log($"[NodeBase] '{nodeName}' 활성화 완료");

        // 외부 이벤트 발행 — NodeStage가 구독해서 4개 다 채워졌는지 체크
        OnNodeCompleted?.Invoke(this);
    }

    // ============================================================
    // 시각 피드백 — Emission 갱신
    // ============================================================

    /// <summary>
    /// 충전 중일 때 진행도에 비례하는 발광 강도 즉시 적용.
    /// 매 프레임 호출될 수 있으므로 트윈 중단 + 즉시 반영.
    /// </summary>
    private void UpdateChargingVisual()
    {
        // 트윈 중이면 중단 (진행도 갱신이 우선)
        if (emissionTweenCoroutine != null)
        {
            StopCoroutine(emissionTweenCoroutine);
            emissionTweenCoroutine = null;
        }

        // 진행도 0% = 약한 발광, 100% = 최대 발광
        float intensity = Mathf.Lerp(0.2f, maxEmissionIntensity, currentProgress);
        ApplyEmissionInstant(chargingColor, intensity);
    }

    /// <summary>
    /// Emission 즉시 적용 (트윈 없음).
    /// MaterialPropertyBlock 사용 → 머터리얼 인스턴스 생성 X (메모리 효율).
    /// </summary>
    private void ApplyEmissionInstant(Color baseColor, float intensity)
    {
        currentEmissionColor = baseColor;
        currentEmissionIntensity = intensity;

        if (glowRenderers == null) return;

        Color finalEmission = baseColor * intensity;

        foreach (var rend in glowRenderers)
        {
            if (rend == null) continue;
            rend.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColorID, finalEmission);
            propertyBlock.SetFloat(EmissionSelfGlowID, intensity);
            rend.SetPropertyBlock(propertyBlock);
        }
    }

    /// <summary>
    /// Emission 부드러운 페이드 트윈 시작.
    /// 상태 전환(Charging→Completed, Charging→Idle)에서만 사용.
    /// </summary>
    private void TweenEmission(Color targetColor, float targetIntensity, float duration)
    {
        if (emissionTweenCoroutine != null)
            StopCoroutine(emissionTweenCoroutine);

        emissionTweenCoroutine = StartCoroutine(
            TweenEmissionRoutine(targetColor, targetIntensity, duration)
        );
    }

    /// <summary>
    /// 현재 Emission 상태 → 목표 상태로 부드럽게 보간.
    /// 색상과 강도를 동시에 트윈.
    /// </summary>
    private IEnumerator TweenEmissionRoutine(Color targetColor, float targetIntensity, float duration)
    {
        Color startColor = currentEmissionColor;
        float startIntensity = currentEmissionIntensity;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            Color c = Color.Lerp(startColor, targetColor, t);
            float i = Mathf.Lerp(startIntensity, targetIntensity, t);

            ApplyEmissionInstant(c, i);
            yield return null;
        }

        // 정확한 최종값 보정
        ApplyEmissionInstant(targetColor, targetIntensity);
        emissionTweenCoroutine = null;
    }

    // ============================================================
    // 디버그 — Scene 뷰 영역 표시
    // ============================================================

    /// <summary>Scene 뷰에서 트리거 영역을 노란 와이어로 표시</summary>
    private void OnDrawGizmosSelected()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<SphereCollider>();
        if (triggerCollider == null) return;

        Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.4f);
        Gizmos.DrawWireSphere(
            transform.position + triggerCollider.center,
            triggerCollider.radius
        );
    }
}
