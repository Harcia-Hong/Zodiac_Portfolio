using System.Collections;
using UnityEngine;

/// <summary>
/// 중력장(블랙홀) — 마우스 지정 위치에 소환되어 지속시간 동안 적을 끌어당기는 지속형 장판.
/// 자체 틱 데미지는 없고, 지속시간 종료 시 Spike 폭발로 단일 데미지를 가함.
/// 
/// 동작 흐름:
///   1. Initialize()로 모든 수치 주입 (지속/범위/끌어당김 강도/Spike 데미지/2차 폭발 옵션)
///   2. 매 프레임: 범위 내 적 → 중앙 방향 Lerp (부드러운 흡인)
///   3. lifetime 도달 → ExplosionSequence 코루틴 시작
///        a. 블랙홀 자체 VFX 비활성 (시각적 종료)
///        b. Spike VFX 인스턴스화 + 범위 내 적에게 데미지 (1차)
///        c. (불씨 SecondaryBlast 활성 시) 짧은 딜레이 후 2차 Spike (감소된 데미지)
///        d. destroyDelayAfterBlast 대기 후 자체 GameObject 파괴
/// 
/// 재사용성:
///   Tornado와 유사한 "지속형 + 종료 폭발" 패턴. 수치만 조정해 다양한 변종 생성 가능.
/// </summary>
public class BlackHole : MonoBehaviour
{
    // ========================================================================
    // 인스펙터 설정 (프리팹 자체에서 설정)
    // ========================================================================

    [Header("자체 비주얼")]
    [Tooltip("지속시간 동안 표시되는 블랙홀 VFX (자식 GameObject).\n" +
             "종료 시 비활성화되어 Spike와 시각적으로 분리됨. 비워두면 비활성 처리 안 함.")]
    [SerializeField] private GameObject blackHoleVisual;

    [Header("종료 후 타이밍")]
    [Tooltip("Spike 폭발 후 GameObject 자체가 파괴되기까지 대기 시간 (초).\n" +
             "Spike 트레일/파티클이 자연 소멸하도록 0.5~1.0초 권장.")]
    [SerializeField] private float destroyDelayAfterBlast = 1.0f;

    [Tooltip("1차 Spike 폭발 후 2차 폭발까지 대기 시간 (초). 0.3~0.5 권장.\n" +
             "SecondaryBlast 불씨 활성 시에만 사용됨.")]
    [SerializeField] private float secondaryBlastDelay = 0.4f;

    // ========================================================================
    // 런타임 설정값 (Initialize로 주입)
    // ========================================================================

    private float lifetime;          // 총 지속 시간
    private float pullRadius;        // 끌어당김 + Spike 범위 (같이 사용)
    private float pullStrength;      // 끌어당김 강도 (0~1, Lerp 비율 기반)

    private int spikeDamage;         // 1차 Spike 데미지 (이미 SkillManager에서 크리/불씨 계산된 값)
    private GameObject spikeVFXPrefab; // 종료 시 인스턴스화할 Spike VFX
    private LayerMask enemyLayer;
    private HitEffectType hitEffect;

    // 불씨 효과 — SecondaryBlast
    private bool hasSecondaryBlast = false;
    private float secondaryBlastPercent = 0f; // 1차의 N% (예: 50)

    // ========================================================================
    // 내부 상태
    // ========================================================================

    private float elapsed = 0f;       // 누적 경과 시간
    private bool isInitialized = false;
    private bool isExploding = false; // 폭발 시퀀스 진행 중 (중복 트리거 방지)

    // ========================================================================
    // 초기화
    // ========================================================================

    /// <summary>
    /// 블랙홀 초기화 — SpawnBlackHoleAction에서 호출.
    /// </summary>
    /// <param name="life">지속 시간 (초)</param>
    /// <param name="radius">끌어당김 + Spike 범위</param>
    /// <param name="strength">끌어당김 강도 (0~1, Lerp 비율)</param>
    /// <param name="spkDamage">1차 Spike 데미지 (SkillManager 계산값)</param>
    /// <param name="spkVFX">Spike VFX 프리팹 (null 가능)</param>
    /// <param name="layer">적 레이어 마스크</param>
    /// <param name="effect">피격 히트 이펙트 타입</param>
    /// <param name="secondaryBlast">SecondaryBlast 불씨 활성 여부</param>
    /// <param name="secondaryPercent">2차 폭발 데미지 비율 (예: 50 = 1차의 50%)</param>
    public void Initialize(
        float life,
        float radius,
        float strength,
        int spkDamage,
        GameObject spkVFX,
        LayerMask layer,
        HitEffectType effect,
        bool secondaryBlast,
        float secondaryPercent)
    {
        lifetime = life;
        pullRadius = radius;
        pullStrength = strength;
        spikeDamage = spkDamage;
        spikeVFXPrefab = spkVFX;
        enemyLayer = layer;
        hitEffect = effect;
        hasSecondaryBlast = secondaryBlast;
        secondaryBlastPercent = secondaryPercent;

        isInitialized = true;
    }

