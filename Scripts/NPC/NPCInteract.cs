using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// NPC 상호작용 + 머리 위 대화 시스템
/// - 플레이어 접근 시 World Space UI로 이름/상호작용 안내
/// - F키 → 머리 위에 대사 표시 (말풍선 방식)
/// - 최초 대화 / 일반 대화 구분
/// </summary>
public class NPCInteract : MonoBehaviour
{
    [Header("World Space UI")]
    [SerializeField, Tooltip("Canvas 루트")]
    private GameObject canvasRoot;

    [SerializeField, Tooltip("NPC 이름 텍스트")]
    private TextMeshProUGUI nameText;

    [SerializeField, Tooltip("대사 텍스트 (이름 아래에 배치)")]
    private TextMeshProUGUI dialogueText;

    [SerializeField, Tooltip("안내 텍스트 ([F] : 대화 / [F] : 다음)")]
    private TextMeshProUGUI interactText;

    [Header("대화 데이터")]
    [SerializeField, Tooltip("최초 대화 (한 번만 재생)")]
    private DialogueDataSO firstDialogue;

    [SerializeField, Tooltip("일반 대화 (최초 이후 랜덤)")]
    private DialogueDataSO[] normalDialogues;

    [Header("설정")]
    [SerializeField] private KeyCode interactKey = KeyCode.F;
    [SerializeField] private float typeSpeed = 0.03f;

    // === 런타임 ===
    private bool isPlayerInRange = false;
    private bool hasFirstDialoguePlayed = false;
    private bool isInDialogue = false;
    private bool isTyping = false;
    private DialogueDataSO currentDialogue;
    private int currentLineIndex;
    private Coroutine typingCoroutine;

    private void Awake()
    {
        if (canvasRoot != null)
            canvasRoot.SetActive(false);

        // 대사 텍스트 초기 숨김
        if (dialogueText != null)
            dialogueText.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!isPlayerInRange) return;

        if (Input.GetKeyDown(interactKey))
        {
            if (!isInDialogue)
            {
                // 대화 시작
                StartDialogue();
            }
            else if (isTyping)
            {
                // 타이핑 중이면 즉시 완성
                CompleteLine();
            }
            else
            {
                // 다음 대사
                NextLine();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        isPlayerInRange = true;

        if (canvasRoot != null)
            canvasRoot.SetActive(true);

        // 대화 중이 아닐 때만 안내 표시
        if (!isInDialogue)
            ShowIdleUI();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        isPlayerInRange = false;

        // 대화 중이었으면 종료
        if (isInDialogue)
            EndDialogue();

        if (canvasRoot != null)
            canvasRoot.SetActive(false);
    }

    // ============================================================
    // UI 상태 전환
    // ============================================================

    /// <summary>
    /// 대기 상태 UI (이름 + [F] 대화)
    /// </summary>
    private void ShowIdleUI()
    {
        if (nameText != null)
        {
            nameText.gameObject.SetActive(true);
            nameText.text = firstDialogue != null ? firstDialogue.speakerName : "NPC";
        }

        if (dialogueText != null)
            dialogueText.gameObject.SetActive(false);

        if (interactText != null)
        {
            interactText.gameObject.SetActive(true);
            interactText.text = "[F] : 대화";
        }
    }

    /// <summary>
    /// 대화 상태 UI (이름 + 대사 + [F] 다음)
    /// </summary>
    private void ShowDialogueUI()
    {
        if (nameText != null)
            nameText.gameObject.SetActive(true);

        if (dialogueText != null)
            dialogueText.gameObject.SetActive(true);

        if (interactText != null)
        {
            interactText.gameObject.SetActive(true);
            interactText.text = "[F] : 다음";
        }
    }

    // ============================================================
    // 대화 진행
    // ============================================================

    /// <summary>
    /// 대화 시작
    /// </summary>
    private void StartDialogue()
    {
        currentDialogue = GetDialogue();

        if (currentDialogue == null)
        {
            Debug.LogWarning("[NPCInteract] 대화 데이터가 없습니다.");
            return;
        }

        isInDialogue = true;
        currentLineIndex = 0;

        // 플레이어 이동 잠금
        LockPlayer(true);

        // UI 전환
        ShowDialogueUI();

        // 첫 대사 표시
        ShowLine(currentDialogue.lines[currentLineIndex]);
    }

    /// <summary>
    /// 대사 한 줄 타이핑 효과로 표시
    /// </summary>
    private void ShowLine(string line)
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = StartCoroutine(TypeLine(line));
    }

    private IEnumerator TypeLine(string line)
    {
        isTyping = true;
        dialogueText.text = "";

        foreach (char c in line)
        {
            dialogueText.text += c;
            yield return new WaitForSeconds(typeSpeed);
        }

        isTyping = false;
    }

    /// <summary>
    /// 타이핑 즉시 완성
    /// </summary>
    private void CompleteLine()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        dialogueText.text = currentDialogue.lines[currentLineIndex];
        isTyping = false;
    }

    /// <summary>
    /// 다음 대사
    /// </summary>
    private void NextLine()
    {
        currentLineIndex++;

        if (currentLineIndex < currentDialogue.lines.Length)
        {
            ShowLine(currentDialogue.lines[currentLineIndex]);
        }
        else
        {
            EndDialogue();
        }
    }

    /// <summary>
    /// 대화 종료
    /// </summary>
    private void EndDialogue()
    {
        isInDialogue = false;

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        // 최초 대화 완료 처리
        if (!hasFirstDialoguePlayed && currentDialogue == firstDialogue)
        {
            hasFirstDialoguePlayed = true;
        }

        // 플레이어 이동 잠금 해제
        LockPlayer(false);

        // UI 복귀
        if (isPlayerInRange)
            ShowIdleUI();
    }

    /// <summary>
    /// 상황에 맞는 대화 데이터 반환
    /// </summary>
    private DialogueDataSO GetDialogue()
    {
        if (!hasFirstDialoguePlayed && firstDialogue != null)
            return firstDialogue;

        if (normalDialogues != null && normalDialogues.Length > 0)
            return normalDialogues[Random.Range(0, normalDialogues.Length)];

        return null;
    }

    /// <summary>
    /// 플레이어 이동 잠금/해제
    /// </summary>
    private void LockPlayer(bool locked)
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            player.isSkillCasting = locked;
    }
}
