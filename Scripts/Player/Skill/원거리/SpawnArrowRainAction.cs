using UnityEngine;
using System.Collections;

/// <summary>
/// 화살비 소환 SkillAction.
/// 
/// 동작 흐름:
///   1. 마우스 방향으로 캐릭터 회전 + 시전 애니 트리거
///   2. spawnDelay 대기 (시전 모션 길이만큼)
///   3. 마우스 위치 지면에 raycast → 그 좌표에 ArrowRain 프리팹 인스턴스화
///   4. ArrowRain.Initialize()로 모든 수치 주입 (지속시간/반경/틱간격/데미지/감속)
/// 
/// 모든 수치는 인스펙터에 노출되어 SO만 복제하면 변종 제작 가능.
/// 불씨 시스템이 추후 이 수치들을 모디파이어로 조절할 예정.
/// </summary>
[CreateAssetMenu(menuName = "Skill/Action/Spawn Arrow Rain")]
public class SpawnArrowRainAction : SkillAction
{
    [Header("화살비 프리팹")]
    [Tooltip("ArrowRain 컴포넌트가 부착된 프리팹.")]
    public GameObject arrowRainPrefab;

    // ========================================================================
    // 시전 모션
    // ========================================================================

    [Header("시전 모션")]
    [Tooltip("시전 직전 캐릭터를 마우스 방향으로 회전시킬지.")]
    public bool rotateToMouse = true;

    [Tooltip("시전 시 발동할 애니메이터 트리거 이름.\n비워두면 트리거 발동 안 함.")]
    public string castAnimTrigger = "";

    [Tooltip("애니메이션 시작 후 화살비 스폰까지 대기 시간 (초).\n시전 모션 길이에 맞춰 설정.")]
    public float spawnDelay = 0.5f;

    // ========================================================================
    // 스폰 위치
    // ========================================================================

    [Header("스폰 위치")]
    [Tooltip("지면 raycast에 사용할 레이어 (Ground 레이어).")]
    public LayerMask groundLayer;

    [Tooltip("스폰 Y축 보정. 0이면 raycast hit 높이, 양수면 위로 띄움.")]
    public float spawnHeightOffset = 0f;

    [Tooltip("raycast 실패 시(허공 클릭 등) 캐릭터 정면 N유닛 앞을 대신 사용.")]
    public float fallbackDistance = 5f;

    // ========================================================================
    // 화살비 동작 수치 (ArrowRain.Initialize에 주입)
    // ========================================================================

    [Header("지속")]
    [Tooltip("총 지속 시간 (초). 이 시간 후 자동 소멸.")]
    public float lifetime = 3f;

    [Header("데미지 판정")]
    [Tooltip("데미지 판정 반경 (유닛).")]
    public float damageRadius = 3f;

    [Tooltip("데미지 틱 간격 (초). 1.0이면 1초마다 데미지.")]
    public float damageInterval = 1f;

    [Tooltip("적 레이어 마스크.")]
    public LayerMask enemyLayer;

    [Tooltip("적이 맞을 때 표시될 히트 이펙트 타입.")]
    public HitEffectType hitEffectType = HitEffectType.Bullet;

    [Header("감속 디버프")]
    [Tooltip("적용할 속도 배율 (0.5 = 절반 속도). 값이 작을수록 더 강하게 감속.")]
    [Range(0.1f, 1f)]
    public float slowMultiplier = 0.5f;

    [Tooltip("감속 디버프 지속 시간 (초).\ndamageInterval보다 약간 길게 두면 화살비 안에 있는 동안 감속 끊김 없이 유지됨.")]
    public float slowDuration = 1.2f;

    [Header("폴백 데미지")]
    [Tooltip("damageOverride가 0 이하일 때 사용할 기본 틱 데미지.\n정상 흐름에선 SkillManager에서 계산된 값이 자동 주입됨.")]
    public int fallbackDamage = 15;

    // ========================================================================
    // 액션 실행
    // ========================================================================

    /// <summary>
    /// 시전 → 대기 → 마우스 위치에 스폰 → Initialize 순차 실행.
    /// damageOverride는 SkillManager에서 (baseDamage + 스탯 보정 + 크리)가 계산되어 주입됨.
    /// </summary>
    public override IEnumerator Execute(GameObject user, int level, KeyCode key, float damageOverride = 0)
    {
        if (arrowRainPrefab == null)
        {
            Debug.LogWarning("[SpawnArrowRainAction] arrowRainPrefab이 비어 있음 — 스폰 취소");
            yield break;
        }

        // 1. 마우스 방향 회전
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

        // 3. 시전 딜레이 — 모션 끝나는 시점까지 대기
        if (spawnDelay > 0f)
        {
            yield return new WaitForSeconds(spawnDelay);
        }

        // 4. 마우스 월드 위치 계산 (지면 raycast)
        Vector3 spawnPos = GetMouseWorldPosition(user);
        spawnPos.y += spawnHeightOffset;

        // 5. 화살비 인스턴스화
        GameObject rainObj = Instantiate(arrowRainPrefab, spawnPos, Quaternion.identity);

        // 6. ArrowRain 컴포넌트에 수치 주입
        ArrowRain rain = rainObj.GetComponent<ArrowRain>();
        if (rain != null)
        {
            int finalDamage = (damageOverride > 0) ? Mathf.RoundToInt(damageOverride) : fallbackDamage;

            rain.Initialize(
                life: lifetime,
                radius: damageRadius,
                interval: damageInterval,
                dmg: finalDamage,
                layer: enemyLayer,
                effect: hitEffectType,
                slowMult: slowMultiplier,
                slowDur: slowDuration
            );
        }
        else
        {
            Debug.LogWarning("[SpawnArrowRainAction] 스폰된 프리팹에 ArrowRain 컴포넌트가 없음!");
        }

        yield break;
    }

    /// <summary>
    /// 카메라 → 마우스 위치 → 지면 raycast로 월드 좌표 계산.
    /// raycast 실패 시 캐릭터 정면 fallbackDistance 거리로 폴백.
    /// </summary>
    private Vector3 GetMouseWorldPosition(GameObject user)
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
            {
                return hit.point;
            }
        }

        // 폴백: 캐릭터 정면 N유닛 앞
        Vector3 forward = user.transform.forward;
        forward.y = 0f;
        forward.Normalize();
        return user.transform.position + forward * fallbackDistance;
    }
}
