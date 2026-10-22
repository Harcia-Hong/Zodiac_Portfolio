using UnityEngine;

/// <summary>
/// 플레이어 락온 시스템 - XZ 평면 거리 계산
/// </summary>
public class PlayerLockOn : MonoBehaviour
{
    [Header("Lock On Settings")]
    public float lockOnRange = 15f;
    public LayerMask enemyLayer;

    [Header("Visual Feedback")]
    public GameObject lockOnUI; // 락온 UI 표시용 (옵션)

    [Header("Current Target")]
    public Transform currentTarget; // public으로 카메라에서 접근 가능

    PlayerController playerController;

    public bool isLockOn = false; // public으로 변경

    private void Start()
    {
        playerController = GetComponent<PlayerController>();
    }

    private void Update()
    {
        // Tab 키로 락온 토글
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (!isLockOn)
                TargetSearch();
            else
                TargetRelease();
        }

        // 락온 중일 때 플레이어 회전 (카메라는 따로 처리)
        if (isLockOn && currentTarget != null)
        {
            // 타겟이 사라졌는지 확인
            if (currentTarget.gameObject == null || !currentTarget.gameObject.activeInHierarchy)
            {
                TargetRelease();
                return;
            }

            // 거리가 너무 멀어지면 락온 해제 - XZ 평면 거리로 계산
            Vector3 myPosXZ = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 targetPosXZ = new Vector3(currentTarget.position.x, 0, currentTarget.position.z);
            float distance = Vector3.Distance(myPosXZ, targetPosXZ);

            if (distance > lockOnRange * 1.5f)
            {
                TargetRelease();
                return;
            }

            // 플레이어만 타겟을 바라보게 (카메라는 CameraFollow에서 처리)
            Vector3 dir = currentTarget.position - transform.position;
            dir.y = 0f; // Y축 회전만
            transform.forward = Vector3.Lerp(transform.forward, dir.normalized, Time.deltaTime * 10f);
        }

        // 락온 UI 업데이트
        if (lockOnUI != null)
        {
            lockOnUI.SetActive(isLockOn && currentTarget != null);
            if (isLockOn && currentTarget != null)
            {
                // UI를 타겟 위에 표시
                Vector3 screenPos = Camera.main.WorldToScreenPoint(currentTarget.position + Vector3.up * 2f);
                lockOnUI.transform.position = screenPos;
            }
        }
    }

    void TargetSearch()
    {
        Collider[] targets = Physics.OverlapSphere(transform.position, lockOnRange, enemyLayer);
        if (targets.Length > 0)
        {
            Transform nearestTarget = targets[0].transform;

            // XZ 평면에서 가장 가까운 적 찾기
            Vector3 myPosXZ = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 nearestPosXZ = new Vector3(nearestTarget.position.x, 0, nearestTarget.position.z);
            float minDistance = Vector3.Distance(myPosXZ, nearestPosXZ);

            foreach (var col in targets)
            {
                Vector3 targetPosXZ = new Vector3(col.transform.position.x, 0, col.transform.position.z);
                float distance = Vector3.Distance(myPosXZ, targetPosXZ);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestTarget = col.transform;
                }
            }

            currentTarget = nearestTarget;
            isLockOn = true;

            Debug.Log($"Lock On: {currentTarget.name}");
        }
        else
        {
            Debug.Log("No targets in range");
        }
    }

    void TargetRelease()
    {
        currentTarget = null;
        isLockOn = false;
        Debug.Log("Lock On Released");
    }

    // 디버그용 기즈모
    private void OnDrawGizmosSelected()
    {
        // 락온 범위 표시
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, lockOnRange);

        // 현재 타겟 표시
        if (isLockOn && currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentTarget.position);
            Gizmos.DrawWireSphere(currentTarget.position, 1f);
        }
    }
}
