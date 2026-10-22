using UnityEngine;
using TMPro;

/// ============================================================
/// NodeInteract.cs — CastingNode 전용 F 인터랙트 + 안내 UI
/// ============================================================
/// Stage 1-2 NodeStage에서 CastingNode를 작동시키는 입력 컴포넌트.
///
/// [역할 분리 — 단일 책임 원칙]
///   - CastingNode: 충전 진행도/완료 처리 (게임 로직)
///   - NodeInteract: F 입력 감지 + 안내 UI 표시 (입력/UI)
///
/// [부착 위치]
///   같은 GameObject에 CastingNode와 함께 부착.
///   CastingNode의 SphereCollider(Trigger)를 공유 — 별도 콜라이더 불필요.
///
/// [KillNode와의 차이]
///   KillNode는 영역 진입 시 자동 카운트가 시작되므로 F 인터랙트 불필요.
///   따라서 NodeInteract는 KillNode에는 부착하지 않음.
///
/// [의존성 — 단방향]
///   NodeInteract → CastingNode → NodeBase
///   (역방향 참조 없음, 결합도 낮음)
/// ============================================================
public class NodeInteract : MonoBehaviour
{
    // ============================================================
    // 인스펙터 설정
    // ============================================================

    [Header("연결 — 비워두면 같은 GameObject에서 자동 탐색")]
    [SerializeField, Tooltip("이 인터랙트가 작동시킬 CastingNode")]
    private CastingNode targetNode;

    [Header("인터랙트 입력")]
    [SerializeField, Tooltip("인터랙트 키")]
    private KeyCode interactKey = KeyCode.F;

    [Header("UI 참조")]
    [SerializeField, Tooltip("안내 UI 루트 (World Space Canvas)")]
    private GameObject canvasRoot;

    [SerializeField, Tooltip("프롬프트 텍스트 (TMP)")]
    private TextMeshProUGUI promptText;

    [Header("UI 메시지")]
    [SerializeField, Tooltip("프롬프트 안내 메시지")]
    private string promptMessage = "[F] 활성화";

    // ============================================================
    // 런타임 상태
    // ============================================================

    /// <summary>현재 프롬프트 UI가 표시되어 있는지 (SetActive 캐시 비교용)</summary>
    private bool isPromptVisible = false;

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    /// <summary>
    /// 컴포넌트 자동 캐싱 + UI 초기 상태 설정.
    /// targetNode가 없으면 같은 GameObject에서 자동 탐색.
    /// </summary>
    private void Awake()
    {
        // CastingNode 자동 탐색
        if (targetNode == null)
            targetNode = GetComponent<CastingNode>();

        if (targetNode == null)
        {
            Debug.LogError($"[NodeInteract] '{gameObject.name}': CastingNode를 찾을 수 없습니다. " +
                           "같은 GameObject에 CastingNode를 부착하거나 Inspector에서 수동 연결하세요.");
            enabled = false;
            return;
        }

        // UI 초기 상태: 숨김
        if (canvasRoot != null)
            canvasRoot.SetActive(false);

        // 프롬프트 텍스트 초기 설정 (한 번만)
        if (promptText != null)
            promptText.text = promptMessage;
    }

    /// <summary>활성 시 노드 완료 이벤트 구독</summary>
    private void OnEnable()
    {
        if (targetNode != null)
            targetNode.OnNodeCompleted += HandleNodeCompleted;
    }

    /// <summary>비활성 시 이벤트 해제 (메모리 누수 방지)</summary>
    private void OnDisable()
    {
        if (targetNode != null)
            targetNode.OnNodeCompleted -= HandleNodeCompleted;
    }

    // ============================================================
    // 매 프레임 — UI 표시 + F 입력 처리
    // ============================================================

    /// <summary>
    /// UI 표시 조건 체크 + F 입력 감지.
    /// 노드 완료 후에는 즉시 종료 (이벤트로 이미 UI 숨김 처리됨).
    /// </summary>
    private void Update()
    {
        if (targetNode == null) return;
        if (targetNode.IsCompleted) return;

        // UI 표시 조건: 영역 안 + 충전 안 시작 + 미완료
        bool shouldShowPrompt = targetNode.IsPlayerInRange && !targetNode.IsCasting;
        SetPromptVisible(shouldShowPrompt);

        // F 입력 → 충전 시작 + UI 즉시 숨김
        if (shouldShowPrompt && Input.GetKeyDown(interactKey))
        {
            targetNode.StartCasting();
            SetPromptVisible(false);
        }
    }

    // ============================================================
    // 이벤트 콜백
    // ============================================================

    /// <summary>
    /// 노드 활성화 완료 시 콜백 — UI 영구 숨김.
    /// </summary>
    private void HandleNodeCompleted(NodeBase node)
    {
        SetPromptVisible(false);
    }

    // ============================================================
    // UI 표시 토글 (캐싱)
    // ============================================================

    /// <summary>
    /// 프롬프트 UI 표시/숨김.
    /// 같은 상태일 때 SetActive 호출 생략 (효율).
    /// </summary>
    private void SetPromptVisible(bool visible)
    {
        if (isPromptVisible == visible) return;

        isPromptVisible = visible;

        if (canvasRoot != null)
            canvasRoot.SetActive(visible);
    }
}
