using UnityEngine;
using System.Collections;

/// <summary>
/// 중력장(블랙홀) 소환 SkillAction.
/// 
/// 동작 흐름:
///   1. 마우스 월드 위치 결정 (시전 직전 한 번)
///   2. (옵션) 시전 애니메이션 트리거 + spawnDelay 대기 — 즉시 시전이면 둘 다 기본값
///   3. 마우스 위치에 BlackHole 프리팹 인스턴스화
///   4. 불씨 모디파이어 조회 (DurationPercent / RangeMultiplier / SecondaryBlast)
///   5. BlackHole.Initialize()로 모든 수치 주입
/// 
/// 사용자 결정: 즉시 시전 컨셉 (spawnDelay=0, castAnimTrigger 빈값이 기본).
/// </summary>
[CreateAssetMenu(menuName = "Skill/Action/Spawn Black Hole")]
public class SpawnBlackHoleAction : SkillAction
{
    // ========================================================================
    // 프리팹
    // ========================================================================

    [Header("프리팹")]
    [Tooltip("스폰될 블랙홀 프리팹. BlackHole 컴포넌트가 반드시 부착되어 있어야 함.")]
    public GameObject blackHolePrefab;

    [Tooltip("지속시간 종료 시 인스턴스화될 Spike VFX 프리팹. 비워두면 VFX 없이 데미지만.")]
    public GameObject spikeVFXPrefab;

    // ========================================================================
    // 시전 모션 (선택) — 즉시 시전이면 기본값 유지
    // ========================================================================

    [Header("시전 모션 (선택)")]
    [Tooltip("시전 시 발동할 애니메이터 트리거. 비워두면 모션 없이 즉시 시전.")]
    public string castAnimTrigger = "";

    [Tooltip("애니메이션 시작 후 블랙홀 스폰까지 대기 시간 (초). 즉시 시전이면 0.")]
    public float spawnDelay = 0f;

    // ========================================================================
    // 스폰 위치 보정
    // ========================================================================

    [Header("스폰 위치 보정")]
    [Tooltip("마우스 월드 위치에서 Y축 보정. 0이면 지면 높이, 양수면 위로 띄움.\n" +
             "지면 VFX가 살짝 묻혀 보일 때 0.05~0.2 정도로 띄우면 자연스러움.")]
    public float spawnHeightOffset = 0f;

    // ========================================================================
    // 블랙홀 동작 수치 (BlackHole.Initialize에 주입됨)
    // ========================================================================

    [Header("지속 / 범위")]
    [Tooltip("블랙홀 기본 지속 시간 (초). DurationPercent 불씨로 +% 가능.")]
    public float lifetime = 5f;

    [Tooltip("블랙홀 끌어당김 + Spike 폭발 범위. RangeMultiplier 불씨로 +% 가능.")]
    public float pullRadius = 4f;

    [Header("끌어당김 강도")]
    [Tooltip("Lerp 비율 기반 흡인 강도. 0.2~0.4 권장. 너무 크면 순간이동처럼 보임.")]
    [Range(0f, 1f)]
    public float pullStrength = 0.25f;

    // ========================================================================
    // Spike 폭발 옵션
    // ========================================================================

    [Header("Spike 폭발")]
    [Tooltip("적 레이어 마스크.")]
    public LayerMask enemyLayer;

    [Tooltip("Spike 폭발 시 적용될 히트 이펙트 타입.")]
    public HitEffectType hitEffectType = HitEffectType.Slashing_Flash;

    [Header("폴백 데미지")]
    [Tooltip("SkillManager에서 damageOverride를 받지 못한 경우(0 이하) 사용할 기본 Spike 데미지.\n" +
             "정상 흐름에선 SkillData의 baseDamage + 스탯/크리/불씨 보정값이 주입되므로 거의 사용되지 않음.")]
    public int fallbackDamage = 100;

    // ========================================================================
    // 액션 실행
    // ========================================================================

