using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 회전 칼날 액션 — 캐릭터 주변에 칼날 N개를 균등 각도로 배치 + Container 회전 + 광역 지속 데미지.
/// 
/// 동작:
///   1) Container 생성 (캐릭터 자식 — 이동 자동 따라감)
///   2) 칼날 N개를 균등 각도 (360 / N)로 Container 자식 배치
///   3) Container에 RotatingBlades 부착 (자체 회전 + 자동 소멸)
///   4) duration 동안 damageInterval 간격으로 주변 광역 데미지 판정
///   5) duration 후 Container 자동 소멸 (RotatingBlades가 처리)
/// 
/// 강화 확장 자리:
///   - 칼날 수 / 궤도 반경 / 회전 속도 (시각)
///   - 지속시간 / 판정 간격 / 데미지 반경 (데미지)
/// 
/// 의존성:
///   - RotatingBlades 컴포넌트 (Container 회전 + 소멸 자동화)
/// </summary>
[CreateAssetMenu(menuName = "Skill/Action/Rotating Blades")]
public class RotatingBladesAction : SkillAction
{
    [Header("칼날 프리팹 & 배치")]
    [Tooltip("칼날 1개 프리팹 (검 모델 + Trail Renderer 등).")]
    public GameObject bladePrefab;

    [Tooltip("칼날 수. 균등 각도로 배치됨.")]
    public int bladeCount = 3;

    [Tooltip("궤도 반경 (m). 캐릭터 중심에서 칼날까지 거리.")]
    public float orbitRadius = 2f;

    [Header("회전")]
    [Tooltip("회전 속도 (초당 도). 360 = 1초에 1바퀴.")]
    public float rotateSpeed = 360f;

    [Header("지속/판정")]
    [Tooltip("스킬 지속시간 (초).")]
    public float duration = 4f;

    [Tooltip("데미지 판정 반경 (m). 보통 orbitRadius보다 약간 크게.")]
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

        // 1. Container 생성 (캐릭터 자식 — 캐릭터 이동/회전 자동 따라감)
        GameObject container = new GameObject("RotatingBlades_Container");
        container.transform.SetParent(user.transform);
        container.transform.localPosition = Vector3.zero;
        container.transform.localRotation = Quaternion.identity;

        // 2. 칼날 N개 균등 각도 배치
        SpawnBlades(container.transform);

        // 3. RotatingBlades 컴포넌트 부착 (자체 회전 + 자동 소멸)
        RotatingBlades rotator = container.AddComponent<RotatingBlades>();
        rotator.rotateSpeed = rotateSpeed;
        rotator.duration = duration;

        // 4. 시전 사운드
        if (castSfx != null && SoundManager.Instance != null)
            SoundManager.Instance.PlaySFX(castSfx);

        // 5. 데미지 결정
        int damagePerTick = (damageOverride > 0) ? Mathf.RoundToInt(damageOverride) : 14;

        // 6. 지속 데미지 루프 (캐릭터 위치 기준)
        float elapsed = 0f;
        while (elapsed < duration && user != null)
        {
            DealDamageAround(user.transform.position, damagePerTick);
            yield return new WaitForSeconds(damageInterval);
            elapsed += damageInterval;
        }
        // Container는 RotatingBlades가 자체 소멸 처리하므로 추가 정리 불필요
    }

    /// <summary>Container 자식으로 칼날을 균등 각도 배치.</summary>
    private void SpawnBlades(Transform containerTransform)
    {
        if (bladePrefab == null || bladeCount <= 0) return;

        float angleStep = 360f / bladeCount;

        for (int i = 0; i < bladeCount; i++)
        {
            float angle = i * angleStep;
            Quaternion rot = Quaternion.Euler(0f, angle, 0f);
            Vector3 offset = rot * Vector3.forward * orbitRadius;

            GameObject blade = Instantiate(bladePrefab, containerTransform);
            blade.transform.localPosition = offset;
            blade.transform.localRotation = rot;  // 칼날이 궤도 방향을 향하도록
        }
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
