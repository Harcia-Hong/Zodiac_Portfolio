using UnityEngine;
using System.Collections;

[CreateAssetMenu(menuName = "Skill/Action/Spawn Trap")]
public class SpawnTrapAction : SkillAction
{
    [Header("트랩 설정")]
    public GameObject trapPrefab;
    public int count = 3;
    public float scatterRadius = 2.0f;

    [Header("폭발 설정")]
    public float explosionRadius = 3f;
    public HitEffectType hitEffect = HitEffectType.Explosion_Bomb;
    public float explosionDelay = 0.5f;

    public override IEnumerator Execute(GameObject user, int level, KeyCode key, float damageOverride = 0)
    {
        Vector3 centerPos = user.transform.position;

        // ▼ [핵심] 받아온 데미지 사용
        int finalDamage = (damageOverride > 0) ? Mathf.RoundToInt(damageOverride) : 100;

        for (int i = 0; i < count; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * scatterRadius;
            Vector3 spawnPos = centerPos + new Vector3(randomCircle.x, 0, randomCircle.y);

            GameObject trap = Instantiate(trapPrefab, spawnPos, Quaternion.identity);

            BombProjectile bombScript = trap.GetComponent<BombProjectile>();
            if (bombScript != null)
            {
                bombScript.Initialize(spawnPos, 1f, finalDamage, explosionRadius, hitEffect, explosionDelay);
            }
        }
        yield break;
    }
}
