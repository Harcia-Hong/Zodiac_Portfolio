using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Camera Settings")]
    public Vector3 offset = new Vector3(0, 4, -8);
    public Vector3 rotation = new Vector3(15, 0, 0);

    void Start()
    {
        // 초기 회전 설정
        transform.rotation = Quaternion.Euler(rotation);
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 단순하게 플레이어 뒤에서 따라가기
        transform.position = target.position + offset;
    }
}