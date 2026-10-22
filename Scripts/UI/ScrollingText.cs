using UnityEngine;

/// <summary>
/// UI 요소(Image, Text 등)를 지정된 방향으로 무한 스크롤시키는 컴포넌트.
/// 메인 메뉴의 상/하단 띠, 우측 세로 띠 등에 부착하여 흐르는 텍스트 연출에 사용.
/// </summary>
public class ScrollingText : MonoBehaviour
{
    /// <summary>스크롤 진행 방향</summary>
    public enum ScrollDirection
    {
        Left,    // 우 → 좌 (가장 일반적, 영문 흐름과 자연스러움)
        Right,   // 좌 → 우
        Up,      // 하 → 상
        Down     // 상 → 하
    }

    [Header("스크롤 설정")]
    [Tooltip("스크롤 진행 방향")]
    [SerializeField] private ScrollDirection direction = ScrollDirection.Left;

    [Tooltip("초당 이동 픽셀 (속도)")]
    [SerializeField] private float speed = 50f;

    [Header("루프 범위")]
    [Tooltip("이 위치(픽셀)에 도달하면 시작 위치로 리셋됨")]
    [SerializeField] private float resetThreshold = -2000f;

    [Tooltip("리셋 후 시작할 위치(픽셀)")]
    [SerializeField] private float startPosition = 2000f;

    // 매 프레임 GetComponent 호출을 피하기 위한 캐싱
    private RectTransform rectTransform;

    /// <summary>
    /// 자신의 RectTransform을 캐싱하여 Update 성능 최적화
    /// </summary>
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    /// <summary>
    /// 매 프레임 지정된 방향으로 anchoredPosition을 이동시키고,
    /// 임계값 도달 시 반대편 시작 위치로 워프하여 무한 루프 구현
    /// </summary>
    private void Update()
    {
        Vector2 pos = rectTransform.anchoredPosition;
        float delta = speed * Time.deltaTime;

        switch (direction)
        {
            case ScrollDirection.Left:
                pos.x -= delta;
                if (pos.x <= resetThreshold) pos.x = startPosition;
                break;

            case ScrollDirection.Right:
                pos.x += delta;
                if (pos.x >= startPosition) pos.x = resetThreshold;
                break;

            case ScrollDirection.Up:
                pos.y += delta;
                if (pos.y >= startPosition) pos.y = resetThreshold;
                break;

            case ScrollDirection.Down:
                pos.y -= delta;
                if (pos.y <= resetThreshold) pos.y = startPosition;
                break;
        }

        rectTransform.anchoredPosition = pos;
    }
}
