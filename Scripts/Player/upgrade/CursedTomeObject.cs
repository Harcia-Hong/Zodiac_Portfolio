using UnityEngine;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// 금서의 잔재 — 월드 인터랙터블 오브젝트 (IRewardObject 구현)
/// - F키로 CardSelectionUI 호출 (카드 4장: 덮어둔다 + I/II/III)
/// - 선택하면 해당 등급 디버프 랜덤 1개 적용 + 스탯 보상 즉시 지급
/// - "덮어둔다" 선택 시에도 선택 완료로 처리 (다른 보상 정리)
/// - 1회용
/// </summary>
public class CursedTomeObject : MonoBehaviour, IRewardObject
{
    [Header("데이터")]
    [SerializeField] private CursedTomeDataSO tomeData;

    [Header("상호작용 설정")]
    [SerializeField] private KeyCode interactKey = KeyCode.F;

    [Header("UI 참조")]
    [SerializeField] private GameObject interactionPromptUI;
    [SerializeField] private TextMeshProUGUI nameTextUI;
    [SerializeField] private TextMeshProUGUI descriptionTextUI;

    [Header("사라짐 연출 설정")]
    [SerializeField, Tooltip("OnDismissed 시 가라앉는 깊이")]
    private float dismissSinkDepth = -3f;

    [SerializeField, Tooltip("OnDismissed 시 연출 시간")]
    private float dismissDuration = 0.6f;

    // 상태
    private bool isPlayerInRange = false;
    private bool isUsed = false;

    public event System.Action<IRewardObject> OnSelectedEvent;
    public event System.Action<IRewardObject> OnDestroyedEvent;

    #region Unity Lifecycle
    private void Awake()
    {
        if (interactionPromptUI != null)
            interactionPromptUI.SetActive(false);
    }

