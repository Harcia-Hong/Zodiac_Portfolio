using UnityEngine;

/// <summary>
/// Enemy 등급별 시각 효과 (All In 1 3D-Shader 기반)
/// - Normal: 효과 없음
/// - Elite: 노란색 Outline
/// - Epic/Boss: 별도 프리팹에서 미리 세팅
/// </summary>
public class EnemyVisualModifier : MonoBehaviour
{
    #region Serialized Fields
    [Header("Elite Outline 설정")]
    [SerializeField] private Color eliteOutlineColor = new Color(1f, 0.8f, 0.2f, 1f);
    [SerializeField] private float eliteOutlineWidth = 0.02f;
    #endregion

    #region Private Fields
    private Enemy enemy;
    private Renderer[] renderers;
    private MaterialPropertyBlock propertyBlock;

    // All In 1 3D-Shader Outline 프로퍼티 ID
    private static readonly int OutlineColorID = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthID = Shader.PropertyToID("_OutlineWidth");
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        renderers = GetComponentsInChildren<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
    }

    private void Start()
    {
        if (enemy != null)
        {
            ApplyRankVisual(enemy.enemyRank);
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 등급에 따른 시각 효과 적용
    /// </summary>
    public void ApplyRankVisual(EnemyRank rank)
    {
        switch (rank)
        {
            case EnemyRank.Normal:
                DisableOutline();
                break;

            case EnemyRank.Elite:
                ApplyOutline(eliteOutlineColor, eliteOutlineWidth);
                break;

            case EnemyRank.Epic:
            case EnemyRank.Boss:
                // 별도 프리팹에서 미리 세팅됨
                break;
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Outline 적용
    /// </summary>
    private void ApplyOutline(Color color, float width)
    {
        foreach (var rend in renderers)
        {
            if (rend == null) continue;

            rend.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(OutlineColorID, color);
            propertyBlock.SetFloat(OutlineWidthID, width);
            rend.SetPropertyBlock(propertyBlock);
        }
    }

    /// <summary>
    /// Outline 비활성화 (Normal)
    /// </summary>
    private void DisableOutline()
    {
        foreach (var rend in renderers)
        {
            if (rend == null) continue;

            rend.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(OutlineWidthID, 0f);
            rend.SetPropertyBlock(propertyBlock);
        }
    }
    #endregion
}
