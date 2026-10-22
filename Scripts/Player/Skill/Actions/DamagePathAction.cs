using UnityEngine;
using System.Collections;

[CreateAssetMenu(menuName = "Skill/Action/Damage Path")]
public class DamagePathAction : SkillAction
{
    [Header("범위 설정")]
    public float width = 3f;
    public float length = 10f;
    public LayerMask enemyLayer;

    [Header("이펙트")]
    public GameObject hitEffectPrefab;
    public HitEffectType hitEffectType = HitEffectType.Slashing_Flash;

    public override IEnumerator Execute(GameObject user, int level, KeyCode key, float damageOverride = 0)
    {
        Vector3 startPos = user.transform.position - user.transform.forward * length;
        Vector3 endPos = user.transform.position;
        Vector3 center = (startPos + endPos) / 2f;

        Collider[] hits = Physics.OverlapBox(center, new Vector3(width / 2, 1f, length / 2), user.transform.rotation, enemyLayer);

        // ▼ [핵심] 받아온 데미지 사용
        int finalDamage = (damageOverride > 0) ? Mathf.RoundToInt(damageOverride) : 50;

        foreach (var hit in hits)
        {
            IDamageable damageable = hit.GetComponent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                damageable.ApplySkillDamage(finalDamage, hitEffectType);
                if (hitEffectPrefab != null)
                    Instantiate(hitEffectPrefab, hit.transform.position, Quaternion.identity);
            }
        }
        yield break;
    }
}
