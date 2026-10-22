using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 임시 디버깅용 — K키 누르면 SkillCardUI를 직접 호출.
/// 캡슐 시스템 빼고 SkillCardUI 자체 동작 확인용.
/// 동작 확인 후 삭제.
/// </summary>
public class TestCardUITrigger : MonoBehaviour
{
    [SerializeField] private List<SkillData> testSkillPool;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            Debug.Log("[TestCardUITrigger] K키 눌림 — SkillCardUI 호출 시도");

            if (SkillCardUI.Instance == null)
            {
                Debug.LogError("[TestCardUITrigger] SkillCardUI.Instance가 null!");
                return;
            }

            SkillCardUI.Instance.Open(testSkillPool, (picked) =>
            {
                Debug.Log($"[TestCardUITrigger] 콜백 호출됨 — '{picked.skillName}' 픽");
            });
        }
    }
}
