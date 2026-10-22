using UnityEngine;
using System.Collections;

/// <summary>
/// 부채꼴 차징 발사 액션 — 키 홀드 동안 차징, 떼면 부채꼴로 여러 발사체 발사.
/// 
/// 동작:
///   - 차징 시간이 길수록 발사체 수 증가 (예: 3개 → 7개)
///   - 발사체는 부채꼴 각도로 균등 분포
///   - 데미지는 발사체 1개당 고정 (damageOverride에서 받음)
///   - 시전 중 이동 잠금 (부채꼴 발사는 정밀 조준 필요)
///   - 회피로 캔슬 가능
/// 
/// 발사체: LinearProjectile 사용 (관통 옵션 활용).
/// 
/// 활용 예: 난무하는 검기, 부채꼴 화살 난사 등.
/// </summary>
[CreateAssetMenu(menuName = "Skill/Action/Fan Projectile Charge")]
public class FanProjectileChargeAction : SkillAction
{
    // ============================================================
    // 차징 설정
    // ============================================================
    [Header("차징 설정")]
    [Tooltip("최대 차징 시간 (초). 1.5초 권장.")]
    public float maxChargeTime = 1.5f;

    [Tooltip("캐스트 바 단계 표시 수 (시각용).")]
    public int chargeStages = 3;

    // ============================================================
    // 발사체 수
    // ============================================================
    [Header("발사체 수")]
    [Tooltip("차징 0초 시 발사체 수 (최소).")]
    public int minProjectiles = 3;

    [Tooltip("차징 max 시 발사체 수 (최대).")]
    public int maxProjectiles = 7;

    // ============================================================
    // 부채꼴 발사
    // ============================================================
    [Header("부채꼴 발사")]
    [Tooltip("부채꼴 총 각도 (도). 90도면 좌우 ±45도.")]
    public float spreadAngle = 90f;

    // ============================================================
    // 발사체 설정
    // ============================================================
    [Header("발사체 설정")]
    [Tooltip("발사체 프리팹 (LinearProjectile 부착 필요).")]
    public GameObject projectilePrefab;

    [Tooltip("발사체 비행 속도.")]
    public float speed = 45f;

    [Tooltip("최대 사거리 (m).")]
    public float range = 10f;

    [Tooltip("적중 시 히트 이펙트.")]
    public HitEffectType hitEffect = HitEffectType.Slashing_Normal;

    [Tooltip("관통 횟수 (0=첫 적만, N=N+1마리까지). 검기는 5~10 권장.")]
    public int pierceCount = 5;

    [Header("일점사 옵션")]
    [Tooltip("일점사 불씨 활성 시 발사체 사이 간격 (초).\n" +
         "0.05~0.10이 '파파팍' 느낌에 적당. 너무 크면 끊겨 보임.")]
    public float focusFireInterval = 0.05f;

    // ============================================================
    // Execute (메인 코루틴)
    // ============================================================

    /// <summary>
    /// 흐름:
    ///   1) 불씨 조회 (MaxChargeTimeReduction) → 실효 최대 차징 시간 결정
    ///   2) 캐스트 바 표시 + 이동 잠금
    ///   3) 키 홀드 동안 차징 누적 (실효 최대 시간까지)
    ///   4) 키 떼거나 max 도달 시 발사
    ///   5) 회피 시 캔슬 (발사 X)
    /// </summary>
    public override IEnumerator Execute(GameObject user, int level, KeyCode key, float damageOverride = 0)
    {
        PlayerController pc = user.GetComponent<PlayerController>();

        // [불씨] 빠른 난무 — 최대 차징 시간 단축
        //        조회 실패 시 0 → maxChargeTime 그대로 유지
        float effectiveMaxCharge = maxChargeTime;
        if (SkillManager.Instance != null)
        {
            float reduction = SkillManager.Instance.GetActiveEmberModifier(key, EmberEffectType.MaxChargeTimeReduction);
            effectiveMaxCharge = Mathf.Max(0.1f, maxChargeTime - reduction);
        }

        // 캐스트 바 UI — 실효 시간 기준으로 표시
        CastBarUI castBar = FindFirstObjectByType<CastBarUI>(FindObjectsInactive.Include);
        if (castBar != null) castBar.Show("난무", effectiveMaxCharge, true, chargeStages);

        // 이동 잠금
        if (pc != null)
        {
            pc.isSkillCasting = true;
            pc.isCharging = true;
        }

        float currentCharge = 0f;
        bool isCancelled = false;

        // 차징 루프 — 실효 최대 차징 시간까지 누적
        while (Input.GetKey(key) && currentCharge < effectiveMaxCharge)
        {
            currentCharge += Time.deltaTime;

            // 회피로 캔슬
            if (pc != null && pc.isDodging)
            {
                isCancelled = true;
                break;
            }
            yield return null;
        }

        // 정리
        if (castBar != null) castBar.Hide();
        if (pc != null)
        {
            pc.isSkillCasting = false;
            pc.isCharging = false;
        }

        // 발사 — 일점사 불씨 분기는 FireFanProjectiles 내부에서 처리
        if (!isCancelled)
        {
            yield return FireFanProjectiles(user, currentCharge, effectiveMaxCharge, damageOverride, key);
        }
    }

