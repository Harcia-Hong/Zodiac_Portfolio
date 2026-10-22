using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 플레이어와 상호작용 가능한 보상 오브젝트 (스킬 드롭 등) — IRewardObject 구현
/// - 활성 상태: Glow 발광
/// - 인터랙트 시: 스킬 드롭 + Glow 꺼짐 + 자기 파괴
/// - 다른 보상이 선택됐을 때: OnDismissed로 가라앉으며 사라짐
/// </summary>
public class InteractableRewardObject : MonoBehaviour, IRewardObject
{
    [Header("상호작용 설정")]
    [SerializeField, Tooltip("상호작용 키")]
    private KeyCode interactKey = KeyCode.F;
    [SerializeField, Tooltip("오브젝트 이름")]
    private string objectName = "테스트 오브젝트";

    [Header("UI 참조")]
    [SerializeField, Tooltip("상호작용 프롬프트 UI ( F키로 작동 )")]
    private GameObject interactionPromptUI;
    [SerializeField, Tooltip("오브젝트 이름 텍스트")]
    private TextMeshProUGUI nameTextUI;

    [Header("보상 설정")]
    [SerializeField, Tooltip("상호작용 시 드랍할 아이템 프리팹")]
    private GameObject rewardItemPrefab;
    [SerializeField, Tooltip("랜덤 스킬 목록 테이블")]
    private SkillLootTable skillLootTable;
    [SerializeField, Tooltip("아이템 드랍 위치")]
    private Transform spawnPoint;

    [Header("보상 스폰 연출")]
    [SerializeField] private GameObject rewardSpawnEffectPrefab;
    [SerializeField] private float effectWaitTime = 0.8f;

    [Header("Glow 설정")]
    [SerializeField, Tooltip("발광 색상")]
    private Color glowColor = new Color(1f, 1f, 0.5f, 1f);
    [SerializeField, Tooltip("발광 강도")]
    private float glowIntensity = 1.5f;
    [SerializeField, Tooltip("발광 꺼지는 시간")]
    private float glowFadeOutDuration = 0.5f;

    [Header("사라짐 연출 설정 (OnDismissed)")]
    [SerializeField, Tooltip("OnDismissed 시 가라앉는 깊이")]
    private float dismissSinkDepth = -3f;
    [SerializeField, Tooltip("OnDismissed 시 연출 시간")]
    private float dismissDuration = 0.6f;

    // 상태 변수
    private bool isPlayerInRange = false;
    private bool isUsed = false;

    // IRewardObject 이벤트
    public event System.Action<IRewardObject> OnSelectedEvent;
    public event System.Action<IRewardObject> OnDestroyedEvent;

    // Glow 관련
    private Renderer[] renderers;
    private MaterialPropertyBlock propertyBlock;
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionSelfGlowID = Shader.PropertyToID("_EmissionSelfGlow");

    #region Unity Lifecycle

    private void Awake()
    {
        if (interactionPromptUI != null)
            interactionPromptUI.SetActive(false);

        if (spawnPoint == null)
            spawnPoint = transform;

        // Glow 초기화
        renderers = GetComponentsInChildren<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
    }

    private void Start()
    {
        ApplyGlow(glowIntensity);
    }

