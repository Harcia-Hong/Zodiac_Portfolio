using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Action_Projectile", menuName = "Skill System/Actions/Projectile Action")]
public class ProjectileAction : SkillAction
{
    [Header("투사체 설정")]
    public GameObject projectilePrefab;

    [Header("발사 설정")]
    public float speed = 15f;
    public float radius = 3f;
    public HitEffectType hitEffectType = HitEffectType.Explosion_Bomb;

    // [수정] damageOverride 받음
    public override IEnumerator Execute(GameObject user, int level, KeyCode key, float damageOverride = 0)
    {
        if (projectilePrefab == null) yield break;

        PlayerController pc = user.GetComponent<PlayerController>();
        if (pc == null) yield break;

        Vector3 targetPos = pc.GetMouseWorldPosition();
        Vector3 spawnPos = user.transform.position + Vector3.up * 1.0f;

        GameObject projectileObj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);

        BombProjectile bombScript = projectileObj.GetComponent<BombProjectile>();
        if (bombScript != null)
        {
            // ▼ [핵심] 받아온 데미지(damageOverride)를 그대로 사용 (정수로 변환)
            // 만약 값이 0이면(테스트 등) 안전장치로 10 사용
            int finalDamage = (damageOverride > 0) ? Mathf.RoundToInt(damageOverride) : 10;

            bombScript.Initialize(
                targetPos,
                speed,
                finalDamage,
                radius,
                hitEffectType
            );
        }

        // 플레이어 회전
        Vector3 dir = (targetPos - user.transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
            user.transform.rotation = Quaternion.LookRotation(dir);

        yield break;
    }
}
