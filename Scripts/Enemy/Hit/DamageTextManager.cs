using UnityEngine;

public class DamageTextManager : MonoBehaviour
{
    #region Singleton
    public static DamageTextManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    #endregion

    #region Settings
    [Header("===== 프리팹 =====")]
    [SerializeField] private GameObject damageTextPrefab;

    [Header("===== 오프셋 =====")]
    [Tooltip("HP바 위로 얼마나 띄울지")]
    [SerializeField] private float heightOffset = 2f;

    [Tooltip("좌우 랜덤 범위 (겹침 방지)")]
    [SerializeField] private float randomOffsetX = 0.3f;
    #endregion

    #region Public Methods
    /// <summary>
    /// 데미지 텍스트 생성
    /// </summary>
    /// <param name="worldPosition">대상 위치 (Enemy 위치)</param>
    /// <param name="damage">데미지 수치</param>
    /// <param name="isCritical">크리티컬 여부</param>
    public void Show(Vector3 worldPosition, int damage, bool isCritical = false)
    {
        if (damageTextPrefab == null)
        {
            Debug.LogError("[DamageTextManager] damageTextPrefab이 할당되지 않았습니다!");
            return;
        }

        // 위치 계산 (HP바 위 + 랜덤 X 오프셋)
        Vector3 spawnPos = worldPosition;
        spawnPos.y += heightOffset;
        spawnPos.x += Random.Range(-randomOffsetX, randomOffsetX);

        // 프리팹 생성
        GameObject textObj = Instantiate(damageTextPrefab, spawnPos, Quaternion.identity);

        // 카메라를 향하도록 회전 (빌보드)
        if (Camera.main != null)
        {
            textObj.transform.forward = Camera.main.transform.forward;
        }

        // 데미지 텍스트 설정
        DamageTextPopup popup = textObj.GetComponent<DamageTextPopup>();
        if (popup != null)
        {
            popup.Setup(damage, isCritical);
        }
    }
    #endregion
}
