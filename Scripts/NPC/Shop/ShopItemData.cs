using UnityEngine;

/// <summary>
/// 상점 슬롯 1개의 런타임 데이터
/// - 스킬 또는 소켓 아이템 정보를 담음
/// - ShopManager에서 생성, ShopUI에서 표시
/// </summary>
public class ShopItemData
{
    /// <summary>상품 종류</summary>
    public ShopItemType itemType;

    /// <summary>스킬 데이터 (스킬일 때만)</summary>
    public SkillData skillData;

    /// <summary>소켓 데이터 (소켓일 때만)</summary>
    public SocketItemData socketData;

    /// <summary>강화 레벨 (0 = 기본)</summary>
    public int enhanceLevel;

    /// <summary>판매 가격 (골드)</summary>
    public int price;

    /// <summary>품절 여부</summary>
    public bool isSoldOut;

    /// <summary>표시용 이름</summary>
    public string displayName;

    /// <summary>표시용 아이콘</summary>
    public Sprite icon;

    /// <summary>표시용 설명</summary>
    public string description;

    /// <summary>소켓 품질 (소켓일 때만, 0~100)</summary>
    public int quality;
}

/// <summary>
/// 상점 상품 종류
/// </summary>
public enum ShopItemType
{
    Skill,
    Socket
}
