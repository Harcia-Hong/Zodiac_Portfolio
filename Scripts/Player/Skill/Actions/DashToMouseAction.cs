using DG.Tweening;
using System.Collections;
using UnityEngine;

/// <summary>
/// 마우스 위치로 빠른 돌진 액션 — 신속하게 마우스 월드 위치까지 슬라이드.
/// 
/// 동작:
///   - 마우스 월드 위치(XZ 평면) 계산
///   - 최대 사거리/장애물 체크
///   - 캐릭터 회전 (마우스 방향)
///   - DOTween으로 빠르게 이동
/// 
/// 활용:
///   - 연속 찌르기 (시전 1단계 = 마우스 위치 돌진)
///   - 짧은 점프형 이동기 전반
/// 
/// 책임 밖:
///   - 이동 후 추가 동작 (찌르기 등) — 다음 액션이 처리
///   - 시전 중 이동 잠금 (SkillData.blockMovement)
/// </summary>
[CreateAssetMenu(menuName = "Skill/Action/Dash To Mouse")]
public class DashToMouseAction : SkillAction
{
    [Header("이동 설정")]
    [Tooltip("최대 사거리 (m). 마우스가 더 멀어도 여기까지만 이동.")]
    public float maxRange = 10f;

    [Tooltip("최소 거리 (m). 마우스가 너무 가까우면 무시. 0이면 무조건 이동.")]
    public float minRange = 0.5f;

    [Tooltip("이동 시간 (초). 짧을수록 슈웅~ 빠른 느낌.")]
    public float duration = 0.15f;

    [Tooltip("이동 곡선.")]
    public Ease dashEase = Ease.OutCubic;

    [Header("장애물")]
    [Tooltip("벽 레이어. 충돌 시 그 직전까지만 이동.")]
    public LayerMask obstacleLayer;

    [Header("사운드")]
    [Tooltip("돌진 시 재생.")]
    public AudioClip dashSfx;

    public override IEnumerator Execute(GameObject user, int level, KeyCode key, float damageOverride = 0)
    {
        PlayerController pc = user.GetComponent<PlayerController>();
        if (pc == null)
        {
            Debug.LogError("[DashToMouseAction] PlayerController 없음");
            yield break;
        }

        Vector3 startPos = user.transform.position;
        Vector3 mouseWorldPos = pc.GetMouseWorldPosition();

        // 마우스 위치를 XZ 평면으로 (Y는 캐릭터 높이 유지)
        Vector3 targetPos = new Vector3(mouseWorldPos.x, startPos.y, mouseWorldPos.z);

        Vector3 toTarget = targetPos - startPos;
        float distance = toTarget.magnitude;

        // 너무 가까우면 무시 (의미 없는 짧은 이동 방지)
        if (distance < minRange)
        {
            Debug.Log("[DashToMouseAction] 마우스 거리 minRange 미만 — 돌진 무시");
            yield break;
        }

        Vector3 direction = toTarget.normalized;

        // 사거리 제한
        if (distance > maxRange)
        {
            targetPos = startPos + direction * maxRange;
            distance = maxRange;
        }

        // 장애물 체크 — 벽 만나면 직전까지만
        if (Physics.Raycast(startPos, direction, out RaycastHit hit, distance, obstacleLayer))
        {
            targetPos = hit.point - direction * 0.5f;
            targetPos.y = startPos.y;
        }

        // 캐릭터 회전 (마우스 방향)
        user.transform.rotation = Quaternion.LookRotation(direction);

        // 사운드
        if (dashSfx != null && SoundManager.Instance != null)
            SoundManager.Instance.PlaySFX(dashSfx);

        // 이동 — Rigidbody 있으면 그걸로, 없으면 transform
        Rigidbody rb = user.GetComponent<Rigidbody>();
        if (rb != null)
            yield return rb.DOMove(targetPos, duration).SetEase(dashEase).WaitForCompletion();
        else
            yield return user.transform.DOMove(targetPos, duration).SetEase(dashEase).WaitForCompletion();
    }
}
