using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "SocketItem_", menuName = "Skill System/Socket Item Data")]
public class SocketItemData : ScriptableObject
{
    [Header("기본 정보")]
    public string itemName;         // 아이템 이름 (예: 하급 어스름의 정수)
    public Sprite icon;             // 아이콘
    [TextArea]
    public string descriptionTemplate; // 툴팁 템플릿 ("어둠 피해 {0} 추가...")

    [Header("아이템 효과 (Actions)")]
    // 이 정수를 끼우면 발동될 추가 액션들
    public List<SkillAction> actions = new List<SkillAction>();
}
