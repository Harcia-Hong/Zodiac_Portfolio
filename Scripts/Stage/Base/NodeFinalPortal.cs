using UnityEngine;
using TMPro;

/// ============================================================
/// NodeFinalPortal.cs — Stage 1-2 최종 포탈 진입 감지
/// ============================================================
/// NodeStage의 4개 노드 활성화 후 등장하는 최종 포탈 GameObject에 부착.
/// 플레이어가 포탈에 닿으면 NodeStage.OnPortalReached() 호출 → Stage 1-3 전환.
///
/// [진입 방식 옵션]
///   - requireInteraction = false (디폴트): 트리거 진입 즉시 자동 호출
///   - requireInteraction = true: World Space UI 표시 후 F키 입력 대기
///
/// [중복 호출 방지]
///   hasTriggered 플래그로 1회 한정 — 페이드 진행 중 재진입 방지.
///
/// [부착 위치]
///   포탈 GameObject (Trigger Collider 필요)
/// ============================================================
public class NodeFinalPortal : MonoBehaviour
{
    // ============================================================
    // 인스펙터 설정
    // ============================================================

    [Header("연결 — 비워두면 씬에서 자동 탐색")]
    [SerializeField, Tooltip("이 포탈이 트리거할 NodeStage 참조")]
    private NodeStage nodeStage;

    [Header("진입 방식")]
    [SerializeField, Tooltip("true: F키 인터랙트 필요 / false: 닿으면 즉시 자동 진입(디폴트)")]
    private bool requireInteraction = false;

    [SerializeField, Tooltip("인터랙트 키 (requireInteraction == true일 때만 사용)")]
    private KeyCode interactKey = KeyCode.F;

    [Header("World Space UI (옵션)")]
    [SerializeField, Tooltip("안내 UI 루트 — 비워두면 UI 없이 작동")]
    private GameObject canvasRoot;

    [SerializeField, Tooltip("안내 텍스트 (TMP)")]
    private TextMeshProUGUI interactText;

    [SerializeField, Tooltip("표시할 안내 메시지")]
    private string interactMessage = "[F] 다음 스테이지";

    // ============================================================
    // 런타임 상태
    // ============================================================

    /// <summary>플레이어가 트리거 영역 안에 있는지</summary>
    private bool isPlayerInRange = false;

    /// <summary>이미 포탈을 트리거했는지 (1회 한정)</summary>
    private bool hasTriggered = false;

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    /// <summary>
    /// NodeStage 자동 탐색 + UI 초기 상태 설정.
    /// 인스펙터에서 nodeStage 수동 연결을 권장 (FindObjectOfType은 Fallback).
    /// </summary>
    private void Awake()
    {
        // NodeStage 자동 탐색 (수동 연결이 비어있을 때만)
        if (nodeStage == null)
            nodeStage = FindAnyObjectByType<NodeStage>();

        // UI 초기 숨김
        if (canvasRoot != null)
            canvasRoot.SetActive(false);

        // 안내 텍스트 초기 설정 (한 번만)
        if (interactText != null)
            interactText.text = interactMessage;
    }

    /// <summary>
    /// 매 프레임 F키 입력 체크 (requireInteraction == true일 때만).
    /// 자동 모드면 Update 자체가 의미 없으므로 조기 리턴.
    /// </summary>
    private void Update()
    {
        if (!requireInteraction) return;
        if (!isPlayerInRange) return;
        if (hasTriggered) return;

        if (Input.GetKeyDown(interactKey))
            TriggerPortal();
    }

    // ============================================================
    // 트리거 — 플레이어 진입/이탈
    // ============================================================

    /// <summary>
    /// 플레이어 영역 진입 처리.
    /// - 자동 모드: 즉시 TriggerPortal() 호출
    /// - 인터랙트 모드: 안내 UI 표시 + F키 입력 대기
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (hasTriggered) return;

        isPlayerInRange = true;

        // 자동 모드 → 즉시 트리거
        if (!requireInteraction)
        {
            TriggerPortal();
            return;
        }

        // 인터랙트 모드 → 안내 UI 표시
        if (canvasRoot != null)
            canvasRoot.SetActive(true);
    }

    /// <summary>
    /// 플레이어 영역 이탈 처리 (인터랙트 모드에서 안내 UI 숨김).
    /// 자동 모드는 진입 즉시 트리거되므로 이탈 처리 필요 없음.
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (hasTriggered) return;

        isPlayerInRange = false;

        if (canvasRoot != null)
            canvasRoot.SetActive(false);
    }

    // ============================================================
    // 포탈 트리거 — NodeStage에 알림
    // ============================================================

    /// <summary>
    /// 실제 포탈 진입 처리.
    /// 1) hasTriggered 플래그로 중복 호출 방지
    /// 2) UI 숨김
    /// 3) NodeStage.OnPortalReached() 호출 → 페이드 → Stage 1-3
    /// </summary>
    private void TriggerPortal()
    {
        if (hasTriggered) return;
        hasTriggered = true;

        // NodeStage 참조 검증
        if (nodeStage == null)
        {
            Debug.LogError("[NodeFinalPortal] NodeStage 참조가 없습니다. " +
                           "Inspector에서 수동 연결하거나 씬에 NodeStage가 있는지 확인하세요.");
            return;
        }

        // UI 숨김
        if (canvasRoot != null)
            canvasRoot.SetActive(false);

        Debug.Log("[NodeFinalPortal] 포탈 진입 → NodeStage.OnPortalReached() 호출");

        // NodeStage에 알림 → SetProgress(100f) → StageBase 자동 페이드 흐름
        nodeStage.OnPortalReached();
    }
}
