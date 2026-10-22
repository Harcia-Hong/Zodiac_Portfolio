using UnityEngine;
using System.Collections;

/// <summary>
/// 토네이도 소환 SkillAction.
/// 
/// 동작 흐름:
///   1. 마우스 방향으로 캐릭터 회전 (rotateToMouse 옵션)
///   2. 시전 애니메이션 트리거 (castAnimTrigger 비어있지 않으면)
///   3. spawnDelay만큼 대기 — 시전 모션이 끝나는 시점까지
///   4. 회전된 캐릭터의 forward 방향 약간 앞에 Tornado 프리팹 인스턴스화
///   5. Tornado.Initialize()로 모든 수치 주입 (이동/지속/데미지)
/// 
/// 변종 제작:
///   이 SO를 복제하고 수치만 바꾸면 불씨, 얼음 폭풍, 강력한 폭풍 등 즉시 제작 가능.
///   토네이도 프리팹의 Tornado 컴포넌트 자체에 있는 pullEnemiesToCenter 같은 옵션과 조합하면 다양한 변종 표현 가능.
/// </summary>
[CreateAssetMenu(menuName = "Skill/Action/Spawn Tornado")]
public class SpawnTornadoAction : SkillAction
{
    [Header("토네이도 프리팹")]
    [Tooltip("스폰될 토네이도 프리팹. Tornado 컴포넌트가 반드시 부착되어 있어야 함.")]
    public GameObject tornadoPrefab;

    // ========================================================================
    // 시전 모션 설정
    // ========================================================================

    [Header("시전 모션")]
    [Tooltip("시전 직전 캐릭터를 마우스 방향으로 회전시킬지.\n" +
             "체크 시 마우스가 뒤쪽에 있어도 캐릭터가 그쪽을 향한 채 시전.")]
    public bool rotateToMouse = true;

    [Tooltip("시전 시 발동할 애니메이터 트리거 이름.\n" +
             "비워두면 트리거 발동 안 함 (모션 없이 즉시 스폰).")]
    public string castAnimTrigger = "";

    [Tooltip("애니메이션 시작 후 토네이도 스폰까지 대기 시간 (초).\n" +
             "시전 모션 길이에 맞춰 설정 — 모션이 끝나는 시점에 토네이도가 나오도록.\n" +
             "이 시간 동안 SkillData.blockMovement가 true면 캐릭터 락 유지됨.")]
    public float spawnDelay = 0.5f;

    // ========================================================================
    // 스폰 위치
    // ========================================================================

    [Header("스폰 위치")]
    [Tooltip("캐릭터 정면 기준 스폰 거리 (유닛). 캐릭터 forward × 이 값만큼 앞쪽에서 생성.")]
    public float spawnForwardOffset = 2.5f;

    [Tooltip("스폰 Y축 보정. 0이면 캐릭터 발 높이, 양수면 위로 띄움.\n" +
             "지형이 평평하지 않을 때 미세 조정용.")]
    public float spawnHeightOffset = 0f;

    // ========================================================================
    // 토네이도 동작 수치 (Tornado.Initialize에 주입됨)
    // ========================================================================

    [Header("이동 설정")]
    [Tooltip("토네이도 전진 속도 (유닛/초).")]
    public float moveSpeed = 2f;

    [Tooltip("이동 후 정지하는 거리 (유닛). 이 거리까지 가면 멈춰서 그 자리에서 지속 데미지.")]
    public float travelDistance = 6f;

    [Header("지속 시간")]
    [Tooltip("토네이도 총 지속 시간 (초). 이 시간 후 자동 소멸.")]
    public float lifetime = 5f;

    [Header("데미지 판정")]
    [Tooltip("데미지 판정 반경 (유닛).")]
    public float damageRadius = 2f;

    [Tooltip("데미지 틱 간격 (초). 이 간격마다 범위 내 적에게 데미지.")]
    public float damageInterval = 0.5f;

    [Tooltip("적 레이어 마스크.")]
    public LayerMask enemyLayer;