    // ============================================================
    // Fire
    // ============================================================

    /// <summary>
    /// 차징 비율로 발사체 수 결정 후 발사.
    /// 일점사 불씨 활성 시: 같은 방향으로 focusFireInterval 간격 순차 발사 + 2번째+ 발사체 데미지 감소.
    /// 일점사 비활성 시: 부채꼴 균등 분포 동시 발사.
    /// </summary>
    private IEnumerator FireFanProjectiles(GameObject user, float chargeTime, float effectiveMaxCharge, float damageOverride, KeyCode key)
    {
        PlayerController pc = user.GetComponent<PlayerController>();
        if (pc == null) yield break;

        // 부채꼴 중심선 = 마우스 방향
        Vector3 spawnPos = user.transform.position + Vector3.up * 1.0f;
        Vector3 mouseWorldPos = pc.GetMouseWorldPosition();
        Vector3 targetPosFlat = new Vector3(mouseWorldPos.x, spawnPos.y, mouseWorldPos.z);
        Vector3 centerDirection = (targetPosFlat - spawnPos).normalized;

        // 캐릭터 회전
        user.transform.LookAt(new Vector3(mouseWorldPos.x, user.transform.position.y, mouseWorldPos.z));

        // 차징 비율로 발사체 수 결정 (실효 최대 차징 시간 기준)
        float chargeRatio = Mathf.Clamp01(chargeTime / effectiveMaxCharge);
        int projectileCount = Mathf.RoundToInt(Mathf.Lerp(minProjectiles, maxProjectiles, chargeRatio));
        projectileCount = Mathf.Max(1, projectileCount);

        // 데미지 (발사체 1개당). damageOverride > 0이면 사용, 아니면 폴백 60.
        int damagePerProjectile = (damageOverride > 0) ? Mathf.RoundToInt(damageOverride) : 60;

        // [불씨] 일점사 — 부채꼴 무시, 모두 중심 방향. 2번째+ 발사체 데미지 -focusPenalty%
        float focusPenalty = 0f;
        if (SkillManager.Instance != null)
        {
            focusPenalty = SkillManager.Instance.GetActiveEmberModifier(key, EmberEffectType.FanFocusSinglePoint);
        }
        bool isFocusFire = focusPenalty > 0f;

        // [불씨] 활성 슬롯을 한 번만 조회 — 발사체별로 같은 슬롯의 모디파이어 적용
        //        DistanceScaling 등 발사체 본체가 직접 처리하는 효과를 위해 SpawnProjectile에 전달
        SkillSlot activeSlot = null;
        if (SkillManager.Instance != null)
        {
            activeSlot = SkillManager.Instance.GetSlot(SkillManager.Instance.GetActiveCategory(), key);
        }

        if (isFocusFire)
        {
            // 일점사 모드: 같은 방향, 인덱스별 데미지 차등, focusFireInterval 간격 순차 발사
            float penaltyMult = 1f - Mathf.Clamp01(focusPenalty / 100f);
            for (int i = 0; i < projectileCount; i++)
            {
                int dmg = (i == 0)
                    ? damagePerProjectile
                    : Mathf.Max(1, Mathf.RoundToInt(damagePerProjectile * penaltyMult));
                SpawnProjectile(spawnPos, centerDirection, dmg, activeSlot);

                // 마지막 발사체 뒤엔 대기 X — 불필요한 지연 방지
                if (i < projectileCount - 1)
                {
                    yield return new WaitForSeconds(focusFireInterval);
                }
            }
        }
        else
        {
            // 부채꼴 모드 (기존): 균등 각도 동시 발사
            for (int i = 0; i < projectileCount; i++)
            {
                float angle = (projectileCount == 1)
                    ? 0f
                    : Mathf.Lerp(-spreadAngle / 2f, spreadAngle / 2f, (float)i / (projectileCount - 1));

                Vector3 fireDirection = Quaternion.AngleAxis(angle, Vector3.up) * centerDirection;
                SpawnProjectile(spawnPos, fireDirection, damagePerProjectile, activeSlot);
            }
        }
    }

    /// <summary>
    /// 발사체 1개 생성 + LinearProjectile 초기화 (관통 옵션 포함) + 불씨 모디파이어 적용.
    /// </summary>
    private void SpawnProjectile(Vector3 spawnPos, Vector3 direction, int damage, SkillSlot slot)
    {
        if (projectilePrefab == null) return;

        Quaternion lookRot = Quaternion.LookRotation(direction);
        GameObject proj = Instantiate(projectilePrefab, spawnPos, lookRot);

        LinearProjectile linearProj = proj.GetComponent<LinearProjectile>();
        if (linearProj != null)
        {
            linearProj.Initialize(speed, damage, range, hitEffect, pierceCount);

            // [불씨] 발사체 자체 처리 효과 적용 (DistanceScaling 등)
            //        slot이 null이어도 LinearProjectile.ApplyEmberModifiers가 안전 폴백
            linearProj.ApplyEmberModifiers(slot);
        }
        else
        {
            Debug.LogWarning("[FanProjectileChargeAction] projectilePrefab에 LinearProjectile 없음");
        }
    }
}
