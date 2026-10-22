using UnityEngine;
using System.Collections; // 코루틴 사용을 위해 추가

/// <summary>
/// '폭탄 투척' 스킬의 실제 로직.
/// ISkillLogic 인터페이스를 구현합니다.
/// </summary>
public class BombThrowLogic : MonoBehaviour, ISkillLogic
{
    [Header("폭탄 설정")]
    [Tooltip("실제로 날아갈 폭탄 프리팹 (BombProjectile.cs 스크립트가 있어야 함)")]
    public GameObject bombProjectilePrefab;

    [Tooltip("폭탄이 생성될 위치 (Player의 자식 오브젝트)")]
    public Transform spawnPoint; // (PlayerSwordSkill의 vfxSpawnPoint처럼)

    [Header("스킬 성능")]
    public int damage = 50;
    public float radius = 3f;
    public float throwSpeed = 15f;

    // 이 Logic을 소유한 SkillData (Awake에서 자동으로 세팅)
    // (이 방법은 나중에 설명, 지금은 public 변수로 진행)

    /// <summary>
    /// 스킬 발동 시 호출되는 메인 함수 (ISkillLogic 구현)
    /// </summary>
    public void Activate(GameObject owner)
    {
        Debug.Log("스킬 발동: 폭탄 투척!");

        // 1. 플레이어 컨트롤러에서 필요한 정보 가져오기
        PlayerController playerController = owner.GetComponent<PlayerController>();
        if (playerController == null)
        {
            Debug.LogError("[BombThrowLogic] 스킬 시전자가 PlayerController를 가지고 있지 않습니다.");
            return;
        }

        // 2. 스킬을 발동할 발사 위치 찾기
        // (spawnPoint가 설정 안됐을 경우, owner(Player)의 위치를 기본값으로 사용)
        Vector3 startPosition = (spawnPoint != null) ? spawnPoint.position : owner.transform.position;

        // 3. 목표 지점(마우스 위치) 찾기
        // GetMouseWorldPosition은 락온도 지원함
        Vector3 targetPosition = playerController.GetMouseWorldPosition();

        // 4. 폭탄 프리팹 생성
        if (bombProjectilePrefab == null)
        {
            Debug.LogError("[BombThrowLogic] bombProjectilePrefab이 설정되지 않았습니다!");
            return;
        }

        GameObject bombInstance = Instantiate(bombProjectilePrefab, startPosition, Quaternion.identity);

        // 5. 생성된 폭탄에게 정보 전달
        BombProjectile projectile = bombInstance.GetComponent<BombProjectile>();
        if (projectile != null)
        {
            // 폭탄에 목표 지점, 데미지, 속도, 범위, 피격 효과 타입을 알려줌
            projectile.Initialize(
                targetPosition,
                throwSpeed,
                damage,
                radius,
                HitEffectType.Explosion_Bomb // (이 타입을 HitEffectType.cs에 추가해야 함)
            );
        }
        else
        {
            Debug.LogError("[BombThrowLogic] 폭탄 프리팹에 BombProjectile.cs 스크립트가 없습니다!");
        }

        // 6. 플레이어가 마우스 방향을 바라보게 회전
        Vector3 directionToTarget = (targetPosition - owner.transform.position).normalized;
        directionToTarget.y = 0;
        if (directionToTarget.sqrMagnitude > 0.001f)
        {
            owner.transform.rotation = Quaternion.LookRotation(directionToTarget);
        }
    }
}
