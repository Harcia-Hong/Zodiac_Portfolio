using UnityEngine;

/// <summary>
/// 회전 칼날 Container — 매 프레임 Y축 회전 + 지정 시간 후 자동 소멸.
/// 
/// 동작:
///   Update에서 rotateSpeed (deg/s)로 Y축 회전.
///   duration 후 GameObject 자체 Destroy.
/// 
/// 책임 밖:
///   - 칼날 배치 (RotatingBladesAction이 처리)
///   - 데미지 판정 (RotatingBladesAction이 처리)
///   - 캐릭터 추적 (Container를 캐릭터 자식으로 부착하면 자동)
/// </summary>
public class RotatingBlades : MonoBehaviour
{
    [Tooltip("회전 속도 (초당 도). 360 = 1초에 1바퀴.")]
    public float rotateSpeed = 360f;

    [Tooltip("자동 소멸까지 시간 (초). 0 이하면 자동 소멸 X.")]
    public float duration = 4f;

    private float timer = 0f;

    private void Update()
    {
        // Y축 회전
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.Self);

        // 지속시간 체크
        if (duration > 0f)
        {
            timer += Time.deltaTime;
            if (timer >= duration) Destroy(gameObject);
        }
    }
}
