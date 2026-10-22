using UnityEngine;
using DG.Tweening;

/// <summary>
/// 자석형 루트 아이템
/// 스폰 후 퍼졌다가 플레이어에게 빨려들어가는 방식
/// </summary>
public class MagnetLoot : MonoBehaviour
{
    #region Enums
    /// <summary>
    /// 루트 타입 정의
    /// </summary>
    public enum LootType
    {
        Experience,     // 경험치
        Stardust,       // 별빛 조각
        Gold            // 골드
    }
    #endregion

    #region Settings
    [Header("루트 설정")]
    [SerializeField] private LootType lootType = LootType.Experience;
    [SerializeField] private int amount = 1;

    [Header("퍼짐 설정")]
    [SerializeField] private float spreadRadius = 2f;
    [SerializeField] private float spreadDuration = 0.25f;

    [Header("자석 이동 설정")]
    [SerializeField] private float moveSpeed = 15f;
    [SerializeField] private float acceleration = 30f;
    [SerializeField] private float collectDistance = 0.5f;

    [Header("비주얼")]
    [SerializeField] private Renderer visualRenderer;
    #endregion

    #region State
    private Transform player;
    private bool isSpreading = true;
    private bool isCollected = false;
    private float currentSpeed;
    private Vector3 spreadTargetPos;
    #endregion

    #region Properties
    public LootType Type => lootType;
    public int Amount => amount;
    #endregion

    #region Initialization
    /// <summary>
    /// 외부에서 호출하여 루트 초기화
    /// </summary>
    /// <param name="type">루트 타입</param>
    /// <param name="value">획득량</param>
    /// <param name="spawnPosition">스폰 위치</param>
    public void Initialize(LootType type, int value, Vector3 spawnPosition)
    {
        lootType = type;
        amount = value;
        transform.position = spawnPosition;

        // 플레이어 찾기
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }

        // 색상 설정
        ApplyColor();

        // 퍼짐 시작
        StartSpread();
    }
    #endregion

    #region Color Setting
    /// <summary>
    /// 루트 타입에 따른 색상 적용
    /// </summary>
    private void ApplyColor()
    {
        if (visualRenderer == null)
        {
            visualRenderer = GetComponentInChildren<Renderer>();
        }

        if (visualRenderer == null) return;

        Color lootColor = lootType switch
        {
            LootType.Experience => new Color(0.8f, 0.5f, 1f),    // 보라색 (임시)
            LootType.Stardust => new Color(0.4f, 0.8f, 1f),      // 밝은 푸른색
            LootType.Gold => new Color(1f, 0.85f, 0.2f),         // 금색
            _ => Color.white
        };

        // 머티리얼 색상 변경
        MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
        visualRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_Color", lootColor);
        propBlock.SetColor("_EmissionColor", lootColor * 2f);
        visualRenderer.SetPropertyBlock(propBlock);
    }
    #endregion

    #region Spread Phase
    /// <summary>
    /// 랜덤 방향으로 퍼지는 연출 시작
    /// </summary>
    private void StartSpread()
    {
        isSpreading = true;
        currentSpeed = 0f;

        // 랜덤 방향 계산 (XZ 평면)
        Vector2 randomDir = Random.insideUnitCircle.normalized;
        float randomDist = Random.Range(spreadRadius * 0.5f, spreadRadius);

        spreadTargetPos = transform.position + new Vector3(randomDir.x, 0f, randomDir.y) * randomDist;

        // DOTween으로 퍼짐 연출
        transform.DOMove(spreadTargetPos, spreadDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                isSpreading = false;
            });
    }
    #endregion

    #region Update - Magnet Movement
    private void Update()
    {
        // 퍼지는 중이거나 이미 수집됐으면 리턴
        if (isSpreading || isCollected) return;
        if (player == null) return;

        // 가속하며 플레이어에게 이동
        currentSpeed += acceleration * Time.deltaTime;
        currentSpeed = Mathf.Min(currentSpeed, moveSpeed);

        Vector3 direction = (player.position - transform.position).normalized;
        transform.position += direction * currentSpeed * Time.deltaTime;

        // 수집 거리 체크
        float distance = Vector3.Distance(transform.position, player.position);
        if (distance <= collectDistance)
        {
            Collect();
        }
    }
    #endregion

    #region Collection
    /// <summary>
    /// 플레이어에게 습득됨
    /// </summary>
    private void Collect()
    {
        if (isCollected) return;
        isCollected = true;

        // PlayerStats에 값 반영
        if (PlayerStats.Instance != null)
        {
            switch (lootType)
            {
                case LootType.Experience:
                    PlayerStats.Instance.GainEXP(amount);
                    break;
                case LootType.Stardust:
                    PlayerStats.Instance.AddStardust(amount);
                    break;
                case LootType.Gold:
                    PlayerStats.Instance.AddCoin(amount);
                    break;
            }
        }

        // TODO: 습득 이펙트, 사운드 재생

        // 오브젝트 삭제
        Destroy(gameObject);
    }
    #endregion

    #region Cleanup
    private void OnDestroy()
    {
        // DOTween 정리
        transform.DOKill();
    }
    #endregion
}
