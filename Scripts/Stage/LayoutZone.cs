using UnityEngine;

/// ============================================================
/// LayoutZone — 각 Layout 루트에 부착하는 트리거 존
/// ============================================================
/// 플레이어가 이 Layout에 들어오거나 나가는 것을 감지해서
/// PlayerLayoutTracker에 통보하는 컴포넌트.
///
/// 필수 세팅:
///   - BoxCollider (isTrigger = true) 필요
///   - layoutIndex는 ArenaMapManager.layoutGroups의 인덱스와 일치해야 함
///   - 플레이어 오브젝트에는 "Player" 태그 필수
/// ============================================================
[RequireComponent(typeof(Collider))]
public class LayoutZone : MonoBehaviour
{
    [Header("Layout 식별")]
    [Tooltip("이 존이 나타내는 Layout 인덱스 (ArenaMapManager.layoutGroups와 일치)")]
    [SerializeField] private int layoutIndex = 0;

    [Header("감지 설정")]
    [Tooltip("감지할 대상의 태그 (기본: Player)")]
    [SerializeField] private string playerTag = "Player";

    /// <summary>이 존이 담당하는 Layout 인덱스</summary>
    public int LayoutIndex => layoutIndex;

    // ============================================================
    // Unity 콜라이더 이벤트
    // ============================================================

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        PlayerLayoutTracker.Instance?.NotifyEnter(layoutIndex);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        PlayerLayoutTracker.Instance?.NotifyExit(layoutIndex);
    }

    // ============================================================
    // 에디터 검증
    // ============================================================

    private void OnValidate()
    {
        // 콜라이더가 트리거인지 자동 검사
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"[LayoutZone] {gameObject.name}의 Collider가 isTrigger가 아닙니다. " +
                             "플레이어 감지를 위해 isTrigger를 체크해주세요.", this);
        }
    }
}

