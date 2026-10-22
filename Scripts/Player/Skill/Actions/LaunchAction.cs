using UnityEngine;
using System.Collections;

/// <summary>
/// 범용 직선 발사체 SkillAction.
/// 
/// ProjectileBase 컴포넌트가 부착된 어떤 프리팹이든 마우스 방향으로 발사 가능.
/// 발사체 종류별 특수 동작(폭발, 감속, 관통 등)은 각 ProjectileBase 자식 클래스가 처리.
/// 
/// 동작 흐름:
///   1. 마우스 방향으로 캐릭터 회전 + 시전 애니 트리거
///   2. spawnDelay 대기 (시전 모션 길이만큼)
///   3. 캐릭터 forward 방향으로 발사체 인스턴스화
///   4. ProjectileBase.Initialize()로 공통 수치(방향/속도/사거리/데미지/레이어) 주입
/// 
/// 사용 예:
///   - 폭발 화살 (ExplosiveArrow) — 폭렬 미사일
///   - 얼음 화살, 독 화살, 관통 화살 등 어떤 직선 발사체든 동일 액션으로
/// </summary>
[CreateAssetMenu(menuName = "Skill/Action/Launch")]
public class LaunchAction : SkillAction
{
    // ========================================================================
    // 발사체 프리팹
    // ========================================================================

    [Header("발사체 프리팹")]
    [Tooltip("ProjectileBase를 상속한 컴포넌트가 부착된 프리팹.")]
    public GameObject projectilePrefab;

    // ========================================================================
    // 시전 모션
    // ========================================================================

    [Header("시전 모션")]
    [Tooltip("시전 직전 캐릭터를 마우스 방향으로 회전시킬지.")]
    public bool rotateToMouse = true;

    [Tooltip("시전 시 발동할 애니메이터 트리거 이름. 비워두면 트리거 없음.")]
    public string castAnimTrigger = "";

    [Tooltip("애니메이션 시작 후 발사까지 대기 시간 (초).\n시전 모션 길이에 맞춰 설정.")]
    public float spawnDelay = 0.3f;

    // ========================================================================
    // 스폰 위치
    // ========================================================================

    [Header("스폰 위치")]
    [Tooltip("캐릭터 위치 기준 발사체 스폰 오프셋 (로컬 좌표).\n" +
             "예: (0, 1, 0.5)면 캐릭터 머리 위 + 살짝 앞쪽에서 발사.")]
    public Vector3 spawnOffset = new Vector3(0f, 1f, 0.5f);

    // ========================================================================
    // 발사체 동작 수치 (ProjectileBase.Initialize에 주입)
    // ========================================================================

    [Header("발사 수치")]
    [Tooltip("발사체 이동 속도 (유닛/초).")]
    public float projectileSpeed = 20f;

    [Tooltip("최대 사거리 (유닛). 도달 시 발사체별 종료 처리(폭발/소멸).")]
    public float maxRange = 15f;

    [Tooltip("적 레이어 마스크.")]
    public LayerMask enemyLayer;

    [Tooltip("벽/장애물 레이어 마스크.")]
    public LayerMask obstacleLayer;

    [Tooltip("적이 맞을 때 표시될 히트 이펙트 타입.")]
    public HitEffectType hitEffectType = HitEffectType.Explosion_Bomb;

    [Header("폴백 데미지")]
    [Tooltip("damageOverride가 0 이하일 때 사용할 기본 데미지.\n정상 흐름에선 SkillManager에서 계산된 값이 자동 주입됨.")]
    public int fallbackDamage = 60;

    // ========================================================================
    // 액션 실행
    // ========================================================================

    /// <summary>
    /// 시전 → 대기 → 발사 순차 실행.
    /// damageOverride는 SkillManager에서 (baseDamage + 스탯 보정 + 일반 크리)가 적용된 값.
    /// 직격 크리 같은 발사체 고유 보정은 ProjectileBase 자식이 처리.
    /// </summary>
    public override IEnumerator Execute(GameObject user, int level, KeyCode key, float damageOverride = 0)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("[LaunchAction] projectilePrefab이 비어 있음 — 발사 취소");
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

        // 4. 스폰 위치/방향 계산 — 캐릭터의 현재 forward 사용 (회전 후라 마우스 방향)
        Vector3 launchDirection = user.transform.forward;
        launchDirection.y = 0f;
        launchDirection.Normalize();

        Vector3 spawnPos = user.transform.position + user.transform.TransformDirection(spawnOffset);

        // 5. 발사체 인스턴스화
        GameObject obj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);

        // 6. ProjectileBase 공통 초기화 호출
        ProjectileBase projectile = obj.GetComponent<ProjectileBase>();
        if (projectile != null)
        {
            int finalDamage = (damageOverride > 0) ? Mathf.RoundToInt(damageOverride) : fallbackDamage;

            projectile.Initialize(
                direction: launchDirection,
                speed: projectileSpeed,
                range: maxRange,
                damage: finalDamage,
                enemyLayer: enemyLayer,
                obstacleLayer: obstacleLayer,
                effect: hitEffectType
            );

            // [NEW] 슬롯에 부여된 불씨 효과 적용 (범위 확장·거리 스케일링 등)
            SkillSlot slot = SkillManager.Instance?.GetCurrentSlot(key);
            projectile.ApplyEmberModifiers(slot);
        }
        else
        {
            Debug.LogWarning("[LaunchAction] 스폰된 프리팹에 ProjectileBase 컴포넌트가 없음!");
        }

        yield break;
    }
}
