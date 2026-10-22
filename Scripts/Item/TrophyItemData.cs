using UnityEngine;

public enum TrophyGrade
{
    Common,     // 일반
    Rare,       // 희귀
    Epic,       // 영웅
    Legendary   // 전설
}

[CreateAssetMenu(fileName = "Trophy_", menuName = "Loot System/Trophy Item Data")]
public class TrophyItemData : ScriptableObject
{
    [Header("기본 정보")]
    public string itemName;
    public Sprite icon;
    [TextArea(2, 4)]
    public string description;

    [Header("등급 및 가치")]
    public TrophyGrade grade = TrophyGrade.Common;
    public int fairPrice = 100;  // 적정가

    [Header("드랍 설정")]
    [Range(0f, 100f)]
    public float dropWeight = 50f;
    public GameObject dropPrefab;  // 바닥에 떨어질 때 보이는 3D 오브젝트
}