using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 광역 지속 데미지 액션 — 캐릭터 자식으로 VFX 부착 + 일정 간격으로 주변 광역 데미지 판정.
/// 
/// 동작:
///   1) VFX 프리팹을 캐릭터 자식으로 부착 (캐릭터 따라 이동/회전)
///   2) damageInterval 간격으로 주변 OverlapSphere 데미지 판정 반복
///   3) duration 후 VFX 제거 + 코루틴 종료
/// 
/// 활용:
///   - 윈드 슬래시 (회오리 이펙트 + 광역 지속)
///   - 화염 오라, 번개 장막 등 자체 광역 지속 스킬 전반
/// 
/// 강화 확장 자리:
///   - duration / damageRadius / damageInterval (인스펙터)
///   - 데미지 (damageOverride)
/// </summary>
[CreateAssetMenu(menuName = "Skill/Action/Aura")]
public class AuraAction : SkillAction
{
    [Header("VFX")]
    [Tooltip("캐릭터 자식으로 부착될 이펙트 프리팹.")]
    public GameObject vfxPrefab;

    [Header("지속/판정")]
    [Tooltip("스킬 지속시간 (초).")]
    public float duration = 4f;

    [Tooltip("데미지 판정 반경 (m).")]
    public float damageRadius = 2.5f;

    [Tooltip("데미지 판정 간격 (초). 짧을수록 빠른 누적.")]
    public float damageInterval = 0.3f;

    [Tooltip("적 레이어.")]
    public LayerMask enemyLayer = -1;

    [Tooltip("적중 시 표시할 히트 이펙트 타입.")]
    public HitEffectType hitEffect = HitEffectType.Slashing_Normal;

    [Header("사운드")]
    [Tooltip("시전 시작 시 1회 재생.")]
    public AudioClip castSfx;

    public override IEnumerator Execute(GameObject user, int level, KeyCode key, float damageOverride = 0)
    {
        if (user == null) yield break;

        // VFX 부착 (캐릭터 자식 — 이동/회전 자동 따라감)
        GameObject vfxInstance = null;
        if (vfxPrefab != null)
        {
            vfxInstance = Instantiate(
                vfxPrefab,
                user.transform.position,
                user.transform.rotation,
                user.transform
            );
        }

        // 시전 사운드
        if (castSfx != null && SoundManager.Instance != null)
            SoundManager.Instance.PlaySFX(castSfx);

        // 데미지 결정
        int damagePerTick = (damageOverride > 0) ? Mathf.RoundToInt(damageOverride) : 14;

        // 지속 데미지 루프
        float elapsed = 0f;
        while (elapsed < duration && user != null)
        {
            DealDamageAround(user.transform.position, damagePerTick);
            yield return new WaitForSeconds(damageInterval);
            elapsed += damageInterval;
        }

        // VFX 정리
        if (vfxInstance != null) Destroy(vfxInstance);
    }

    /// <summary>1회 광역 판정 — 반경 안 모든 적에게 데미지 (1회 판정 내 중복 방지).</summary>
    private void DealDamageAround(Vector3 pos, int damage)
    {
        Collider[] hits = Physics.OverlapSphere(pos, damageRadius, enemyLayer);
        HashSet<GameObject> hitTargets = new HashSet<GameObject>();

        foreach (var hit in hits)
        {
            GameObject obj = hit.gameObject;
            if (hitTargets.Contains(obj)) continue;
            hitTargets.Add(obj);

            IDamageable damageable = hit.GetComponent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                damageable.ApplySkillDamage(damage, hitEffect);
            }
        }
    }
}
