using UnityEngine;
using System.Collections;

// 스킬의 행동을 정의하는 추상 클래스 ㅇㅇ
[CreateAssetMenu(fileName = "SkillAction", menuName = "Scriptable Objects/SkillAction")]
public abstract class SkillAction : ScriptableObject
{
    // 액션 실행 함수
    public virtual bool CanExecute(GameObject user)
    {
        return true;
    }
    public abstract IEnumerator Execute(GameObject user, int level, KeyCode key, float damageOverride = 0);
}
