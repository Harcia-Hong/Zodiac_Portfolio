using UnityEngine;
using UnityEngine.VFX;

public class Weapon : MonoBehaviour
{
    public enum WeaponType { Melee, Range };

    [Header("Weapon Setting")]
    public WeaponType weaponType = WeaponType.Melee;
    public int damage = 10; // 무기 기본 공격력
    public float attackRate;

    [Header("HitBox")]
    public BoxCollider hitBox;

    private void Awake()
    {
        if (hitBox != null) hitBox.enabled = false;
    }

    public void EnableHitBox()
    {
        if (hitBox != null) hitBox.enabled = true;
    }

    public void DisableHitBox()
    {
        if (hitBox != null) hitBox.enabled = false;
    }

    // 공격력 계산 (기본뎀 + 플레이어 스탯)
    public int GetDamage(bool includeCrit = false)
    {
        int totalDamage = damage;

        // PlayerStats에서 공격력 가져오기
        if (PlayerStats.Instance != null)
        {
            totalDamage += PlayerStats.Instance.AttackPower;

            // 치명타 계산
            if (includeCrit && PlayerStats.Instance.RollCritical())
            {
                totalDamage = Mathf.RoundToInt(totalDamage * 1.5f);
                Debug.Log("[Weapon] 치명타 발생!");
            }
        }

        return totalDamage;
    }
}
