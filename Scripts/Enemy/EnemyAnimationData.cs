using System;
using UnityEngine;

/**
 * 애니메이션 파라미터 데이터 관리 클래스
 * string을 hash로 변환하여 성능 최적화
 * FSM에서 애니메이션 제어에 사용
 */
[Serializable]
public class EnemyAnimationData
{
    [Header("지상 상태 애니메이션")]
    [SerializeField] private string groundParameterName = "@Ground";
    [SerializeField] private string idleParameterName = "Idle";
    [SerializeField] private string walkParameterName = "Walk";
    [SerializeField] private string runParameterName = "Run";

    [Header("공중 상태 애니메이션")]
    [SerializeField] private string airParameterName = "@Air";
    [SerializeField] private string jumpParameterName = "Jump";
    [SerializeField] private string fallParameterName = "Fall";

    [Header("공격 상태 애니메이션")]
    [SerializeField] private string attackParameterName = "@Attack";
    [SerializeField] private string comboAttackParameterName = "ComboAttack";
    [SerializeField] private string baseAttackParameterName = "BaseAttack";

    // Hash 값들 (성능 최적화용)
    public int GroundParameterHash { get; private set; }
    public int IdleParameterHash { get; private set; }
    public int WalkParameterHash { get; private set; }
    public int RunParameterHash { get; private set; }

    public int AirParameterHash { get; private set; }
    public int JumpParameterHash { get; private set; }
    public int FallParameterHash { get; private set; }

    public int AttackParameterHash { get; private set; }
    public int ComboAttackParameterHash { get; private set; }
    public int BaseAttackParameterHash { get; private set; }

    /// <summary>
    /// Hash 값 초기화
    /// Awake에서 호출하여 string을 hash로 변환
    /// </summary>
    public void Initialize()
    {
        // 지상 상태
        GroundParameterHash = Animator.StringToHash(groundParameterName);
        IdleParameterHash = Animator.StringToHash(idleParameterName);
        WalkParameterHash = Animator.StringToHash(walkParameterName);
        RunParameterHash = Animator.StringToHash(runParameterName);

        // 공중 상태
        AirParameterHash = Animator.StringToHash(airParameterName);
        JumpParameterHash = Animator.StringToHash(jumpParameterName);
        FallParameterHash = Animator.StringToHash(fallParameterName);

        // 공격 상태
        AttackParameterHash = Animator.StringToHash(attackParameterName);
        ComboAttackParameterHash = Animator.StringToHash(comboAttackParameterName);
        BaseAttackParameterHash = Animator.StringToHash(baseAttackParameterName);
    }
}
