using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어가 소지 중인 전리품을 관리하는 영구 싱글톤
/// 
/// 역할:
///   - 던전에서 획득한 전리품 보관
///   - 베이스캠프 상점에서 판매 시 제거
///   - 씬 전환에도 유지 (DontDestroyOnLoad)
///   - 이벤트로 변경 알림 (UI 갱신용)
/// 
/// 사용 예:
///   TrophyInventory.Instance.AddItem(trophyData);
///   var items = TrophyInventory.Instance.GetAllItems();
///   TrophyInventory.Instance.RemoveItem(trophyData);
/// </summary>
public class TrophyInventory : MonoBehaviour
{
    public static TrophyInventory Instance { get; private set; }

    [Header("인벤토리 설정")]
    [SerializeField, Tooltip("최대 소지 가능 전리품 수 (0 = 무제한)")]
    private int maxCapacity = 20;

    // === 런타임 전리품 목록 ===
    private List<TrophyItemData> items = new List<TrophyItemData>();

    // === 이벤트 (Observer 패턴) ===
    /// <summary>전리품 획득 시 발행 (추가된 아이템 전달)</summary>
    public event Action<TrophyItemData> OnItemAdded;

    /// <summary>전리품 제거 시 발행 (제거된 아이템 전달)</summary>
    public event Action<TrophyItemData> OnItemRemoved;

    /// <summary>인벤토리 전체 비워질 때 발행</summary>
    public event Action OnInventoryCleared;

    // === 프로퍼티 ===
    /// <summary>현재 소지 중인 전리품 수</summary>
    public int Count => items.Count;

    /// <summary>인벤토리가 가득 찼는지 (maxCapacity가 0이면 항상 false)</summary>
    public bool IsFull => maxCapacity > 0 && items.Count >= maxCapacity;

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #endregion

    #region 전리품 추가

    /// <summary>
    /// 전리품 1개 추가
    /// - 인벤토리가 가득 차면 실패
    /// </summary>
    /// <returns>추가 성공 여부</returns>
    public bool AddItem(TrophyItemData item)
    {
        if (item == null)
        {
            Debug.LogWarning("[TrophyInventory] null 아이템 추가 시도");
            return false;
        }

        if (IsFull)
        {
            Debug.Log($"[TrophyInventory] 인벤토리 가득 참 ({items.Count}/{maxCapacity})");
            return false;
        }

        items.Add(item);
        OnItemAdded?.Invoke(item);

        Debug.Log($"[TrophyInventory] '{item.itemName}' 획득 ({items.Count}/{(maxCapacity > 0 ? maxCapacity.ToString() : "∞")})");
        return true;
    }

    #endregion

    #region 전리품 제거

    /// <summary>
    /// 전리품 1개 제거 (판매 시 사용)
    /// - 동일 아이템이 여러 개면 첫 번째 것만 제거
    /// </summary>
    /// <returns>제거 성공 여부</returns>
    public bool RemoveItem(TrophyItemData item)
    {
        if (item == null) return false;

        if (items.Remove(item))
        {
            OnItemRemoved?.Invoke(item);
            Debug.Log($"[TrophyInventory] '{item.itemName}' 제거됨 (남은 수: {items.Count})");
            return true;
        }

        Debug.LogWarning($"[TrophyInventory] '{item.itemName}'이 인벤토리에 없습니다");
        return false;
    }

    /// <summary>
    /// 특정 인덱스의 전리품 제거
    /// - 상점 UI에서 슬롯 인덱스로 제거할 때 사용
    /// </summary>
    public TrophyItemData RemoveAt(int index)
    {
        if (index < 0 || index >= items.Count)
        {
            Debug.LogWarning($"[TrophyInventory] 잘못된 인덱스: {index}");
            return null;
        }

        TrophyItemData removed = items[index];
        items.RemoveAt(index);
        OnItemRemoved?.Invoke(removed);

        Debug.Log($"[TrophyInventory] [{index}] '{removed.itemName}' 제거됨");
        return removed;
    }

    #endregion

    #region 조회

    /// <summary>
    /// 소지 중인 전리품 전체 목록 반환 (읽기 전용 복사본)
    /// </summary>
    public List<TrophyItemData> GetAllItems()
    {
        return new List<TrophyItemData>(items);
    }

    /// <summary>
    /// 특정 전리품을 몇 개 소지 중인지 반환
    /// </summary>
    public int GetItemCount(TrophyItemData item)
    {
        if (item == null) return 0;

        int count = 0;
        string targetName = item.name;

        foreach (var held in items)
        {
            if (held.name == targetName)
                count++;
        }

        return count;
    }

    /// <summary>
    /// 특정 등급의 전리품만 필터링해서 반환
    /// - 상점 UI에서 등급별 표시 시 사용
    /// </summary>
    public List<TrophyItemData> GetItemsByGrade(TrophyGrade grade)
    {
        List<TrophyItemData> result = new List<TrophyItemData>();

        foreach (var item in items)
        {
            if (item.grade == grade)
                result.Add(item);
        }

        return result;
    }

    #endregion

    #region 초기화

    /// <summary>
    /// 인벤토리 전체 비우기
    /// - 현재는 사망 시 전리품 유지이므로 호출하지 않음
    /// - 나중에 패널티 정책 변경 시 사용
    /// </summary>
    public void Clear()
    {
        items.Clear();
        OnInventoryCleared?.Invoke();
        Debug.Log("[TrophyInventory] 인벤토리 전체 비움");
    }

    #endregion
}