    [Tooltip("적이 맞을 때 표시될 히트 이펙트 타입.")]
    public HitEffectType hitEffectType = HitEffectType.Tornado;

    [Header("폴백 데미지")]
    [Tooltip("SkillManager에서 damageOverride를 받지 못한 경우(0 이하) 사용할 기본 틱 데미지.\n" +
             "정상 흐름에선 SkillData의 baseDamage + 스탯 보정값이 자동 주입되므로 거의 사용되지 않음.")]
    public int fallbackDamage = 20;

    // ========================================================================
    // 액션 실행
    // ========================================================================

    /// <summary>
    /// 시전 → 대기 → 스폰 → Initialize 순차 실행.
    /// damageOverride는 SkillManager에서 (baseDamage + 스탯 보정 + 크리)가 계산되어 주입됨.
    /// 불씨 모디파이어(DurationPercent / RangeMultiplier)는 이 액션에서 직접 조회·적용.
    /// </summary>
    public override IEnumerator Execute(GameObject user, int level, KeyCode key, float damageOverride = 0)
    {
        if (tornadoPrefab == null)
        {
            Debug.LogWarning("[SpawnTornadoAction] tornadoPrefab이 비어 있음 — 스폰 취소");
            yield break;
        }

        // 1. 마우스 방향으로 캐릭터 회전
        if (rotateToMouse)
        {
            PlayerController pc = user.GetComponent<PlayerController>();
            if (pc != null) pc.RotateToMouseDirection();
        }

        // 2. 시전 애니메이션 트리거
        if (!string.IsNullOrEmpty(castAnimTrigger))
        {
            Animator anim = user.GetComponentInChildren<Animator>();
            if (anim != null) anim.SetTrigger(castAnimTrigger);
        }

        // 3. 시전 딜레이 — 모션이 끝나는 시점까지 대기
        if (spawnDelay > 0f)
        {
            yield return new WaitForSeconds(spawnDelay);
        }

        // 4. 회전된 캐릭터 forward 기준으로 스폰 위치/회전 계산
        Vector3 forward = user.transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 spawnPos = user.transform.position
                         + forward * spawnForwardOffset
                         + Vector3.up * spawnHeightOffset;

        Quaternion spawnRot = Quaternion.LookRotation(forward);

        // 5. 토네이도 인스턴스화
        GameObject tornadoObj = Instantiate(tornadoPrefab, spawnPos, spawnRot);

        // 6. 불씨 모디파이어 조회 — DurationPercent / RangeMultiplier
        //    조회 실패 시 0 반환되므로 (1 + 0/100) = 1로 안전한 폴백
        float durationBonus = 0f;
        float rangeBonus = 0f;
        if (SkillManager.Instance != null)
        {
            durationBonus = SkillManager.Instance.GetActiveEmberModifier(key, EmberEffectType.DurationPercent);
            rangeBonus = SkillManager.Instance.GetActiveEmberModifier(key, EmberEffectType.RangeMultiplier);
        }

        float finalLifetime = lifetime * (1f + durationBonus / 100f);
        float finalRadius = damageRadius * (1f + rangeBonus / 100f);
        float finalTravelDistance = travelDistance * (1f + rangeBonus / 100f);

        // 7. Tornado 컴포넌트에 모든 수치 주입
        Tornado tornado = tornadoObj.GetComponent<Tornado>();
        if (tornado != null)
        {
            // damageOverride가 정상이면 그대로 사용, 0 이하면 폴백
            int finalDamage = (damageOverride > 0) ? Mathf.RoundToInt(damageOverride) : fallbackDamage;

            tornado.Initialize(
                direction: forward,
                speed: moveSpeed,
                distance: finalTravelDistance,
                life: finalLifetime,
                radius: finalRadius,
                interval: damageInterval,
                dmg: finalDamage,
                layer: enemyLayer,
                effect: hitEffectType
            );
        }
        else
        {
            Debug.LogWarning("[SpawnTornadoAction] 스폰된 프리팹에 Tornado 컴포넌트가 없음! 프리팹 확인 필요.");
        }

        yield break;
    }
}
