using UnityEngine;
using System.Collections;

[CreateAssetMenu(menuName = "Skill/Action/Teleport To Target")]
public class TeleportToTargetAction : SkillAction
{
    [Header("타겟팅 설정")]
    public float maxRange = 15f;
    public LayerMask enemyLayer;

    [Header("이동 설정")]
    public float heightOffset = 5.0f;

    public override bool CanExecute(GameObject user)
    {
        return CheckTarget(user, out _); // 타겟이 있으면 true, 없으면 false
    }

    public override IEnumerator Execute(GameObject user, int level, KeyCode key, float damageOverride = 0)
    {
        if (CheckTarget(user, out RaycastHit hit))
        {
            Vector3 targetPos = hit.transform.position;
            Vector3 airPos = targetPos + Vector3.up * heightOffset;

            // [핵심 수정] 물리적인 힘(관성)을 완전히 제거하고 이동해야 깔끔합니다.
            Rigidbody rb = user.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // 1. 관성 제거 (가던 힘 멈추기)
                // Unity 6버전 기준 linearVelocity, 구버전이면 velocity 사용
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                // 2. 물리 연산 잠시 끄기 (충돌이나 끼임 방지)
                rb.isKinematic = true;

                // 3. 위치 이동
                user.transform.position = airPos;

                // 4. 즉시 물리 위치 동기화 (움찔거림 방지 핵심!)
                Physics.SyncTransforms();

                // 5. 물리 다시 켜기
                rb.isKinematic = false;
            }
            else
            {
                user.transform.position = airPos;
            }

            // 시선 처리
            user.transform.LookAt(targetPos);

            Debug.Log($"[Teleport] {hit.collider.name} 기습 이동 완료 (관성 제거됨)");
        }
        else
        {
            var manager = user.GetComponent<SkillManager>();
            if (manager != null) manager.isSequenceBroken = true;
        }

        yield break;
    }

    private bool CheckTarget(GameObject user, out RaycastHit hitInfo)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        // 1. 적 감지
        if (Physics.Raycast(ray, out hitInfo, 100f, enemyLayer))
        {
            // 2. 거리 체크
            float dist = Vector3.Distance(user.transform.position, hitInfo.transform.position);
            if (dist <= maxRange)
            {
                return true; // 타겟 있고 거리도 맞음 -> OK
            }
        }

        hitInfo = new RaycastHit(); // 빈 값
        return false; // 타겟 없음 -> NO
    }
}