    private void Update()
    {
        if (isUsed || !isPlayerInRange) return;

        if (Input.GetKeyDown(interactKey))
        {
            OpenSelectionUI();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isUsed || !other.CompareTag("Player")) return;

        isPlayerInRange = true;
        ShowPrompt(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        isPlayerInRange = false;
        ShowPrompt(false);
    }

    private void OnDestroy()
    {
        transform.DOKill();
        OnDestroyedEvent?.Invoke(this);
    }
    #endregion

    #region 상호작용
    /// <summary>
    /// 카드 선택 UI 열기 — 덮어둔다(무시) + 등급 I/II/III 카드 생성
    /// </summary>
    private void OpenSelectionUI()
    {
        if (tomeData == null || CardSelectionUI.Instance == null)
        {
            Debug.LogWarning("[CursedTomeObject] tomeData 또는 CardSelectionUI가 없습니다.");
            return;
        }

        ShowPrompt(false);

        List<CardData> cards = new List<CardData>();

        // 카드 0: 덮어둔다 (무시)
        cards.Add(new CardData(
            "",
            "",
            tomeData.noneName,
            tomeData.noneDescription,
            null,
            tomeData.noneColor
        ));

        // 카드 1~3: 등급 I / II / III
        for (int i = 0; i < tomeData.GradeCount; i++)
        {
            CurseGradeData gradeData = tomeData.GetGradeData(i);
            if (gradeData == null) continue;

            string description = gradeData.gradeDescription;

            cards.Add(new CardData(
                gradeData.gradeName,
                "[저주]",
                gradeData.gradeName,
                description,
                null,
                gradeData.gradeColor
            ));
        }

        // UI 열기 (ESC 닫기 허용 — 닫으면 유지)
        CardSelectionUI.Instance.Show(tomeData.tomeName, cards, OnCardSelected, true);
    }

    /// <summary>
    /// CardSelectionUI 선택 콜백
    /// index -1 = 취소 (오브젝트 유지)
    /// index 0 = 덮어둔다 (무시 후 선택 완료 처리)
    /// index 1~3 = 등급 I/II/III (디버프 + 보상 적용 후 선택 완료 처리)
    /// </summary>
    private void OnCardSelected(int index)
    {
        // ESC 취소(-1) → 오브젝트 유지 (다시 사용 가능)
        if (index < 0)
        {
            Debug.Log("[CursedTomeObject] 취소 — 금서 유지");
            return;
        }

        // 덮어둔다(0) → 아무 효과 없음, 선택 완료 처리
        if (index == 0)
        {
            Debug.Log("[CursedTomeObject] 금서를 덮어두었습니다.");
            OnSelected();
            return;
        }

        // 등급 선택 (1=I, 2=II, 3=III)
        int gradeIndex = index - 1;

        DebuffEntrySO debuff = tomeData.GetRandomDebuff(gradeIndex);
        if (debuff != null)
        {
            RewardManager.Instance?.ApplyDebuff(debuff);
            Debug.Log($"[CursedTomeObject] 디버프 적용: {debuff.debuffName}");
        }

        CurseGradeData gradeData = tomeData.GetGradeData(gradeIndex);
        if (gradeData != null)
        {
            ApplyReward(gradeData);
        }

        OnSelected();
    }

    /// <summary>
    /// 보상 즉시 지급 — 해당 등급의 보상 풀에서 1개 뽑아 적용
    /// 최소 등급 이상이 보장됨
    /// </summary>
    private void ApplyReward(CurseGradeData gradeData)
    {
        if (gradeData.rewardBoostPool == null)
        {
            Debug.LogWarning("[CursedTomeObject] 보상 풀이 설정되지 않았습니다.");
            return;
        }

        List<StatBoostSelection> selections = gradeData.rewardBoostPool.GetRandomSelections(1);
        if (selections == null || selections.Count == 0) return;

        StatBoostSelection reward = selections[0];

        // 최소 등급 보장
        if (reward.grade < gradeData.minRewardGrade)
        {
            reward.grade = gradeData.minRewardGrade;
        }

        reward.entry.Apply(reward.grade);
        Debug.Log($"[CursedTomeObject] 보상 적용: {reward.entry.boostName} ({reward.grade})");
    }
    #endregion

    #region IRewardObject 구현

    /// <summary>
    /// 플레이어가 이 보상을 선택했을 때 호출 (카드 선택 또는 덮어둔다 선택 시점)
    /// - 획득/무시 연출 후 자기 자신만 제거
    /// - 다음 페이즈 진행은 포탈이 담당
    /// </summary>
    public void OnSelected()
    {
        if (isUsed) return;
        isUsed = true;

        ShowPrompt(false);

        // 획득 연출 — 솟아올랐다 사라짐
        transform.DOKill();
        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOMoveY(transform.position.y + 1.5f, 0.3f).SetEase(Ease.OutQuad));
        seq.Join(transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InQuad));
        seq.OnComplete(() => Destroy(gameObject));

        OnSelectedEvent?.Invoke(this);
    }

    /// <summary>
    /// 다른 보상이 선택되어 이 오브젝트가 사라져야 할 때 호출
    /// - 가라앉으며 Destroy
    /// </summary>
    public void OnDismissed()
    {
        if (isUsed) return;
        isUsed = true;

        ShowPrompt(false);

        transform.DOKill();
        transform.DOMoveY(transform.position.y + dismissSinkDepth, dismissDuration)
            .SetEase(Ease.InQuad)
            .OnComplete(() => Destroy(gameObject));
    }


    /// <summary>
    /// 오브젝트 Transform 접근용
    /// </summary>
    public Transform GetTransform() => transform;

    #endregion

    #region UI 헬퍼
    /// <summary>
    /// 상호작용 안내 표시/숨기기
    /// </summary>
    private void ShowPrompt(bool show)
    {
        if (interactionPromptUI != null)
        {
            if (show)
            {
                if (nameTextUI != null) nameTextUI.text = tomeData != null ? tomeData.tomeName : "금서의 잔재";
                if (descriptionTextUI != null) descriptionTextUI.text = tomeData != null ? tomeData.tomeDescription : "";
            }
            interactionPromptUI.SetActive(show);
        }
    }
    #endregion
}
