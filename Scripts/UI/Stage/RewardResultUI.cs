using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 보상 결과 UI (우측 상단)
/// - 획득한 보상/디버프 표시
/// - 일정 시간 후 자동으로 사라짐
/// </summary>
public class RewardResultUI : MonoBehaviour
{
    public static RewardResultUI Instance { get; private set; }

    #region Serialized Fields
    [Header("UI 참조")]
    [SerializeField] private Transform resultContainer;
    [SerializeField] private GameObject resultEntryPrefab;

    [Header("표시 설정")]
    [SerializeField] private float entryDisplayDuration = 3f;
    [SerializeField] private float entryFadeInDuration = 0.3f;
    [SerializeField] private float entryFadeOutDuration = 0.5f;
    [SerializeField] private int maxVisibleEntries = 5;

    [Header("색상 설정")]
    [SerializeField] private Color buffColor = new Color(0.5f, 1f, 0.5f, 1f);
    [SerializeField] private Color debuffColor = new Color(1f, 0.5f, 0.5f, 1f);
    [SerializeField] private Color currencyColor = new Color(1f, 0.9f, 0.5f, 1f);
    [SerializeField] private Color healColor = new Color(0.5f, 1f, 0.8f, 1f);
    #endregion

    #region Private Fields
    private Queue<GameObject> activeEntries = new Queue<GameObject>();
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 보상 결과 표시
    /// </summary>
    public void ShowReward(RewardEntrySO reward)
    {
        if (reward == null) return;

        Color color = GetColorByRewardType(reward.rewardType);
        string icon = GetIconByRewardType(reward.rewardType);
        string text = $"{icon} {reward.rewardName}: {reward.GetSummaryText()}";

        CreateEntry(text, color, reward.icon);
    }

    /// <summary>
    /// 디버프 결과 표시
    /// </summary>
    public void ShowDebuff(DebuffEntrySO debuff)
    {
        if (debuff == null) return;

        string text = $"▼ {debuff.debuffName}: {debuff.GetSummaryText()}";

        CreateEntry(text, debuffColor, debuff.icon);
    }

    /// <summary>
    /// 커스텀 메시지 표시
    /// </summary>
    public void ShowMessage(string message, Color color, Sprite icon = null)
    {
        CreateEntry(message, color, icon);
    }

    /// <summary>
    /// 보상 + 디버프 한번에 표시
    /// </summary>
    public void ShowResults(RewardEntrySO reward, DebuffEntrySO[] debuffs)
    {
        // 보상 먼저
        if (reward != null)
        {
            ShowReward(reward);
        }

        // 디버프들
        if (debuffs != null)
        {
            foreach (var debuff in debuffs)
            {
                if (debuff != null)
                {
                    ShowDebuff(debuff);
                }
            }
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// 결과 엔트리 생성
    /// </summary>
    private void CreateEntry(string text, Color color, Sprite icon)
    {
        if (resultContainer == null || resultEntryPrefab == null)
        {
            Debug.LogWarning("[RewardResultUI] resultContainer 또는 resultEntryPrefab이 없습니다.");
            return;
        }

        // 최대 개수 초과 시 오래된 것 제거
        while (activeEntries.Count >= maxVisibleEntries)
        {
            GameObject oldEntry = activeEntries.Dequeue();
            if (oldEntry != null)
            {
                Destroy(oldEntry);
            }
        }

        // 새 엔트리 생성
        GameObject entry = Instantiate(resultEntryPrefab, resultContainer);
        activeEntries.Enqueue(entry);

        // 텍스트 설정
        TextMeshProUGUI entryText = entry.GetComponentInChildren<TextMeshProUGUI>();
        if (entryText != null)
        {
            entryText.text = text;
            entryText.color = color;
        }

        // 아이콘 설정
        Image entryIcon = entry.transform.Find("Icon")?.GetComponent<Image>();
        if (entryIcon != null)
        {
            if (icon != null)
            {
                entryIcon.sprite = icon;
                entryIcon.enabled = true;
            }
            else
            {
                entryIcon.enabled = false;
            }
        }

        // 애니메이션
        StartCoroutine(EntryLifecycleRoutine(entry));
    }

    /// <summary>
    /// 엔트리 생명주기 (등장 → 유지 → 사라짐)
    /// </summary>
    private IEnumerator EntryLifecycleRoutine(GameObject entry)
    {
        if (entry == null) yield break;

        CanvasGroup cg = entry.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = entry.AddComponent<CanvasGroup>();
        }

        // 페이드 인
        cg.alpha = 0f;
        cg.DOFade(1f, entryFadeInDuration);

        // 슬라이드 인 (오른쪽에서)
        RectTransform rect = entry.GetComponent<RectTransform>();
        if (rect != null)
        {
            Vector2 originalPos = rect.anchoredPosition;
            rect.anchoredPosition = originalPos + new Vector2(100f, 0f);
            rect.DOAnchorPos(originalPos, entryFadeInDuration).SetEase(Ease.OutQuad);
        }

        // 표시 시간 대기
        yield return new WaitForSeconds(entryDisplayDuration);

        // 페이드 아웃
        cg.DOFade(0f, entryFadeOutDuration);
        yield return new WaitForSeconds(entryFadeOutDuration);

        // 제거
        if (activeEntries.Contains(entry))
        {
            // Queue에서 제거는 복잡하므로 그냥 놔둠 (maxVisibleEntries로 관리)
        }

        Destroy(entry);
    }

    /// <summary>
    /// 보상 타입에 맞는 색상 반환
    /// </summary>
    private Color GetColorByRewardType(RewardType type)
    {
        switch (type)
        {
            case RewardType.StatBuff: return buffColor;
            case RewardType.Currency: return currencyColor;
            case RewardType.SkillDrop: return buffColor;
            case RewardType.Heal: return healColor;
            default: return Color.white;
        }
    }

    /// <summary>
    /// 보상 타입에 맞는 아이콘 텍스트 반환
    /// </summary>
    private string GetIconByRewardType(RewardType type)
    {
        switch (type)
        {
            case RewardType.StatBuff: return "▲";
            case RewardType.Currency: return "★";
            case RewardType.SkillDrop: return "◆";
            case RewardType.Heal: return "♥";
            default: return "●";
        }
    }
    #endregion
}
