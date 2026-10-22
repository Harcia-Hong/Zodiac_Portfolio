using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillUpgradeUI : MonoBehaviour
{
    #region Singleton
    public static SkillUpgradeUI Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    #endregion

    #region UI References
    [Header("메인 UI 요소")]
    [SerializeField] private GameObject upgradeOverlayPanel;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("데이터 연결")]
    [SerializeField] private SkillUpgradeCost upgradeCostData;

    [Header("결과창 UI")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private Image resultSkillIcon;
    [SerializeField] private TextMeshProUGUI resultSkillName;
    [SerializeField] private TextMeshProUGUI resultStatsChange;

    [Header("설정")]
    [SerializeField] private KeyCode closeKey = KeyCode.F;
    [SerializeField] private float zoomScale = 1.2f;
    #endregion

    private bool isOpen = false;
    private SkillUpgradeObject currentCaller;

    private void Start()
    {
        if (upgradeOverlayPanel != null) upgradeOverlayPanel.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(false);
    }

    private void Update()
    {
        if (!isOpen) return;

        if (Input.GetKeyDown(closeKey) || Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }

        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");
        if (moveX != 0 || moveY != 0) Close();
    }

    public void Open(SkillUpgradeObject caller)
    {
        if (isOpen) return;
        isOpen = true;
        currentCaller = caller;

        if (upgradeOverlayPanel != null)
        {
            upgradeOverlayPanel.SetActive(true);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.DOFade(1f, 0.3f).SetUpdate(true);
            }
        }

        if (CombatUIManager.Instance != null)
            CombatUIManager.Instance.ExpandUI(true, true);

        SetSlotsUpgradeMode(true);
        Debug.Log("[SkillUpgradeUI] 강화 모드 시작");
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        if (upgradeOverlayPanel != null)
        {
            if (canvasGroup != null)
            {
                canvasGroup.DOFade(0f, 0.2f).SetUpdate(true).OnComplete(() =>
                {
                    upgradeOverlayPanel.SetActive(false);
                });
            }
            else
            {
                upgradeOverlayPanel.SetActive(false);
            }
        }

        if (CombatUIManager.Instance != null)
            CombatUIManager.Instance.ExpandUI(false, true);

        SetSlotsUpgradeMode(false);

        if (currentCaller != null)
        {
            currentCaller.OnUIClosed();
            currentCaller = null;
        }

        Debug.Log("[SkillUpgradeUI] 강화 모드 종료");
    }

    private void SetSlotsUpgradeMode(bool active)
    {
        // Unity 2023+ 호환 (FindObjectsSortMode.None 사용)
        SkillSlotUI[] slots = FindObjectsByType<SkillSlotUI>(FindObjectsSortMode.None);

        foreach (var slot in slots)
        {
            if (active)
            {
                KeyCode key = slot.GetSlotKey();
                int currentLvl = SkillManager.Instance.GetSkillLevel(key);
                int cost = GetUpgradeCost(currentLvl);
                slot.SetUpgradeMode(true, cost);
            }
            else
            {
                slot.SetUpgradeMode(false);
            }
        }
    }

    public void TryUpgradeSkill(KeyCode key)
    {
        SkillData skill = SkillManager.Instance.GetEquippedSkill(key);
        if (skill == null) return;

        int currentLvl = SkillManager.Instance.GetSkillLevel(key);
        int cost = GetUpgradeCost(currentLvl);

        int myStardust = PlayerStats.Instance.Stardust;
        if (myStardust < cost)
        {
            Debug.Log($"돈 부족! (보유: {myStardust}, 필요: {cost})");
            return;
        }

        if (currentLvl >= 20) return;

        PlayerStats.Instance.UseStardust(cost);
        SkillManager.Instance.UpgradeSkill(key);

        Debug.Log($"{skill.skillName} 강화 성공! Lv.{currentLvl} -> Lv.{currentLvl + 1}");

        ShowResultPanel(skill, currentLvl, currentLvl + 1);
        SetSlotsUpgradeMode(true);
    }

    private void ShowResultPanel(SkillData skill, int prevLv, int nextLv)
    {
        if (resultPanel == null) return;

        resultPanel.SetActive(true);

        if (resultSkillIcon != null) resultSkillIcon.sprite = skill.skillIcon;

        if (resultSkillName != null)
        {
            string levelText = $"{prevLv} ▶ {GetColoredText(nextLv.ToString(), "#FFD700")}";
            float prevCool = GetCooldown(prevLv);
            float nextCool = GetCooldown(nextLv);
            string coolText = $"{prevCool:F1}초 ▶ {GetColoredText(nextCool.ToString("F1") + "초", "#FFD700")}";

            resultSkillName.text = $"{skill.skillName}   {levelText}            {coolText}";
        }

        if (resultStatsChange != null)
        {
            float prevDmg = GetDamage(prevLv);
            float nextDmg = GetDamage(nextLv);
            string dmgChangeStr = $"{prevDmg} ▶ {GetColoredText(nextDmg.ToString(), "#FFD700")}";

            // ▼▼▼ [수정됨] description으로 변경 완료 ▼▼▼
            if (skill.description.Contains("{0}"))
            {
                resultStatsChange.text = string.Format(skill.description, dmgChangeStr);
            }
            else
            {
                resultStatsChange.text = $"{skill.description}\n\n[공격력] {dmgChangeStr}";
            }
        }

        CancelInvoke(nameof(HideResultPanel));
        Invoke(nameof(HideResultPanel), 3.0f);
    }

    private void HideResultPanel()
    {
        if (resultPanel != null) resultPanel.SetActive(false);
    }

    private int GetUpgradeCost(int level)
    {
        if (upgradeCostData != null) return upgradeCostData.GetCost(level);
        return (level + 1) * 10;
    }

    private string GetColoredText(string text, string hexColor)
    {
        return $"<color={hexColor}>{text}</color>";
    }

    private float GetCooldown(int level)
    {
        float baseCool = 10.0f;
        return Mathf.Max(1.0f, baseCool - (level * 0.5f));
    }

    private float GetDamage(int level)
    {
        float baseDmg = 100f;
        return baseDmg + (level * 20f);
    }
}
