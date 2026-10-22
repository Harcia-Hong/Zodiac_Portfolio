// SuicideTelegraph.cs — 자폭 전용 단순 Telegraph (외곽선 + Fill 원 차오름)

using UnityEngine;
using DG.Tweening;

/// <summary>
/// 자폭 공격 전용 단순 Telegraph 컴포넌트
///
/// [동작]
/// - 외곽선 원: 즉시 최대 반경으로 표시 (정적, 회피 가능 범위 알림)
/// - Fill 원: 0에서 최대 반경까지 차오름 (시간 시각화)
/// - 둘 다 같은 위치 + 같은 최종 크기 → 폭발 범위 정확하게 매칭
///
/// [AttackTelegraph와의 차이]
/// - 자폭에 특화 (radius와 duration만 받으면 됨)
/// - 외곽선과 Fill의 최종 크기가 코드로 강제 동기화 (prefab 크기 차이 무관)
///
/// [의존성]
/// - DOTween (DOTween Pro)
/// </summary>
public class SuicideTelegraph : MonoBehaviour
{
    // =========================================================
    // 설정
    // =========================================================

    [Header("Telegraph Prefabs")]
    [SerializeField, Tooltip("외곽선용 prefab — 즉시 최대 반경으로 표시 (선택, 없어도 동작)")]
    private GameObject borderPrefab;

    [SerializeField, Tooltip("Fill용 prefab — 점점 차오르며 시간 시각화")]
    private GameObject fillPrefab;

    [Header("표시 설정")]
    [SerializeField, Tooltip("바닥에 붙도록 Y축 오프셋 (Z-fighting 방지)")]
    private float groundOffset = 0.05f;

    [SerializeField, Tooltip("Fill 차오를 때 사용할 트윈 이징")]
    private Ease scaleEase = Ease.Linear;

    // =========================================================
    // 내부 상태
    // =========================================================

    /// <summary>현재 표시 중인 외곽선 인스턴스</summary>
    private GameObject currentBorder;

    /// <summary>현재 표시 중인 Fill 인스턴스</summary>
    private GameObject currentFill;

    /// <summary>Fill의 Scale 트윈 (외부 취소 시 안전 정리용)</summary>
    private Tween currentTween;

    // =========================================================
    // 공개 API
    // =========================================================

    /// <summary>
    /// Telegraph 표시 시작
    /// 외곽선은 즉시 최대 크기로 / Fill은 0에서 최대 크기까지 duration 동안 차오름
    /// </summary>
    /// <param name="center">표시 중심 위치</param>
    /// <param name="radius">최대 반경 (= 폭발 범위)</param>
    /// <param name="duration">Fill이 차오르는 데 걸리는 시간 (= 카운트다운)</param>
    public void Show(Vector3 center, float radius, float duration)
    {
        // 이전 Telegraph가 남아있으면 즉시 정리 (중복 표시 방지)
        Cancel();

        // 1. 위치 계산 (바닥에서 살짝 띄움)
        Vector3 spawnPos = new Vector3(center.x, center.y + groundOffset, center.z);

        // 2. 최종 스케일 = radius * 2 (Quad 1x1 메쉬 기준 — 반경 보이려면 지름 = radius * 2)
        Vector3 targetScale = Vector3.one * (radius * 2f);

        // 3. 외곽선 생성 (즉시 최대 크기, 정적) — prefab 슬롯이 비어있으면 스킵
        if (borderPrefab != null)
        {
            currentBorder = Instantiate(borderPrefab, spawnPos, borderPrefab.transform.rotation);
            currentBorder.transform.localScale = targetScale;
        }

        // 4. Fill 생성 (0 → 최대 크기로 차오름) — Fill은 필수
        if (fillPrefab != null)
        {
            currentFill = Instantiate(fillPrefab, spawnPos, fillPrefab.transform.rotation);
            currentFill.transform.localScale = Vector3.zero; // 0부터 시작

            // DOTween으로 Scale 애니메이션
            // OnComplete에서 Fill만 자동 파괴 (외곽선은 Cancel/OnDisable에서 일괄 정리)
            currentTween = currentFill.transform
                .DOScale(targetScale, duration)
                .SetEase(scaleEase)
                .OnComplete(() =>
                {
                    // Fill 차오름 완료 시점 — 외곽선과 Fill 모두 정리
                    Cancel();
                });
        }
        else
        {
            Debug.LogWarning($"[{name}] Fill Prefab이 설정되지 않았습니다.");
        }
    }

    /// <summary>
    /// 현재 표시 중인 모든 Telegraph를 즉시 취소 + 정리
    /// 자폭 도중 죽었을 때 안전하게 정리하기 위함
    /// </summary>
    public void Cancel()
    {
        // 트윈 먼저 정지 (OnComplete 콜백 무한루프 방지)
        if (currentTween != null)
        {
            currentTween.Kill();
            currentTween = null;
        }

        // 외곽선 + Fill 둘 다 안전하게 파괴
        if (currentBorder != null)
        {
            Destroy(currentBorder);
            currentBorder = null;
        }

        if (currentFill != null)
        {
            Destroy(currentFill);
            currentFill = null;
        }
    }

    /// <summary>
    /// 컴포넌트 비활성화/파괴 시 안전 정리
    /// 적이 풀에 반환되거나 씬 이동 시 트윈/오브젝트 누수 방지
    /// </summary>
    private void OnDisable()
    {
        Cancel();
    }
}
