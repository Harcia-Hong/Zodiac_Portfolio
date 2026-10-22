using UnityEngine;

/// <summary>
/// 직사형 투사체 (총알, 화살, 검기, 마탄 등)
/// 앞으로 쭉 날아가다가 부딪히면 데미지 주고 사라짐.
/// 관통 옵션 / 불씨 거리 스케일링 지원.
/// 
/// ProjectileBase 상속 X — 자체 Initialize 시그니처 사용 (호출 측이 LaunchAction이 아닌 별도 액션들).
/// 단, ApplyEmberModifiers 시그니처는 ProjectileBase와 통일 — 호출 측에서 같은 패턴으로 사용 가능.
/// </summary>
public class LinearProjectile : MonoBehaviour
{
    private float speed;
    private int damage;
    private HitEffectType hitEffect;
    private float maxDistance; // 최대 사거리 (안 맞으면 사라지게)
    private Vector3 startPos;

    private bool isInitialized = false;

    // 관통 시스템
    private int pierceCount;                                              // 남은 관통 횟수 (0 = 첫 적만 명중 후 파괴)
    private System.Collections.Generic.HashSet<GameObject> hitTargets     // 같은 적 중복 명중 방지
        = new System.Collections.Generic.HashSet<GameObject>();

    // ========================================================================
    // 불씨 효과 (ApplyEmberModifiers에서 설정)
    // ========================================================================
    private bool hasDistanceScaling = false;
    private float distanceScalingMaxBonus = 0f;

    /// <summary>
    /// 발사체 초기화.
    /// </summary>
    /// <param name="pierce">관통 가능 적 수 (0=첫 적만 명중 후 파괴, N=N+1마리까지 명중)</param>
    public void Initialize(float moveSpeed, int dmg, float range, HitEffectType effectType, int pierce = 0)
    {
        speed = moveSpeed;
        damage = dmg;
        maxDistance = range;
        hitEffect = effectType;
        startPos = transform.position;
        pierceCount = pierce;

        isInitialized = true;

        Destroy(gameObject, 3f);
    }

    /// <summary>
    /// 슬롯에 부여된 불씨 효과 적용. Initialize 직후 호출.
    /// - DistanceScaling: 비행 거리에 따라 명중 데미지 누적 증가, 최대 N%까지
    /// 
    /// ExplosiveArrow.ApplyEmberModifiers와 동일 시그니처 — 호출 측 일관성 유지.
    /// slot == null이면 아무것도 안 함 (안전 폴백).
    /// </summary>
    public void ApplyEmberModifiers(SkillSlot slot)
    {
        if (slot == null) return;

        // 비행 거리 스케일링 — 진행도 비율로 명중 시 데미지 보너스
        if (slot.HasEmberEffect(EmberEffectType.DistanceScaling))
        {
            hasDistanceScaling = true;
            distanceScalingMaxBonus = slot.GetEmberModifierSum(EmberEffectType.DistanceScaling);
        }
    }

    void Update()
    {
        if (!isInitialized) return;

        // 1. 앞으로 이동 (전방 이동)
        transform.Translate(Vector3.forward * speed * Time.deltaTime);

        // 2. 최대 사거리 체크
        if (Vector3.Distance(startPos, transform.position) >= maxDistance)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable != null && !damageable.IsDead)
        {
            // 같은 적 중복 명중 방지 (관통 시 한 적이 여러 번 맞지 않도록)
            GameObject targetObj = other.gameObject;
            if (hitTargets.Contains(targetObj)) return;
            hitTargets.Add(targetObj);

            // [불씨] 거리 스케일링 — 현재 비행 진행도 기준으로 데미지 계산
            //        관통 중 적마다 다른 진행도가 적용되어 멀리 갈수록 더 강해지는 효과 유지
            int finalDamage = damage;
            if (hasDistanceScaling && maxDistance > 0f)
            {
                float traveled = Vector3.Distance(startPos, transform.position);
                float progress = Mathf.Clamp01(traveled / maxDistance);
                float bonusPercent = distanceScalingMaxBonus * progress;
                finalDamage = Mathf.RoundToInt(damage * (1f + bonusPercent / 100f));
            }

            damageable.ApplySkillDamage(finalDamage, hitEffect);

            // 관통 횟수 차감 — 0 이하면 파괴
            if (pierceCount <= 0)
            {
                Destroy(gameObject);
            }
            else
            {
                pierceCount--;
            }
            return;
        }

        // 벽/바닥 충돌 시 항상 파괴 (관통 무관)
        if (other.gameObject.layer == LayerMask.NameToLayer("Ground") || other.gameObject.layer == LayerMask.NameToLayer("Wall"))
        {
            Destroy(gameObject);
        }
    }
}
