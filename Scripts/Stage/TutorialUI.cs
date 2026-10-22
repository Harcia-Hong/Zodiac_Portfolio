using UnityEngine;

/// <summary>
/// 튜토리얼 조작법 안내 UI
/// - 씬 시작 시 자동으로 보임 (씬에 활성 상태로 박아두면 끝)
/// - 게임 자동 일시정지 (적이 공격 못하게)
/// - Enter 누르거나 버튼 클릭으로 닫힘 → 게임 재개
/// </summary>
public class TutorialUI : MonoBehaviour
{
    /// <summary>
    /// 씬 진입 시 게임 정지
    /// 적이 플레이어 공격하는 거 막음
    /// </summary>
    //private void Start()
    //{
    //    Time.timeScale = 0f;
    //}

    /// <summary>
    /// Enter 키 입력 감지 (게임 정지 중에도 Input은 작동함)
    /// </summary>
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            Close();
    }

    /// <summary>
    /// UI 닫기 + 게임 재개
    /// "이어서 하기" 버튼 OnClick에 이 메서드 연결
    /// </summary>
    public void Close()
    {
        Time.timeScale = 1f;
        gameObject.SetActive(false);
    }
}
