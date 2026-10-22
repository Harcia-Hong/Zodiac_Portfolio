using UnityEngine;
using TMPro;

/// <summary>
/// 회피 스택 UI - 스킬 슬롯 옆 원형 아이콘에 스택 숫자 표시
/// </summary>
public class DodgeStackUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private TextMeshProUGUI stackText;

    void Start()
    {
        // PlayerController 자동 찾기
        if (playerController == null)
        {
            playerController = FindFirstObjectByType<PlayerController>();
        }
    }

    void Update()
    {
        if (playerController == null) return;

        // 스택 숫자 업데이트
        stackText.text = playerController.CurrentDodgeStacks.ToString();
    }
}
