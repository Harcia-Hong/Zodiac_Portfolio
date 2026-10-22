using UnityEngine;
using System.Collections;

[CreateAssetMenu(menuName = "Skill/Action/Play Anim From Time")]
public class PlayAnimFromTimeAction : SkillAction
{
    [Header("애니메이션 설정")]
    [Tooltip("재생할 애니메이션 상태 이름 (Trigger 아님!)")]
    public string stateName = "JumpAttack";

    [Tooltip("시작 지점 (0.0 ~ 1.0). 예: 0.4면 40% 지점부터 재생")]
    [Range(0f, 1f)]
    public float startTime = 0.4f;

    public override IEnumerator Execute(GameObject user, int level, KeyCode key, float damageOverride = 0)
    {
        Animator anim = user.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            // 0번 레이어에서 해당 상태를 startTime부터 즉시 재생
            anim.Play(stateName, 0, startTime);
        }
        yield break;
    }
}
