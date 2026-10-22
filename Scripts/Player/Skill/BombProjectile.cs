using UnityEngine;

/// <summary>
/// 실제로 날아가서 터지는 폭탄 오브젝트의 로직
/// </summary>
public class BombProjectile : MonoBehaviour
{
    private Vector3 targetPosition;
    private float speed;
    private int damage;
    private float explosionRadius;
    private HitEffectType hitEffectType;

    [Tooltip("폭발 시 생성될 이펙트 프리팹")]
    public GameObject explosionVFX;

    [Tooltip("폭발 시 재생할 효과음")]
    public AudioClip explosionSfx;

    private bool isInitialized = false;

    /// <summary>
    /// BombThrowLogic에서 호출되어 폭탄의 정보를 설정
    /// </summary>
    public void Initialize(Vector3 target, float moveSpeed, int dmg, float radius, HitEffectType effectType, float fixedDelay = -1f)
    {
        targetPosition = target;
        speed = moveSpeed;
        damage = dmg;
        explosionRadius = radius;
        hitEffectType = effectType;
        isInitialized = true;

        float timeToExplode = 0f;

        if (fixedDelay >= 0f)
            timeToExplode = fixedDelay;
        else
        {
            // Y값을 무시하고 XZ 평면상의 거리만 계산하여 이동 시간 추정
            // (포물선 로직은 복잡하므로, 여기서는 직선 이동으로 단순화)
            float distance = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(target.x, 0, target.z));

            // speed가 0일 경우(설치형인데 딜레이를 안 준 실수 등)를 대비한 안전장치
            if (speed > 0)
                timeToExplode = distance / speed;
            else
                timeToExplode = 0f;
        }

        // 일정 시간 후 폭발하도록 예약
        // (DOTween이나 포물선 계산을 쓰면 더 좋지만, 지금은 Invoke로 단순화)
        Invoke(nameof(Explode), timeToExplode);
    }

    void Update()
    {
        if (!isInitialized) return;

        // 목표 지점과 거리가 거의 0이면(트랩 설치 등) 이동하지 않음
        if (Vector3.Distance(transform.position, targetPosition) > 0.1f)
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
    }

    /// <summary>
    /// 목표 지점에 도달했거나 시간이 다 되면 폭발 — VFX + SFX + 데미지 처리
    /// </summary>
    private void Explode()
    {
        // 폭발 사운드 재생
        if (explosionSfx != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(explosionSfx);
        }

        // 폭발 이펙트 생성
        if (explosionVFX != null)
        {
            GameObject vfxInstance = Instantiate(explosionVFX, transform.position, Quaternion.identity);
            Destroy(vfxInstance, 3f);
        }

        // 폭발 범위 내의 모든 적 찾기
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (Collider hit in hits)
        {
            IDamageable damageable = hit.GetComponent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                damageable.ApplySkillDamage(damage, hitEffectType);
            }
        }

        // 폭탄 오브젝트 파괴
        Destroy(gameObject);
    }

    // 디버그용: 폭발 범위 시각화
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
