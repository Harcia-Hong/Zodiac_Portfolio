using System.Collections;
using UnityEngine;

/// <summary>
/// 화살비 본체 — 지정 위치에 떨어지는 광역 공격 컴포넌트.
/// 
/// 두 가지 모드 지원 (singleHit 토글):
///   - 단일 발동: 시작 시 한 번 데미지 + 감속 적용, 이후 lifetime 동안 VFX만 재생
///   - 지속 장판: damageInterval마다 반복 데미지 + 감속 (Tornado 패턴)
/// 
/// 모든 수치는 Initialize로 외부 주입.
/// 불씨 시스템에서 다음 수치들을 모디파이어로 곱연산 가능:
///   - lifetime, damageRadius, damagePerTick, damageInterval, slowMultiplier
/// </summary>
public class ArrowRain : MonoBehaviour
{
    // ========================================================================
    // 발동 모드 (인스펙터 토글)
    // ========================================================================

    [Header("발동 모드")]
    [Tooltip("체크하면 시작 시 한 번만 데미지 + 감속 적용 (단일 발동).\n" +
             "체크 해제 시 damageInterval마다 반복 데미지 (지속 장판).")]
    [SerializeField] private bool singleHit = true;

    [Header("VFX 옵션")]
    [Tooltip("체크하면 lifetime 동안 자식 ParticleSystem을 강제로 반복 재생.\n" +
             "지속 발동(singleHit=false)에서 burst형 VFX 반복시킬 때 사용.\n" +
             "단일 발동에서는 꺼둘 것 — VFX가 자기 길이대로 1회만 재생됨.")]
    [SerializeField] private bool loopChildVFX = false;

    // ========================================================================
    // 런타임 설정값 (Initialize로 주입)
    // ========================================================================

    private float lifetime;
    private float damageRadius;
    private float damageInterval;
    private int damagePerTick;
    private LayerMask enemyLayer;
    private HitEffectType hitEffectType;

    private float slowMultiplier;
    private float slowDuration;

    // ========================================================================
    // 내부 상태
    // ========================================================================

    private float damageTimer = 0f;
    private bool isInitialized = false;

    // ========================================================================
    // 초기화
    // ========================================================================

    /// <summary>
    /// 화살비 초기화 — SpawnArrowRainAction에서 호출.
    /// singleHit=true면 즉시 한 번 데미지 적용 + 이후 VFX만 보임.
    /// </summary>
    public void Initialize(
        float life,
        float radius,
        float interval,
        int dmg,
        LayerMask layer,
        HitEffectType effect,
        float slowMult,
        float slowDur)
    {
        lifetime = life;
        damageRadius = radius;
        damageInterval = interval;
        damagePerTick = dmg;
        enemyLayer = layer;
        hitEffectType = effect;
        slowMultiplier = slowMult;
        slowDuration = slowDur;

        isInitialized = true;

        // 단일 발동: 시작 시 한 번 데미지 + 감속 즉시 적용
        if (singleHit)
        {
            ApplyDamageAndSlow();
        }

        // VFX 반복 처리 (옵션)
        if (loopChildVFX)
        {
            ApplyLoopVFX();
        }

        // 지속시간 만료 시 자동 파괴
        Destroy(gameObject, lifetime);
    }

    // ========================================================================
    // 메인 루프
    // ========================================================================

    private void Update()
    {
        if (!isInitialized) return;

        // 단일 발동은 Initialize에서 이미 처리 완료 — 이후엔 VFX만
        if (singleHit) return;

        // 지속 발동: damageInterval마다 데미지
        damageTimer += Time.deltaTime;
        if (damageTimer < damageInterval) return;

        damageTimer = 0f;
        ApplyDamageAndSlow();
    }

    /// <summary>
    /// 범위 내 적 검색 → 데미지 + 감속 디버프 일괄 적용.
    /// </summary>
    private void ApplyDamageAndSlow()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, damageRadius, enemyLayer);
        if (hits == null || hits.Length == 0) return;

        foreach (var col in hits)
        {
            // 데미지 적용 (IDamageable — Enemy/Boss/Breakable 모두 호환)
            IDamageable damageable = col.GetComponent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                damageable.ApplySkillDamage(damagePerTick, hitEffectType);
            }

            // 감속 디버프 (Enemy 전용)
            Enemy enemy = col.GetComponent<Enemy>();
            if (enemy != null && !enemy.isDead)
            {
                enemy.ApplySlowDebuff(slowMultiplier, slowDuration);
            }
        }
    }

    /// <summary>
    /// 자식 ParticleSystem을 모두 looping으로 변환 + duration을 lifetime에 맞춤.
    /// 재생 중일 때 duration 변경 시 에러 → Stop 먼저 → 변경 → Play 순서로 처리.
    /// </summary>
    private void ApplyLoopVFX()
    {
        ParticleSystem[] childParticles = GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in childParticles)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var mainModule = ps.main;
            mainModule.loop = true;
            mainModule.duration = lifetime;

            ps.Play();
        }
    }

    // ========================================================================
    // 디버그
    // ========================================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, damageRadius);
    }
}
