using UnityEngine;
using System.Collections;

[CreateAssetMenu(menuName = "Skill/Action/Spawn At Mouse")]
public class SpawnAtMouseAction : SkillAction
{
    [Header("소환 설정")]
    public GameObject prefab;
    public float duration = 3.0f;

    [Header("전투 설정")]
    public float radius = 3f;
    public HitEffectType hitEffect = HitEffectType.Slashing_Normal;
    public LayerMask enemyLayer;
    public float tickInterval = 0.5f;

    public override IEnumerator Execute(GameObject user, int level, KeyCode key, float damageOverride = 0)
    {
        PlayerController pc = user.GetComponent<PlayerController>();
        if (pc == null) yield break;

        Vector3 targetPos = pc.GetMouseWorldPosition();

        if (prefab != null)
        {
            GameObject instance = Instantiate(prefab, targetPos, Quaternion.identity);
            Destroy(instance, duration);
        }

        // ▼ [핵심] 받아온 데미지 사용
        int finalDamage = (damageOverride > 0) ? Mathf.RoundToInt(damageOverride) : 30;

        MonoBehaviour userMono = user.GetComponent<MonoBehaviour>();
        if (userMono != null)
        {
            userMono.StartCoroutine(PeriodicDamageRoutine(targetPos, finalDamage));
        }
        yield break;
    }

    private IEnumerator PeriodicDamageRoutine(Vector3 center, int damage)
    {
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            Collider[] hits = Physics.OverlapSphere(center, radius, enemyLayer);
            foreach (var hit in hits)
            {
                IDamageable damageable = hit.GetComponent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    damageable.ApplySkillDamage(damage, hitEffect);
                }
            }
            yield return new WaitForSeconds(tickInterval);
            elapsedTime += tickInterval;
        }
    }
}
