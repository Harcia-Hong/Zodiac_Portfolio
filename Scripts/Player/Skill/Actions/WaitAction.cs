using System.Collections;
using UnityEngine;

/// <summary>
/// 단순히 N초 대기하는 액션.
/// 
/// 사용 목적:
///   스킬 액션 시퀀스의 실행 시간과 애니메이션 재생 시간을 일치시키기 위함.
///   blockMovement=true인 스킬에서 시퀀스 마지막에 두면, 베기 같은 시각적 모션이
///   완전히 끝날 때까지 캐릭터의 이동 잠금이 유지됨.
/// 
/// 예시 (분노 연참 1단):
///   ComboAnim → Dash(0.15s) → Damage(즉시) → Wait(0.4s)
///   → 총 0.55초 동안 캐릭터 락 = 베기 애니메이션 길이와 매칭
/// 
/// 다른 콤보 스킬이나 캐스팅 스킬에도 그대로 재사용 가능.
/// </summary>
[CreateAssetMenu(menuName = "Skill/Action/Wait")]
public class WaitAction : SkillAction
{
    [Header("대기 설정")]
    [Tooltip("이 액션이 실행될 때 대기할 시간 (초).\n" +
             "이 시간 동안 다음 액션 진행과 스킬 시퀀스 종료가 멈춤.")]
    public float duration = 0.4f;

    /// <summary>
    /// 지정한 시간만큼 코루틴 yield로 대기.
    /// duration이 0 이하면 즉시 종료.
    /// </summary>
    public override IEnumerator Execute(GameObject user, int level, KeyCode key, float damageOverride = 0)
    {
        if (duration > 0f)
        {
            yield return new WaitForSeconds(duration);
        }
    }
}
