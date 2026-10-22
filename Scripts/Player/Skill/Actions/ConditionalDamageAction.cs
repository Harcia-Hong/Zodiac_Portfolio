using UnityEngine;
using System.Collections;

[CreateAssetMenu(menuName = "Skill/Action/Conditional Damage")]
public class ConditionalDamageAction : SkillAction
{
    public float range = 2.0f;
    public LayerMask enemyLayer;
    public HitEffectType hitEffect = HitEffectType.Slashing_Normal;

    public override IEnumerator Execute(GameObject user, int level, KeyCode key, float damageOverride = 0)
    {
        Collider[] hits = Physics.OverlapSphere(user.transform.position, range, enemyLayer);
        Enemy target = null;
        float minDist = float.MaxValue;

        foreach (var hit in hits)
        {
            float d = Vector3.Distance(user.transform.position, hit.transform.position);
            if (d < minDist)
            {
                minDist = d;
                target = hit.GetComponent<Enemy>();
            }
        }

        if (target != null)
        {
            // ▼ [핵심] 받아온 데미지 사용
            int finalDamage = (damageOverride > 0) ? Mathf.RoundToInt(damageOverride) : 100;

            // 조건부: 체력 꽉 찼으면 2배
            if (target.health.curHealth >= target.health.maxHealth)
            {
                finalDamage *= 2;
            }

            target.ApplySkillDamage(finalDamage, hitEffect);
        }
        yield break;
    }
}
