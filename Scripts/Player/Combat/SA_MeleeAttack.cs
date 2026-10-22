using System.Collections;
using UnityEngine;

/// <summary>
/// 기본 근접 공격 액션
/// 애니메이션 속도를 공격 속도에 맞춰 조절하고, 무기 히트박스를 제어합니다.
/// </summary>
[CreateAssetMenu(fileName = "SA_MeleeAttack", menuName = "Skill System/Actions/Melee Attack")]
public class SA_MeleeAttack : SkillAction
{
    [Header("설정")]
    public string animationTrigger = "Attack1";
    public float startDelay = 0.1f;
    public float endDelay = 0.1f;
    public float hitBoxStartTime = 0.1f; // 애니 시작 후 언제 켤지
    public float hitBoxDuration = 0.2f;  // 얼마나 켜둘지



    public override IEnumerator Execute(GameObject user, int level, KeyCode key, float damage)
    {
        Animator anim = user.GetComponentInChildren<Animator>();
        Weapon weapon = user.GetComponentInChildren<Weapon>();
        PlayerController pc = user.GetComponent<PlayerController>();

        // 1. 공격 속도 가져오기
        float attackSpeed = 1.0f;
        if (PlayerStats.Instance != null) attackSpeed = PlayerStats.Instance.AttackSpeed;

        // 2. 애니메이션 속도 조절 (빨라짐!)
        if (anim != null)
        {
            anim.speed = attackSpeed;
            anim.SetTrigger(animationTrigger);
        }

        // 3. 적 방향 바라보기
        if (pc != null) pc.RotateToMouseDirection();

        // 4. 히트박스 타이밍 (공속에 맞춰 빨라짐)
        yield return new WaitForSeconds(hitBoxStartTime / attackSpeed);

        // 히트박스 켜기 (데미지 전달)
        if (weapon != null)
        {
            // 무기에게 "이번 공격은 이만큼의 데미지야" 라고 알려줄 수도 있지만,
            // 보통 Weapon.cs가 직접 충돌 시 PlayerStats를 참조하거나
            // 여기서 히트박스 로직을 직접 제어할 수도 있습니다.
            // 일단 기존 Weapon 방식대로 켭니다.
            weapon.EnableHitBox();

            // [중요] 스킬 데미지를 무기에 잠시 저장하거나, Weapon이 충돌 시 이 damage 값을 쓰도록 수정 필요.
            // (간단하게 Weapon의 damage 변수를 덮어씌우는 방식 사용)
            weapon.damage = (int)damage;
        }

        yield return new WaitForSeconds(hitBoxDuration / attackSpeed);

        // 히트박스 끄기
        if (weapon != null) weapon.DisableHitBox();

        yield return new WaitForSeconds(endDelay / attackSpeed);

        // 5. 애니메이션 속도 원상복구
        if (anim != null) anim.speed = 1.0f;
    }
}
