using UnityEngine;

public class PlayerGunSkill : MonoBehaviour
{
    public void UseQSkill() { Debug.Log("총 Q스킬 (미구현)"); }
    public void UseESkill() { Debug.Log("총 E스킬 (미구현)"); }
    public void ActivateSkills() { enabled = true; Debug.Log("총 스킬 활성화"); }
    public void DeactivateSkills() { enabled = false; Debug.Log("총 스킬 비활성화"); }
    public object GetCooldownStatus() { return null; }
}
