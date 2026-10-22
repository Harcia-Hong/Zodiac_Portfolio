using UnityEngine;
using System.Collections;

[CreateAssetMenu(menuName = "Skill/Action/Charge Projectile")]
public class ChargeProjectileAction : SkillAction
{
    [Header("차징 설정")]
    public float maxChargeTime = 1.5f;
    public bool allowMovement = true;
    public int chargeStages = 3;

    [Header("발사체 설정")]
    public GameObject projectilePrefab;
    public float speed = 40f; // 마탄은 좀 빨라야 함
    public float range = 30f;
    public HitEffectType hitEffect = HitEffectType.Bullet;

    public override IEnumerator Execute(GameObject user, int level, KeyCode key, float damageOverride = 0)
    {
        PlayerController pc = user.GetComponent<PlayerController>();
        SkillManager sm = user.GetComponent<SkillManager>();

        // UI 켜기
        CastBarUI castBar = FindFirstObjectByType<CastBarUI>(FindObjectsInactive.Include);
        if (castBar != null) castBar.Show("차징", maxChargeTime, true, chargeStages);

        if (pc != null) pc.isSkillCasting = !allowMovement;

        float currentCharge = 0f;
        bool isCancelled = false;

        pc.isCharging = true;
        while (Input.GetKey(key) && currentCharge < maxChargeTime)
        {
            currentCharge += Time.deltaTime;
            if (pc != null && pc.isDodging)
            {
                isCancelled = true;
                break;
            }
            yield return null;
        }

        if (castBar != null) castBar.Hide();
        if (pc != null) pc.isSkillCasting = false;
        pc.isCharging = false;

        if (!isCancelled)
        {
            FireProjectile(user, currentCharge, level, damageOverride);
        }
    }

    private void FireProjectile(GameObject user, float chargeTime, int level, float damageInfo)
    {
        PlayerController pc = user.GetComponent<PlayerController>();
        Vector3 spawnPos = user.transform.position + Vector3.up * 1.0f;
        Vector3 mouseWorldPos = pc.GetMouseWorldPosition();
        Vector3 targetPosFlat = new Vector3(mouseWorldPos.x, spawnPos.y, mouseWorldPos.z);
        Vector3 fireDirection = (targetPosFlat - spawnPos).normalized;

        user.transform.LookAt(new Vector3(mouseWorldPos.x, user.transform.position.y, mouseWorldPos.z));

        // ▼ [핵심] 데미지 계산 로직 변경
        // SkillData에서 준 값(damageInfo)을 '최대 데미지'로 사용
        float maxDmg = (damageInfo > 0) ? damageInfo : 100f;
        float minDmg = maxDmg * 0.3f; // 최소 데미지는 최대의 30%

        float chargeRatio = Mathf.Clamp01(chargeTime / maxChargeTime);
        int finalDamage = Mathf.RoundToInt(Mathf.Lerp(minDmg, maxDmg, chargeRatio));

        if (projectilePrefab != null)
        {
            Quaternion lookRot = Quaternion.LookRotation(fireDirection);
            GameObject proj = Instantiate(projectilePrefab, spawnPos, lookRot);
            LinearProjectile bulletScript = proj.GetComponent<LinearProjectile>();

            if (bulletScript != null)
            {
                bulletScript.Initialize(speed, finalDamage, range, hitEffect);
            }
        }
    }
}
