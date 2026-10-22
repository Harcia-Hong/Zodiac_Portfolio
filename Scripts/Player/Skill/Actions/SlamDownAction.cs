using UnityEngine;
using System.Collections;
using DG.Tweening;

[CreateAssetMenu(menuName = "Skill/Action/Slam Down")]
public class SlamDownAction : SkillAction
{
    [Header("낙하 설정")]
    [Tooltip("바닥까지 찍는 시간 (작을수록 빠름)")]
    public float slamDuration = 0.15f;

    [Tooltip("바닥 인식 레이어")]
    public LayerMask groundLayer;

    [Tooltip("낙하 가속도 느낌 (InQuad 추천: 점점 빠르게)")]
    public Ease slamEase = Ease.InQuad;

    public override IEnumerator Execute(GameObject user, int level, KeyCode key, float damageOverride = 0)
    {
        var tr = user.transform;
        var rb = user.GetComponent<Rigidbody>();

        // 1. 목표 지점(바닥) 계산
        Vector3 startPos = tr.position;
        Vector3 targetPos = startPos + Vector3.down * 50f; // 못 찾을 경우를 대비한 기본값

        if (Physics.Raycast(startPos + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 100f, groundLayer))
        {
            targetPos = hit.point;
        }

        // 2. 목표 자세(회전) 계산: 똑바로 서기
        // 현재 보는 방향(Y축 회전)은 유지하되, X/Z축 기울기를 0으로 만듭니다.
        Quaternion targetRotation = Quaternion.Euler(0f, tr.eulerAngles.y, 0f);

        // 3. 물리 간섭 방지 (선택 사항: 필요하면 주석 해제)
        // if (rb != null) rb.isKinematic = true;

        // 4. 이동과 회전을 동시에 실행 (Sequence 활용)
        Sequence slamSequence = DOTween.Sequence();

        // 4-1. 위치 이동
        if (rb != null)
            slamSequence.Join(rb.DOMove(targetPos, slamDuration).SetEase(slamEase));
        else
            slamSequence.Join(tr.DOMove(targetPos, slamDuration).SetEase(slamEase));

        // 4-2. 회전 보정 (시작하자마자 팍! 하고 자세 잡기)
        float rotateDuration = slamDuration * 0.1f;
        if (rb != null)
        {
            // [수정] Rigidbody는 DORotate(Vector3)를 사용해야 합니다.
            slamSequence.Join(rb.DORotate(targetRotation.eulerAngles, rotateDuration).SetEase(Ease.OutQuad));
        }
        else
        {
            // Transform은 DORotateQuaternion 사용 가능
            slamSequence.Join(tr.DORotateQuaternion(targetRotation, rotateDuration).SetEase(Ease.OutQuad));
        }

        // 5. 대기
        yield return slamSequence.WaitForCompletion();

        // 6. 물리 복구 (선택 사항)
        // if (rb != null) rb.isKinematic = false;

        // 확실하게 자세 고정
        tr.rotation = targetRotation;
    }
}
