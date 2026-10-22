using UnityEngine;

/// <summary>
/// 대화 데이터 ScriptableObject
/// - NPC의 대사 목록을 담는 데이터 컨테이너
/// - 최초 대화, 일반 대화 등 용도별로 여러 개 생성 가능
/// </summary>
[CreateAssetMenu(fileName = "NewDialogue", menuName = "Zodiac/DialogueData")]
public class DialogueDataSO : ScriptableObject
{
    [Header("정보")]
    [Tooltip("NPC 이름 (예: 멀린)")]
    public string speakerName;

    [Tooltip("NPC 초상화 (대화창에 표시)")]
    public Sprite speakerPortrait;

    [Header("대사 목록")]
    [TextArea(2, 5)]
    [Tooltip("순서대로 표시될 대사들")]
    public string[] lines;
}
