using UnityEngine;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// 흩어진 서판 — 월드 인터랙터블 오브젝트 (IRewardObject 구현)
/// - 플레이어 접근 시 상호작용 안내 표시
/// - F키로 StatBoostPoolSO에서 3개 뽑아 CardSelectionUI 호출
/// - 선택 완료 시 스탯 적용 + StageManager에 선택 알림
/// - 다른 보상이 선택되면 OnDismissed()로 가라앉으며 사라짐
/// </summary>
public class StatBoostObject : MonoBehaviour, IRewardObject
{
    [Header("데이터")]
    [SerializeField] private StatBoostPoolSO boostPool;

    [Header("상호작용 설정")]
    [SerializeField] private KeyCode interactKey = KeyCode.F;
    [SerializeField] private string displayName = "흩어진 서판";
    [SerializeField] private string promptText = "스탯을 강화할 수 있습니다.";

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

    // 뽑힌 선택지 캐싱 (선택 시 적용하기 위해)
    private List<StatBoostSelection> currentSelections;

    // IRewardObject 그룹 알림 이벤트
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
        if (isUsed) return;
        if (!isPlayerInRange) return;

        if (Input.GetKeyDown(interactKey))
        {
            OpenSelectionUI();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isUsed) return;
        if (!other.CompareTag("Player")) return;

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
        // DOTween 정리 (오브젝트 파괴 시 트윈 잔상 방지)
        transform.DOKill();
        OnDestroyedEvent?.Invoke(this);
    }
    #endregion

    #region 상호작용
    /// <summary>
    /// 카드 선택 UI 열기 — 풀에서 3개 뽑아서 CardData로 변환 후 전달
    /// </summary>
    private void OpenSelectionUI()
    {
        if (boostPool == null)
        {
            Debug.LogWarning("[StatBoostObject] boostPool이 설정되지 않았습니다.");
            return;
        }

        if (CardSelectionUI.Instance == null)
        {
            Debug.LogWarning("[StatBoostObject] CardSelectionUI.Instance가 없습니다.");
            return;
        }

        ShowPrompt(false);

        // 풀에서 3개 뽑기
        currentSelections = boostPool.GetRandomSelections(3);

        // CardData 리스트로 변환
        List<CardData> cards = new List<CardData>();
        foreach (var selection in currentSelections)
        {
            string gradeTitle = GetGradeDisplayName(selection.grade);
            Color gradeColor = StatBoostEntrySO.GetGradeColor(selection.grade);
            string effectText = selection.entry.GetDisplayText(selection.grade);

            cards.Add(new CardData(
                gradeTitle,
                "[스탯]",
                selection.entry.boostName,
                effectText,
                selection.entry.icon,
                gradeColor
            ));
        }

        // UI 열기
        CardSelectionUI.Instance.Show(displayName, cards, OnCardSelected, true);
    }

    /// <summary>
    /// CardSelectionUI 카드 선택 콜백
    /// - 유효한 카드 선택 시 스탯 적용 후 IRewardObject.OnSelected() 호출
    /// </summary>
    private void OnCardSelected(int index)
    {
        // 취소 (-1)
        if (index < 0 || currentSelections == null || index >= currentSelections.Count)
        {
            // 취소해도 오브젝트 유지 (다시 사용 가능)
            currentSelections = null;
            return;
        }

        // 선택한 스탯 강화 적용
        StatBoostSelection selected = currentSelections[index];
        selected.entry.Apply(selected.grade);

        Debug.Log($"[StatBoostObject] '{selected.entry.boostName}' ({selected.grade}) 적용 완료");

        currentSelections = null;

        // IRewardObject.OnSelected() 호출 → StageManager에 알림 + 제거
        OnSelected();
    }
    #endregion

    #region IRewardObject 구현

    /// <summary>
    /// 플레이어가 이 보상을 선택했을 때 호출 (카드 선택 완료 시점)
    /// - 획득 연출 후 자기 자신만 제거
    /// - 나머지 보상 정리와 다음 페이즈 진행은 포탈이 담당
    /// </summary>
    public void OnSelected()
    {
        if (isUsed) return;
        isUsed = true;

        ShowPrompt(false);

        // 획득 연출 — 살짝 솟아올랐다 사라짐
        transform.DOKill();
        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOMoveY(transform.position.y + 1.5f, 0.3f).SetEase(Ease.OutQuad));
        seq.Join(transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InQuad));
        seq.OnComplete(() => Destroy(gameObject));

        // 그룹에 선택 알림 — 같은 그룹의 나머지 멤버가 OnDismissed로 사라짐
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

        // 가라앉는 연출
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
                if (nameTextUI != null) nameTextUI.text = displayName;
                if (descriptionTextUI != null) descriptionTextUI.text = promptText;
            }
            interactionPromptUI.SetActive(show);
        }
    }

    /// <summary>
    /// 등급 표시 이름 반환
    /// </summary>
    private string GetGradeDisplayName(StatBoostGrade grade)
    {
        switch (grade)
        {
            case StatBoostGrade.Common: return "Common";
            case StatBoostGrade.Uncommon: return "Uncommon";
            case StatBoostGrade.Rare: return "Rare";
            case StatBoostGrade.Legendary: return "Legendary";
            default: return "Common";
        }
    }
    #endregion
}
