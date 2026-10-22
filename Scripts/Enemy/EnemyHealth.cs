using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    #region Variables

    [Header("Sound")]
    [Tooltip("적이 피격될 때 재생할 효과음")]
    [SerializeField] private AudioClip hitSfx;

    [Tooltip("적이 사망할 때 재생할 효과음")]
    [SerializeField] private AudioClip deathSfx;

    [Header("===== 체력 설정 =====")]
    [SerializeField] private int _maxHealth = 100;
    [SerializeField] private int _curHealth;

    public bool isDead = false;
    #endregion

    #region Properties
    /// <summary>최대 체력</summary>
    public int maxHealth => _maxHealth;

    /// <summary>현재 체력</summary>
    public int curHealth => _curHealth;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        _curHealth = _maxHealth;
        isDead = false;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 데미지 받기 — 사운드 재생 포함
    /// 살아있는 동안: 피격 사운드 재생
    /// 체력 0 이하 도달: 사망 사운드 재생 + isDead 플래그 ON
    /// </summary>
    /// <param name="damage">받을 데미지량</param>
    public void TakeDamage(int damage)
    {
        if (isDead) return;

        _curHealth -= damage;
        _curHealth = Mathf.Clamp(_curHealth, 0, _maxHealth);

        if (_curHealth <= 0)
        {
            // 사망 처리 + 사망 사운드
            isDead = true;

            if (deathSfx != null && SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySFX(deathSfx);
            }
        }
        else
        {
            // 피격 사운드 (살아있는 동안에만 재생)
            if (hitSfx != null && SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySFX(hitSfx);
            }
        }
    }

    /// <summary>
    /// 최대 체력 설정 (스폰 시 EnemySO에서 값 받아올 때 사용)
    /// </summary>
    /// <param name="newMaxHealth">새 최대 체력</param>
    public void SetMaxHealth(int newMaxHealth)
    {
        _maxHealth = newMaxHealth;
        _curHealth = _maxHealth;
    }
    #endregion
}
