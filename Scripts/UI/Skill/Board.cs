using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Transform mainCameraTransform;

    void Start()
    {
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
    }

    void LateUpdate()
    {
        if (mainCameraTransform != null)
        {
            // 나(아이콘)의 앞방향을 카메라의 앞방향과 일치시킵니다.
            transform.forward = mainCameraTransform.forward;
        }
    }
}
