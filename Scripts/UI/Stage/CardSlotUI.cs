using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;

/// <summary>
/// 카드 슬롯 1장의 UI 컴포넌트
/// CardSelectionUI의 자식으로 배치 (최대 4개)
/// </summary>
public class CardSlotUI : MonoBehaviour
{
    [Header("UI 요소")]
    [SerializeField] private Image cardBackground;
    [SerializeField] private Image borderImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI gradeText;
    [SerializeField] private TextMeshProUGUI categoryText;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Button selectButton;

    [Header("호버 효과")]
    [SerializeField] private float hoverScale = 1.05f;
    [SerializeField] private float hoverDuration = 0.15f;

    private Vector3 originalScale;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    public void Setup(CardData data, Action onClick)
    {
        if (gradeText != null)
        {
            gradeText.text = data.title;
            gradeText.color = data.gradeColor;
        }

        if (categoryText != null)
            categoryText.text = data.category;

        if (nameText != null)
            nameText.text = data.cardName;

        if (descriptionText != null)
            descriptionText.text = data.description;

        if (iconImage != null)
        {
            if (data.icon != null)
            {
                iconImage.sprite = data.icon;
                iconImage.gameObject.SetActive(true);
            }
            else
            {
                iconImage.gameObject.SetActive(false);
            }
        }

        if (borderImage != null)
            borderImage.color = data.gradeColor;

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onClick?.Invoke());
        }

        transform.localScale = originalScale;
    }

    public void OnPointerEnter()
    {
        transform.DOScale(originalScale * hoverScale, hoverDuration).SetUpdate(true);
    }

    public void OnPointerExit()
    {
        transform.DOScale(originalScale, hoverDuration).SetUpdate(true);
    }
}