    private void OnDestroy()
    {
        // 진행 중인 트윈 정리 (NRE 방지)
        transform.DOKill();
        OnDestroyedEvent?.Invoke(this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isUsed || !other.CompareTag("Player")) return;

        isPlayerInRange = true;
        ShowInteractionUI(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        isPlayerInRange = false;
        ShowInteractionUI(false);
    }

    private void Update()
    {
        if (isUsed || !isPlayerInRange) return;

        if (Input.GetKeyDown(interactKey))
        {
            // IRewardObject.OnSelected 호출 — 자체 처리 + 이벤트 발행
            OnSelected();
        }
    }

    #endregion

    #region UI

    private void ShowInteractionUI(bool show)
    {
        if (interactionPromptUI != null)
        {
            if (nameTextUI != null && show)
                nameTextUI.text = objectName;

            interactionPromptUI.SetActive(show);
        }
    }

    #endregion

    #region IRewardObject 구현

    /// <summary>
    /// 플레이어가 F키 눌렀을 때 호출.
    /// 새 흐름:
    ///   1. SkillCardUI를 열어 lootTable에서 뽑은 카드 N장 표시
    ///   2. 사용자가 카드 픽 → HandleCardPicked 콜백에서 실제 스킬 드롭 + Glow 페이드
    ///   3. ESC로 취소 시 isUsed가 false 유지 → 다시 F키 가능
    /// </summary>
    public void OnSelected()
    {
        if (isUsed) return;

        if (SkillCardUI.Instance != null && skillLootTable != null)
        {
            List<SkillData> pool = BuildSkillPool();
            if (pool.Count > 0)
            {
                // 카드 픽 콜백 + 업그레이드 진입 콜백 둘 다 등록
                SkillCardUI.Instance.Open(pool, HandleCardPicked, HandleUpgradeRequested);
                return;
            }

            Debug.Log($"[{objectName}] 얻을 수 있는 스킬이 없습니다 (모두 보유 중).");
            ConsumeWithoutDrop();
            return;
        }

        Debug.LogWarning("[InteractableRewardObject] SkillCardUI/lootTable 없음 — 즉시 드롭 폴백");
        FallbackImmediateDrop();
    }

    /// <summary>
    /// 카드 UI 픽 콜백 — 실제 스킬 드롭 + Glow 페이드 + 콜라이더 끄기.
    /// 기존 OnSelected의 후처리를 여기로 이동.
    /// </summary>
    private void HandleCardPicked(SkillData pickedSkill)
    {
        if (isUsed) return;
        isUsed = true;

        // 1. UI 숨기기
        ShowInteractionUI(false);

        // 2. 콜라이더 끄기 (재상호작용 방지)
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // 3. 픽한 스킬 드랍
        if (rewardItemPrefab != null && pickedSkill != null)
        {
            StartCoroutine(SpawnRewardItemRoutine(pickedSkill));
        }

        // 4. Glow 페이드
        StartCoroutine(GlowFadeOutRoutine());

        Debug.Log($"[{objectName}] '{pickedSkill.skillName}' 픽 → 빛이 꺼집니다.");

        // 5. 그룹 시스템 알림
        OnSelectedEvent?.Invoke(this);
    }

    /// <summary>
    /// "업그레이드 포인트로 전환" 클릭 시 호출.
    /// 강화 처리는 UpgradeModeController가 알아서 — 여기는 캡슐만 소진 처리.
    /// 드롭은 안 함 (강화 = 보상 1회 소진).
    /// </summary>
    private void HandleUpgradeRequested()
    {
        if (isUsed) return;
        Debug.Log($"[{objectName}] 업그레이드 포인트로 전환 — 캡슐 소진");
        ConsumeWithoutDrop();
    }

    /// <summary>
    /// SkillLootTable에서 카드 후보 풀 만들기.
    /// weight 무시하고 전체 해금 스킬을 가져온 후:
    ///   - 이미 장착된 스킬 제외
    /// 결정적이라 weight 편향으로 풀이 작아지는 문제 없음.
    /// </summary>
    private List<SkillData> BuildSkillPool()
    {
        List<SkillData> pool = new List<SkillData>();
        if (skillLootTable == null) return pool;

        List<SkillData> allUnlocked = skillLootTable.GetAllUnlockedSkills();
        foreach (var skill in allUnlocked)
        {
            if (skill == null) continue;
            if (SkillManager.Instance != null && SkillManager.Instance.IsSkillEquipped(skill)) continue;
            pool.Add(skill);
        }

        return pool;
    }

    /// <summary>
    /// 폴백 — 카드 UI 못 쓸 때 기존 방식으로 즉시 1장 드롭.
    /// </summary>
    private void FallbackImmediateDrop()
    {
        isUsed = true;
        ShowInteractionUI(false);

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        if (rewardItemPrefab != null && skillLootTable != null)
        {
            SkillData selected = skillLootTable.GetRandomSkill();
            if (selected != null) StartCoroutine(SpawnRewardItemRoutine(selected));
        }

        StartCoroutine(GlowFadeOutRoutine());
        OnSelectedEvent?.Invoke(this);
    }

    /// <summary>
    /// 풀이 비어 있을 때 — 드롭 없이 캡슐만 사용 처리.
    /// 모든 스킬을 이미 가진 상태에서 발생.
    /// Glow 페이드 + 콜라이더 끄기 + isUsed 처리.
    /// </summary>
    private void ConsumeWithoutDrop()
    {
        isUsed = true;
        ShowInteractionUI(false);

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        StartCoroutine(GlowFadeOutRoutine());
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

        ShowInteractionUI(false);

        // 콜라이더 끄기
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // Glow도 같이 꺼짐
        StartCoroutine(GlowFadeOutRoutine());

        // 가라앉는 연출
        transform.DOKill();
        transform.DOMoveY(transform.position.y + dismissSinkDepth, dismissDuration)
            .SetEase(Ease.InQuad)
            .OnComplete(() => Destroy(gameObject));
    }

    /// <summary>오브젝트 Transform 접근용</summary>
    public Transform GetTransform() => transform;

    #endregion

    #region 보상 드랍

    private IEnumerator SpawnRewardItemRoutine(SkillData skillToSpawn)
    {
        Vector3 offsetPos = GetRandomSpawnOffset();
        Vector3 spawnPos = spawnPoint.position + offsetPos;

        if (rewardSpawnEffectPrefab != null)
        {
            GameObject effect = Instantiate(rewardSpawnEffectPrefab, spawnPos, Quaternion.identity);
            Destroy(effect, 2f);
        }

        yield return new WaitForSeconds(effectWaitTime);

        GameObject droppedItemInstance = Instantiate(rewardItemPrefab, spawnPos, Quaternion.identity);

        DroppedSkillItem skillItemLogic = droppedItemInstance.GetComponent<DroppedSkillItem>();
        if (skillItemLogic != null)
        {
            skillItemLogic.Initialize(skillToSpawn);
        }
    }

    private Vector3 GetRandomSpawnOffset()
    {
        float offsetDistance = 2f;

        Vector3[] offsets = new Vector3[]
        {
            transform.right * offsetDistance,
            -transform.right * offsetDistance,
            transform.forward * offsetDistance
        };

        return offsets[Random.Range(0, offsets.Length)];
    }

    #endregion

    #region Glow

    private void ApplyGlow(float intensity)
    {
        foreach (var rend in renderers)
        {
            if (rend == null) continue;

            rend.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColorID, glowColor * intensity);
            propertyBlock.SetFloat(EmissionSelfGlowID, intensity);
            rend.SetPropertyBlock(propertyBlock);
        }
    }

    private IEnumerator GlowFadeOutRoutine()
    {
        float elapsed = 0f;

        while (elapsed < glowFadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / glowFadeOutDuration;
            float currentIntensity = Mathf.Lerp(glowIntensity, 0f, t);
            ApplyGlow(currentIntensity);
            yield return null;
        }

        ApplyGlow(0f);
    }

    #endregion
}
