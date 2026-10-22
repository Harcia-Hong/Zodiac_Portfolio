using UnityEngine;
using TMPro;

/// <summary>
/// 오벨리스크 인터랙트 — F키로 Stage 1-1 미션 시작
/// - 플레이어 범위 진입 시 [F] 안내 표시
/// - F키 누르면 ObeliskStage.StartMission() 호출 (1회 한정)
/// 
/// 부착 위치: 오벨리스크 GameObject (Trigger Collider 필요)
/// </summary>
public class ObeliskInteract : MonoBehaviour
{
    // ============================================================
    // Inspector 설정
    // ============================================================

    [Header("연결")]
    [SerializeField, Tooltip("미션을 시작할 ObeliskStage 참조 (씬에 있는 StageController)")]
    private ObeliskStage obeliskStage;

    [Header("World Space UI")]
    [SerializeField, Tooltip("Canvas 루트 (오벨리스크 위에 떠있는 UI)")]
    private GameObject canvasRoot;

    [SerializeField, Tooltip("안내 텍스트 (예: [F] : 활성화)")]
    private TextMeshProUGUI interactText;

    [Header("설정")]
    [SerializeField, Tooltip("인터랙트 키")]
    private KeyCode interactKey = KeyCode.F;

    [SerializeField, Tooltip("표시할 안내 메시지")]
    private string interactMessage = "[F] : 오벨리스크 활성화";

    // ============================================================
    // 런타임 상태
    // ============================================================

    // 플레이어가 인터랙트 범위 안에 있는지
    private bool isPlayerInRange = false;

    // 이미 인터랙트했는지 (1회 한정)
    private bool hasInteracted = false;

    // ============================================================
    // 생명주기
    // ============================================================

    /// <summary>
    /// 시작 시 안내 UI 숨김 (플레이어가 들어올 때만 표시)
    /// </summary>
    private void Awake()
    {
        if (canvasRoot != null)
            canvasRoot.SetActive(false);
    }

    /// <summary>
    /// 매 프레임 입력 체크 (범위 안 + 미인터랙트 상태일 때만)
    /// </summary>
    private void Update()
    {
        // 범위 밖이거나 이미 인터랙트했으면 무시
        if (!isPlayerInRange || hasInteracted) return;

        // F키 입력 체크
        if (Input.GetKeyDown(interactKey))
        {
            Interact();
        }
    }

    // ============================================================
    // 범위 감지
    // ============================================================

    /// <summary>
    /// 플레이어가 인터랙트 범위에 진입
    /// 안내 UI 표시
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        isPlayerInRange = true;

        // 이미 인터랙트했으면 UI 표시 안 함
        if (!hasInteracted && canvasRoot != null)
        {
            canvasRoot.SetActive(true);

            // 안내 메시지 설정
            if (interactText != null)
                interactText.text = interactMessage;
        }
    }

    /// <summary>
    /// 플레이어가 인터랙트 범위 밖으로 나감
    /// 안내 UI 숨김
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        isPlayerInRange = false;

        if (canvasRoot != null)
            canvasRoot.SetActive(false);
    }

    // ============================================================
    // 인터랙트 처리
    // ============================================================

    /// <summary>
    /// F키 입력 시 호출 — Stage 1-1 미션 시작
    /// 1) ObeliskStage.StartMission() 호출
    /// 2) hasInteracted = true (재인터랙트 차단)
    /// 3) 안내 UI 숨김
    /// </summary>
    private void Interact()
    {
        // ObeliskStage 참조 검증
        if (obeliskStage == null)
        {
            Debug.LogError("[ObeliskInteract] ObeliskStage가 설정되지 않았습니다. " +
                           "Inspector에서 연결하세요.");
            return;
        }

        // 미션 시작
        obeliskStage.StartMission();

        // 인터랙트 완료 처리
        hasInteracted = true;

        // 안내 UI 숨김
        if (canvasRoot != null)
            canvasRoot.SetActive(false);

        Debug.Log("[ObeliskInteract] 오벨리스크 활성화 - 미션 시작");
    }
}
