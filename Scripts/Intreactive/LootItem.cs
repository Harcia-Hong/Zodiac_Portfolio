using UnityEngine;
using DG.Tweening;

public class LootItem : MonoBehaviour
{
    #region Enums
    public enum LootType { Coin, Material }
    #endregion

    #region Setting
    [Header("전리품 설정")]
    [SerializeField] private LootType lootType = LootType.Coin;
    [SerializeField] private int amount = 1;

    [Header("전리품 생성")]
    [SerializeField] private float dropDuration = 0.5f;
    [SerializeField] private float dropHeight = 1.5f;

    [Header("두둥실")]
    [SerializeField] private float floatHeight = 0.3f;
    [SerializeField] private float floatDuration = 1f;

    [Header("습득 자석")]
    [SerializeField] private float magnetRange = 3f;
    [SerializeField] private float magnetSpeed = 10f;
    [SerializeField] private float collectRange = 0.5f;
    #endregion

    #region State
    private bool isDropping = true;
    private bool isMagneting = false;
    private bool isCollected = false;

    private Transform player;
    private Tween floatTween;
    #endregion

    #region Properties
    public LootType Type => lootType;
    public int Amount    => amount;
    #endregion

    #region Initialization
    /// <summary>
    /// 전리품 초기화 및 드롭 시이작
    /// </summary>
    public void Initialize(Vector3 targetPos, LootType type, int amount)
    {
        this.lootType = type;
        this.amount = amount;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        StartDropAnimation(targetPos);
    }
    #endregion

    #region Drop Animation
    private void StartDropAnimation(Vector3 targetPos)
    {
        isDropping = true;

        Vector3 startPos = transform.position;

        // DOTween Sequence로 포물선 구현
        Sequence dropSequence = DOTween.Sequence();

        // X, Z는 직선 이동
        dropSequence.Append(
            transform.DOMove(targetPos, dropDuration)
                .SetEase(Ease.OutQuad)
        );

        // Y는 포물선 (Jump)
        dropSequence.Join(
            transform.DOMoveY(startPos.y + dropHeight, dropDuration * 0.5f)
                .SetEase(Ease.OutQuad)
        );
        dropSequence.Insert(dropDuration * 0.5f,
            transform.DOMoveY(targetPos.y, dropDuration * 0.5f)
                .SetEase(Ease.InQuad)
        );

        // 드롭 완료 후 둥실둥실 시작
        dropSequence.OnComplete(() =>
        {
            isDropping = false;
            StartFloating();
        });
    }
    #endregion

    #region Floating Animation
    private void StartFloating()
    {
        float baseY = transform.position.y;

        floatTween = transform.DOMoveY(baseY + floatHeight, floatDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }
    private void StopFloating()
    {
        if (floatTween != null && floatTween.IsActive())
        {
            floatTween.Kill();
        }
    }
    #endregion

    #region Magnet & Collect
    private void Update()
    {
        if (isDropping || isCollected) return;
        if (player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);

        // 자석 범위 안에 들어옴
        if (!isMagneting && distance <= magnetRange)
        {
            isMagneting = true;
            StopFloating();
        }

        // 자석 이동
        if (isMagneting)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                player.position,
                magnetSpeed * Time.deltaTime
            );

            // 습득 거리 도달
            if (distance <= collectRange)
            {
                Collect();
            }
        }
    }

    private void Collect()
    {
        if (isCollected) return;
        isCollected = true;

        if (PlayerStats.Instance != null)
        {
            switch (lootType)
            {
                case LootType.Coin:
                    PlayerStats.Instance.AddCoin(amount);
                    break;
                case LootType.Material:
                    PlayerStats.Instance.AddStardust(amount);
                    break;
            }
        }

        // TODO: 습득 이펙트 재생 (플레이어 위치)
        // TODO: 습득 사운드 재생

        // 오브젝트 삭제
        Destroy(gameObject);
    }
    #endregion

    #region Cleanup
    private void OnDestroy()
    {
        StopFloating();
    }
    #endregion
}
