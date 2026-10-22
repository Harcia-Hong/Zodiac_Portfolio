using UnityEngine;

public class PlayerExp : MonoBehaviour
{
    [Header("설정")]
    public int currentLevel => PlayerStats.Instance != null ? PlayerStats.Instance.Level : 1;
    public float curExp => PlayerStats.Instance != null ? PlayerStats.Instance.CurrentEXP : 0f;
    public float maxExp => PlayerStats.Instance != null ? PlayerStats.Instance.MaxEXP : 100f;

    // 경험치 획득 테스트용 (나중에 몬스터 처치 시 이 함수를 부르면 됨)
    public void GainExp(float amount)
    {
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.GainEXP(amount);
        }
        else
        {
            Debug.LogError("[PlayerExp] PlayerStats.Instance가 없습니다!");
        }
    }
}
