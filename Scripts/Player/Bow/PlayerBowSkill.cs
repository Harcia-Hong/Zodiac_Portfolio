using UnityEngine;

/// <summary>
/// 임시 활 스킬 스텁
/// </summary>
public class PlayerBowSkill : MonoBehaviour
{
    public void UseQSkill() { Debug.Log("활 Q스킬 (미구현)"); }
    public void UseESkill() { Debug.Log("활 E스킬 (미구현)"); }
    public void ActivateSkills() { enabled = true; Debug.Log("활 스킬 활성화"); }
    public void DeactivateSkills() { enabled = false; Debug.Log("활 스킬 비활성화"); }
    public object GetCooldownStatus() { return null; }
}
