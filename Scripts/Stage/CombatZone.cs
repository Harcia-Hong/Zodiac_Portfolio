using UnityEngine;

/// ============================================================
/// CombatZone — 각 전투 지역에 부착하는 트리거
/// ============================================================
/// 플레이어가 이 전투 지역에 진입하면 CombatZoneManager에
/// 해당 페이즈 시작 신호를 통보하는 컴포넌트.
///
/// 필수 세팅:
///   - BoxCollider (isTrigger = true)
///   - phaseIndex: PhaseData 배열의 인덱스 (0부터)
///   - 플레이어 오브젝트에 "Player" 태그 필수
///
/// 특징:
///   - 일회성 — 한 번 트리거되면 비활성화
///   - Layout의 자식으로 배치 (Layout과 함께 솟아오름/가라앉음)
/// ============================================================
[RequireComponent(typeof(Collider))]
public class CombatZone : MonoBehaviour
{
    [Header("전투 지역 식별")]
    [Tooltip("이 전투 지역이 트리거할 Phase Index (PhaseData 배열의 인덱스, 0부터)")]
    [SerializeField] private int phaseIndex = 0;

    [Header("감지 설정")]
    [Tooltip("감지할 대상의 태그 (기본: Player)")]
    [SerializeField] private string playerTag = "Player";

    [Header("디버그")]
    [SerializeField, Tooltip("씬 뷰에 전투 지역 범위 표시")]
    private bool showGizmos = true;

    [SerializeField] private Color gizmoColor = new Color(1f, 0.8f, 0.2f, 0.3f);

    // ============================================================
    // 런타임 상태
    // ============================================================

    /// <summary>이미 트리거되었는지 (일회성 발동 보장)</summary>
    private bool hasTriggered = false;

    // ============================================================
    // Unity 콜라이더 이벤트
    // ============================================================

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;
        if (!other.CompareTag(playerTag)) return;

        hasTriggered = true;

        CombatZoneManager.Instance?.NotifyZoneTriggered(phaseIndex);

        Debug.Log($"[CombatZone] 플레이어 진입 → Phase {phaseIndex} 트리거 " +
                  $"(오브젝트: {gameObject.name})");
    }

    // ============================================================
    // 외부 인터페이스
    // ============================================================

    /// <summary>이 CombatZone의 Phase Index 반환</summary>
    public int GetPhaseIndex() => phaseIndex;

    /// <summary>이미 트리거되었는지</summary>
    public bool HasTriggered() => hasTriggered;

    /// <summary>
    /// 트리거 상태 수동 리셋 (스테이지 재시작 등에 사용)
    /// </summary>
    public void ResetTrigger()
    {
        hasTriggered = false;
    }

    // ============================================================
    // 에디터 검증 + 기즈모
    // ============================================================

    private void OnValidate()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"[CombatZone] {gameObject.name}의 Collider가 isTrigger가 아닙니다. " +
                             "플레이어 감지를 위해 isTrigger를 체크해주세요.", this);
        }

        phaseIndex = Mathf.Max(0, phaseIndex);
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;

        // 이미 트리거된 경우 회색으로 표시
        Color color = hasTriggered ? new Color(0.5f, 0.5f, 0.5f, 0.2f) : gizmoColor;
        Gizmos.color = color;

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);

        // 테두리
        Color borderColor = color;
        borderColor.a = 1f;
        Gizmos.color = borderColor;
        Gizmos.DrawWireCube(box.center, box.size);

        Gizmos.matrix = oldMatrix;
    }
}