    /// <summary>
    /// 시전 → 마우스 위치 결정 → 스폰 → Initialize 순차 실행.
    /// damageOverride는 SkillManager에서 (baseDamage + 스탯 보정 + 크리 + DamagePercent 불씨)가 계산되어 주입됨.
    /// 불씨 효과(DurationPercent / RangeMultiplier / SecondaryBlast)는 이 액션이 직접 조회·적용.
    /// </summary>
    public override IEnumerator Execute(GameObject user, int level, KeyCode key, float damageOverride = 0)
    {
        if (blackHolePrefab == null)
        {
            Debug.LogWarning("[SpawnBlackHoleAction] blackHolePrefab이 비어 있음 — 스폰 취소");
            yield break;
        }

        PlayerController pc = user.GetComponent<PlayerController>();
        if (pc == null)
        {
            Debug.LogWarning("[SpawnBlackHoleAction] PlayerController를 찾을 수 없음");
            yield break;
        }

        // 1. 마우스 월드 위치 결정 — 시전 직전 한 번만. spawnDelay 도중 마우스 움직여도 무시
        Vector3 mouseWorldPos = pc.GetMouseWorldPosition();
        Vector3 spawnPos = new Vector3(
            mouseWorldPos.x,
            mouseWorldPos.y + spawnHeightOffset,
            mouseWorldPos.z
        );

        // 2. 시전 애니메이션 트리거 (선택)
        if (!string.IsNullOrEmpty(castAnimTrigger))
        {
            Animator anim = user.GetComponentInChildren<Animator>();
            if (anim != null) anim.SetTrigger(castAnimTrigger);
        }

        // 3. 시전 딜레이 — 즉시 시전이면 0이라 yield 없이 즉시 진행
        if (spawnDelay > 0f)
        {
            yield return new WaitForSeconds(spawnDelay);
        }

        // 4. 블랙홀 인스턴스화 (회전 무관, Quaternion.identity)
        GameObject blackHoleObj = Instantiate(blackHolePrefab, spawnPos, Quaternion.identity);

        // 5. 불씨 모디파이어 조회 — DurationPercent / RangeMultiplier / SecondaryBlast
        //    SkillManager가 없으면 0 폴백 → 영향 없는 기본 동작
        float durationBonus = 0f;
        float rangeBonus = 0f;
        float secondaryBlastPercent = 0f;
        if (SkillManager.Instance != null)
        {
            durationBonus = SkillManager.Instance.GetActiveEmberModifier(key, EmberEffectType.DurationPercent);
            rangeBonus = SkillManager.Instance.GetActiveEmberModifier(key, EmberEffectType.RangeMultiplier);
            secondaryBlastPercent = SkillManager.Instance.GetActiveEmberModifier(key, EmberEffectType.SecondaryBlast);
        }

        float finalLifetime = lifetime * (1f + durationBonus / 100f);
        float finalRadius = pullRadius * (1f + rangeBonus / 100f);
        bool hasSecondaryBlast = secondaryBlastPercent > 0f;

        // 6. Spike 데미지 결정 — damageOverride 우선, 0 이하면 폴백
        int spikeDamage = (damageOverride > 0) ? Mathf.RoundToInt(damageOverride) : fallbackDamage;

        // 7. BlackHole 컴포넌트에 모든 수치 주입
        BlackHole blackHole = blackHoleObj.GetComponent<BlackHole>();
        if (blackHole != null)
        {
            blackHole.Initialize(
                life: finalLifetime,
                radius: finalRadius,
                strength: pullStrength,
                spkDamage: spikeDamage,
                spkVFX: spikeVFXPrefab,
                layer: enemyLayer,
                effect: hitEffectType,
                secondaryBlast: hasSecondaryBlast,
                secondaryPercent: secondaryBlastPercent
            );
        }
        else
        {
            Debug.LogWarning("[SpawnBlackHoleAction] 스폰된 프리팹에 BlackHole 컴포넌트가 없음! 프리팹 확인 필요.");
        }

        yield break;
    }
}