    // ========================================================================
    // 메인 루프
    // ========================================================================

    private void Update()
    {
        // 폭발 시퀀스 진행 중에는 끌어당김 중단
        if (!isInitialized || isExploding) return;

        elapsed += Time.deltaTime;

        // 지속시간 종료 → 폭발 시퀀스 시작
        if (elapsed >= lifetime)
        {
            StartCoroutine(ExplosionSequence());
            return;
        }

        // 평상시: 범위 내 적 끌어당김
        PullEnemiesToCenter();
    }

    /// <summary>
    /// 범위 내 모든 적을 중심 방향으로 부드럽게 Lerp 흡인.
    /// Y축 보존 — 수평 평면에서만 이동.
    /// </summary>
    private void PullEnemiesToCenter()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, pullRadius, enemyLayer);
        if (hits == null || hits.Length == 0) return;

        foreach (var col in hits)
        {
            Vector3 enemyPos = col.transform.position;
            Vector3 targetPos = transform.position;
            targetPos.y = enemyPos.y; // 적 높이 유지 (3D 쿼터뷰 — 떠오르지 않게)

            col.transform.position = Vector3.Lerp(
                enemyPos,
                targetPos,
                pullStrength * Time.deltaTime * 5f
            );
        }
    }

    // ========================================================================
    // 폭발 시퀀스
    // ========================================================================

    /// <summary>
    /// 종료 폭발 시퀀스 — 코루틴으로 1·2차 Spike 타이밍 제어.
    ///   1) 블랙홀 자체 VFX 비활성 (시각적 분리)
    ///   2) 1차 Spike 폭발 + 범위 내 데미지
    ///   3) (SecondaryBlast 불씨) 딜레이 → 2차 Spike (감소된 데미지)
    ///   4) destroyDelayAfterBlast 대기 후 GameObject 파괴
    /// </summary>
    private IEnumerator ExplosionSequence()
    {
        isExploding = true;

        // 1) 블랙홀 비주얼 끄기 — Spike와 시각적 분리
        if (blackHoleVisual != null)
        {
            blackHoleVisual.SetActive(false);
        }

        // 2) 1차 Spike 폭발
        TriggerSpikeBlast(spikeDamage);

        // 3) 2차 폭발 (불씨 활성 시)
        if (hasSecondaryBlast && secondaryBlastPercent > 0f)
        {
            yield return new WaitForSeconds(secondaryBlastDelay);

            int secondaryDmg = Mathf.Max(1, Mathf.RoundToInt(spikeDamage * (secondaryBlastPercent / 100f)));
            TriggerSpikeBlast(secondaryDmg);
        }

        // 4) Spike 이펙트 자연 소멸 대기 후 자체 파괴
        yield return new WaitForSeconds(destroyDelayAfterBlast);
        Destroy(gameObject);
    }

    /// <summary>
    /// Spike 폭발 단발 처리 — VFX 인스턴스화 + 범위 내 적에게 데미지.
    /// 1차/2차 공통 사용.
    /// </summary>
    private void TriggerSpikeBlast(int damage)
    {
        // Spike VFX 인스턴스화
        if (spikeVFXPrefab != null)
        {
            Instantiate(spikeVFXPrefab, transform.position, Quaternion.identity);
        }

        // 범위 내 적 데미지 적용
        Collider[] hits = Physics.OverlapSphere(transform.position, pullRadius, enemyLayer);
        if (hits == null || hits.Length == 0) return;

        foreach (var col in hits)
        {
            IDamageable damageable = col.GetComponent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                damageable.ApplySkillDamage(damage, hitEffect);
            }
        }
    }

    // ========================================================================
    // 디버그
    // ========================================================================

    /// <summary>씬 뷰에서 블랙홀 범위 시각화.</summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, pullRadius);
    }
}
