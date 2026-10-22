using DG.Tweening;
using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Skill/Action/Dash")]
public class DashAction : SkillAction
{
    [Header("돌진 설정")]
    public float dashDistance = 10f;
    public float duration = 0.2f;
    public LayerMask obstacleLayer; // 벽 등
    public Ease dashEase = Ease.OutCubic;

    [Header("방향 설정")]
    public bool isBackStep = false;

    public override IEnumerator Execute(GameObject user, int level, KeyCode key, float damageOverride = 0)
    {
        var rb = user.GetComponent<Rigidbody>();
        var pc = user.GetComponent<PlayerController>();
        if (pc == null) yield break;

        // 1. 방향 결정
        Vector3 dir = pc.GetMouseDirection();

        // [신규] 백스텝이면 방향 반대로 뒤집기
        if (isBackStep)
        {
            dir = -dir;
        }

        // 2. 목표 지점 계산
        Vector3 startPos = user.transform.position;
        Vector3 targetPos = startPos + dir * dashDistance;

        if (Physics.Raycast(startPos, dir, out RaycastHit hit, dashDistance, obstacleLayer))
        {
            targetPos = hit.point - dir * 0.5f;
        }
        targetPos.y = startPos.y;

        user.transform.rotation = Quaternion.LookRotation(pc.GetMouseDirection());

        // 4. 이동 (DOTween)
        if (rb != null)
            yield return rb.DOMove(targetPos, duration).SetEase(dashEase).WaitForCompletion();
        else
            yield return user.transform.DOMove(targetPos, duration).SetEase(dashEase).WaitForCompletion();
    }
}
