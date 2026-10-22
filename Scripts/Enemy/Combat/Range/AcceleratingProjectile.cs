// AcceleratingProjectile.cs

using UnityEngine;

/// <summary>
/// 가속 투사체 — 느리게 시작해서 점점 빨라짐
/// - BossRadialAttack에서 생성 시 부착
/// - 직선 이동 + 시간에 따라 속도 증가
/// - 플레이어 충돌 시 데미지
/// </summary>
public class AcceleratingProjectile : MonoBehaviour
{
    // 이동 설정
    private Vector3 direction;
    private float startSpeed;
    private float maxSpeed;
    private float accelerationTime;
    private int damage;
    private float lifetime;

    // 상태
    private float elapsed = 0f;
    private bool initialized = false;

    /// <summary>
    /// 투사체 초기화
    /// </summary>
    public void Initialize(Vector3 dir, float startSpd, float maxSpd,
        float accelTime, int dmg, float life)
    {
        direction = dir.normalized;
        startSpeed = startSpd;
        maxSpeed = maxSpd;
        accelerationTime = accelTime;
        damage = dmg;
        lifetime = life;
        elapsed = 0f;
        initialized = true;

        // 콜라이더 설정 (없으면 추가)
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 0.3f;
        }
        else
        {
            col.isTrigger = true;
        }

        // 수명 후 파괴
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (!initialized) return;

        elapsed += Time.deltaTime;

        // 가속 계산: startSpeed → maxSpeed (accelerationTime 동안)
        float t = Mathf.Clamp01(elapsed / accelerationTime);
        float currentSpeed = Mathf.Lerp(startSpeed, maxSpeed, t * t); // 제곱 커브로 자연스러운 가속

        // 이동 (XZ 평면)
        Vector3 movement = direction * currentSpeed * Time.deltaTime;
        movement.y = 0f;
        transform.position += movement;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerHealth ph = other.GetComponent<PlayerHealth>();
            if (ph != null)
            {
                ph.TakeDamage(damage);
                Debug.Log($"[ThornProjectile] 적중! 데미지: {damage}");
            }

            Destroy(gameObject);
        }
    }
}