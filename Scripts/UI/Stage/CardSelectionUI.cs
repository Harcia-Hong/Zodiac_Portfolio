using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;
using System.Collections.Generic;

/// <summary>
/// 카드 1장의 데이터 — UI에 표시할 정보를 담는 구조체
/// 서판, 금서 등 어떤 시스템이든 이 형태로 데이터를 넘겨주면 됨
/// </summary>
[System.Serializable]
public class CardData
{
    public string title;
    public string category;
    public string cardName;
    public string description;
    public Sprite icon;
    public Color gradeColor;

    public CardData(string title, string category, string cardName,
                    string description, Sprite icon, Color gradeColor)
    {
        this.title = title;
        this.category = category;
        this.cardName = cardName;
        this.description = description;
        this.icon = icon;
        this.gradeColor = gradeColor;
    }
}

/// <summary>
/// 범용 카드 선택 UI
/// - 카드 3~4장을 화면 중앙에 표시
/// - 서판(스탯 강화), 금서(디버프+보상) 등 여러 시스템에서 공용으로 사용
/// - 외부에서 카드 데이터 + 선택 콜백을 넘겨주면 표시
/// 
/// 사용 예:
///   CardSelectionUI.Instance.Show("흩어진 서판", cardList, OnCardSelected);
/// </summary>
public class CardSelectionUI : MonoBehaviour
{
    public static CardSelectionUI Instance { get; private set; }

    [Header("UI 요소")]
    [SerializeField] private GameObject panel;
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("카드 슬롯 (최대 4개 배치해두기)")]
    [SerializeField] private List<CardSlotUI> cardSlots;

    [Header("닫기 설정")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;
    [SerializeField] private bool allowClose = true;

    [Header("애니메이션")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float fadeOutDuration = 0.2f;

    private Action<int> onCardSelected;
    private bool isOpen = false;

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    private void Update()
    {
        if (!isOpen) return;

        if (allowClose && Input.GetKeyDown(closeKey))
        {
            Close(-1);
        }
    }
    #endregion

    #region 외부 호출 — 열기/닫기
    public void Show(string title, List<CardData> cards, Action<int> onSelected, bool canClose = true)
    {
        if (isOpen || cards == null || cards.Count == 0) return;

        isOpen = true;
        onCardSelected = onSelected;
        allowClose = canClose;

        if (titleText != null)
            titleText.text = title;

        SetupCards(cards);

        panel.SetActive(true);

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.DOFade(1f, fadeInDuration).SetUpdate(true);
        }

        Time.timeScale = 0f;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void Close(int selectedIndex)
    {
        if (!isOpen) return;

        isOpen = false;

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.DOFade(0f, fadeOutDuration)
                .SetUpdate(true)
                .OnComplete(() => panel.SetActive(false));
        }
        else
        {
            panel.SetActive(false);
        }

        Time.timeScale = 1f;

        //Cursor.visible = false;
        //Cursor.lockState = CursorLockMode.Locked;

        onCardSelected?.Invoke(selectedIndex);
        onCardSelected = null;
    }
    #endregion

    #region 카드 세팅
    private void SetupCards(List<CardData> cards)
    {
        for (int i = 0; i < cardSlots.Count; i++)
        {
            if (i < cards.Count)
            {
                cardSlots[i].gameObject.SetActive(true);
                int index = i;
                cardSlots[i].Setup(cards[i], () => OnCardClicked(index));
            }
            else
            {
                cardSlots[i].gameObject.SetActive(false);
            }
        }
    }

    private void OnCardClicked(int index)
    {
        Close(index);
    }
    #endregion
}
