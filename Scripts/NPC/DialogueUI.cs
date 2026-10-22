using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;

/// <summary>
/// 대화 UI 컨트롤러
/// - 화면 하단에 초상화 + 이름 + 대사 표시
/// - F키 또는 클릭으로 다음 대사 진행
/// - 대화 중 플레이어 이동 잠금
/// </summary>
public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance { get; private set; }

    [Header("UI 요소")]
    [SerializeField, Tooltip("대화창 전체 패널")]
    private GameObject dialoguePanel;

    [SerializeField, Tooltip("NPC 이름 텍스트")]
    private TextMeshProUGUI nameText;

    [SerializeField, Tooltip("대사 텍스트")]
    private TextMeshProUGUI dialogueText;

    [SerializeField, Tooltip("NPC 초상화 이미지")]
    private Image portraitImage;

    [SerializeField, Tooltip("다음 대사 안내 텍스트 (예: 'F 다음')")]
    private TextMeshProUGUI promptText;

    [Header("설정")]
    [SerializeField, Tooltip("글자 출력 속도 (초당)")]
    private float typeSpeed = 0.03f;

    [SerializeField, Tooltip("다음 대사 키")]
    private KeyCode nextKey = KeyCode.F;

    // === 런타임 ===
    private DialogueDataSO currentDialogue;
    private int currentLineIndex;
    private bool isTyping;
    private bool isDialogueActive;
    private Coroutine typingCoroutine;
    private Action onDialogueEnd;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }

    private void Update()
    {
        if (!isDialogueActive) return;

        if (Input.GetKeyDown(nextKey) || Input.GetMouseButtonDown(0))
        {
            if (isTyping)
            {
                // 타이핑 중이면 즉시 완성
                CompleteLine();
            }
            else
            {
                // 다음 대사로
                NextLine();
            }
        }
    }

    /// <summary>
    /// 대화 시작 — NPCInteract에서 호출
    /// </summary>
    /// <param name="dialogue">대화 데이터</param>
    /// <param name="onEnd">대화 종료 시 콜백 (null 가능)</param>
    public void StartDialogue(DialogueDataSO dialogue, Action onEnd = null)
    {
        if (dialogue == null || dialogue.lines.Length == 0)
        {
            Debug.LogWarning("[DialogueUI] 대사 데이터가 비어있습니다.");
            return;
        }

        currentDialogue = dialogue;
        currentLineIndex = 0;
        onDialogueEnd = onEnd;
        isDialogueActive = true;

        // NPC 정보 세팅
        if (nameText != null)
            nameText.text = dialogue.speakerName;

        if (portraitImage != null && dialogue.speakerPortrait != null)
        {
            portraitImage.sprite = dialogue.speakerPortrait;
            portraitImage.gameObject.SetActive(true);
        }
        else if (portraitImage != null)
        {
            portraitImage.gameObject.SetActive(false);
        }

        if (promptText != null)
            promptText.text = $"[{nextKey}] 다음";

        // 플레이어 이동 잠금
        LockPlayer(true);

        // 패널 표시
        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);

        // 첫 번째 대사 표시
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
    /// 다음 대사로 진행
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
        isDialogueActive = false;

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        // 플레이어 이동 잠금 해제
        LockPlayer(false);

        // 종료 콜백
        onDialogueEnd?.Invoke();

        Debug.Log("[DialogueUI] 대화 종료");
    }

    /// <summary>
    /// 플레이어 이동 잠금/해제
    /// </summary>
    private void LockPlayer(bool locked)
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            player.isSkillCasting = locked;
        }
    }

    /// <summary>
    /// 현재 대화 중인지 확인
    /// </summary>
    public bool IsActive() => isDialogueActive;
}
