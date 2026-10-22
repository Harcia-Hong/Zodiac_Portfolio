using UnityEngine;

/// <summary>
/// 투사체 스크립트
/// </summary>
public class Projectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    public float speed = 10f;
    public int damage = 15;
    public float lifetime = 5f;

    private Vector3 direction;
    private bool initialized = false;

    /// <summary>투사체 초기화</summary>
    public void Initialize(Vector3 dir, float spd, int dmg, float life)
    {
        direction = dir.normalized;
        speed = spd;
        damage = dmg;
        lifetime = life;
        initialized = true;

        // 수명 후 파괴
        Destroy(gameObject, lifetime);

        // 콜라이더 설정 (없다면 추가)
        if (GetComponent<Collider>() == null)
        {
            SphereCollider col = gameObject.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.2f;
        }
    }

    void Start()
    {
        // 기본 설정 (Initialize가 호출되지 않은 경우)
        if (!initialized)
        {
            direction = transform.forward;
            Destroy(gameObject, lifetime);
        }
    }

    void Update()
    {
        // XZ 평면에서만 이동
        Vector3 movement = direction * speed * Time.deltaTime;
        movement.y = 0;
        transform.Translate(movement, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // 플레이어에게 데미지
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage);
                Debug.Log($"[Projectile] 플레이어에게 {damage} 데미지");
            }

            // 투사체 파괴
            Destroy(gameObject);
        }
        else if (other.CompareTag("Ground"))
        {
            // 벽이나 바닥에 충돌하면 파괴
            Destroy(gameObject);
        }
    }
}
