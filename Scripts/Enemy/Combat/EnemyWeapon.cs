using UnityEngine;

public class EnemyWeapon : MonoBehaviour
{
    public enum WeaponType { Melee, Range };

    [Header("Weapon Setting")]
    public WeaponType weaponType = WeaponType.Melee;
    public int damage;
    public float attackRate;

    [Header("HitBox")]
    public BoxCollider hitBox; // Collider는 상위 클래스, 하위 클래스에 Box, Capsule 등등 있음

    [Header("Trail Effect")]
    public TrailRenderer trail;

    private void Awake()
    {
        if(hitBox != null)
        {
            hitBox.enabled = false;
        }
        if(trail != null)
        {
            trail.enabled = false;
        }
    }

    public void EnableHitBox()
    {
        if(hitBox != null)
        {
            hitBox.enabled = true;
        }
        if(trail != null)
        {
            trail.enabled = true;
        }
    }

    public void DisableHitBox()
    {
        if(hitBox != null )
        {
            hitBox.enabled = false;
        }
        if(trail != null)
        {
            trail.enabled = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerHealth player = other.GetComponent<PlayerHealth>();
            if(player != null)
            {
                player.TakeDamage(damage);
            }
        }
    }
}
