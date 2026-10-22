using UnityEngine;

public class QuarterViewCameraFollow : MonoBehaviour
{
    [Header("Follow Settings")]
    public Transform target;
    public float followSpeed = 5f;
    public bool smoothFollow = true;

    [Header("Camera Position")]
    public Vector3 offset = new Vector3(0, 8, -6); // 쿼터뷰에 적합한 기본 오프셋
    public float height = 8f;
    public float distance = 6f;

    [Header("Camera Rotation")]
    public float rotationX = 35f; // 위에서 내려다보는 각도
    public float rotationY = 0f;  // 좌우 회전

    [Header("Boundaries (Optional)")]
    public bool useBoundaries = false;
    public float minX = -10f;
    public float maxX = 10f;
    public float minZ = -10f;
    public float maxZ = 10f;

    [Header("Camera Smoothing")]
    public float positionLerpSpeed = 5f;
    public float rotationLerpSpeed = 3f;

    private Vector3 velocity = Vector3.zero;

    private void Start()
    {
        // 초기 카메라 회전 설정
        transform.rotation = Quaternion.Euler(rotationX, rotationY, 0);
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // 타겟 뒤쪽에서 높은 위치로 카메라 배치
        Vector3 desiredPosition = CalculateDesiredPosition();

        // 경계 제한 적용
        if (useBoundaries)
        {
            desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
            desiredPosition.z = Mathf.Clamp(desiredPosition.z, minZ, maxZ);
        }

        // 카메라 위치 업데이트
        UpdateCameraPosition(desiredPosition);

        // 카메라 회전 업데이트
        UpdateCameraRotation();
    }

    private Vector3 CalculateDesiredPosition()
    {
        // 타겟 위치
        Vector3 targetPosition = target.position + offset;

        // 카메라 각도를 라디안으로 변환
        float radianX = rotationX * Mathf.Deg2Rad;

        // 쿼터뷰를 위한 위치 계산
        // Z축으로 distance만큼 뒤로, Y축으로 height + 각도에 따른 추가 높이
        Vector3 desiredPosition = targetPosition;
        desiredPosition.z -= distance * Mathf.Cos(radianX);  // 뒤쪽으로
        desiredPosition.y += height + (distance * Mathf.Sin(radianX));  // 위쪽으로

        return desiredPosition;
    }

    private void UpdateCameraPosition(Vector3 desiredPosition)
    {
        if (smoothFollow && followSpeed > 0)
        {
            // 부드러운 따라가기
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, 1f / positionLerpSpeed);
        }
        else
        {
            // 즉시 따라가기
            transform.position = desiredPosition;
        }
    }

    private void UpdateCameraRotation()
    {
        Quaternion desiredRotation = Quaternion.Euler(rotationX, rotationY, 0);

        if (smoothFollow && rotationLerpSpeed > 0)
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, desiredRotation, rotationLerpSpeed * Time.deltaTime);
        }
        else
        {
            transform.rotation = desiredRotation;
        }
    }

    // 런타임에서 카메라 각도 조정을 위한 메서드들
    public void SetCameraAngle(float xAngle, float yAngle)
    {
        rotationX = xAngle;
        rotationY = yAngle;
    }

    public void SetCameraDistance(float newDistance)
    {
        distance = newDistance;
    }

    public void SetCameraHeight(float newHeight)
    {
        height = newHeight;
    }

    // 경계 표시를 위한 기즈모
    private void OnDrawGizmosSelected()
    {
        if (useBoundaries)
        {
            Gizmos.color = Color.yellow;
            Vector3 center = new Vector3((minX + maxX) / 2, transform.position.y, (minZ + maxZ) / 2);
            Vector3 size = new Vector3(maxX - minX, 0.1f, maxZ - minZ);
            Gizmos.DrawWireCube(center, size);
        }

        // 카메라가 바라보는 방향 표시
        if (target != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, target.position);

            // 카메라 시야각 표시 (대략적으로)
            Gizmos.color = Color.cyan;
            Vector3 forward = transform.forward * 10f;
            Gizmos.DrawRay(transform.position, forward);
        }
    }
}