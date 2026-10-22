using UnityEngine;
using System.Collections;

/// <summary>
/// 스킬 시전 시 VFX 프리팹을 인스턴스화하는 액션.
/// 
/// 캐릭터 위치(+offset) / 캐릭터 회전(+rotationOffset) 기준으로 스폰됨.
/// VFX 프리팹의 정면(forward)이 -Z거나 90도 틀어진 경우 rotationOffset으로 보정.
/// 
/// 예: VFX가 뒤로 나가면 rotationOffset = (0, 180, 0)
///     VFX가 좌측으로 나가면 rotationOffset = (0, 90, 0)
/// </summary>
[CreateAssetMenu(menuName = "Skill/Action/Spawn Effect")]
public class SpawnEffectAction : SkillAction
{
    [Header("이펙트 설정")]
    public GameObject vfxPrefab;
    public float destroyDelay = 2.0f; // 2초 뒤 자동 삭제

    [Header("위치 보정")]
    [Tooltip("플레이어 기준 로컬 오프셋 (앞: +Z, 위: +Y, 우: +X).\n" +
             "캐릭터 회전이 적용된 후의 로컬 방향이므로 베기 방향 앞으로 띄우려면 Z를 양수로.")]
    public Vector3 offset = Vector3.zero;

    [Tooltip("플레이어 몸에 붙일지 여부.\n" +
             "버프형 이펙트(오라 등) ☑, 1회성 베기/폭발 ☐.")]
    public bool attachToPlayer = false;

    [Header("회전 보정 [NEW]")]
    [Tooltip("VFX 프리팹의 정면이 잘못 잡혀있을 때 보정용 오일러 각도(도 단위).\n" +
             "VFX가 뒤로 나가면 → Y = 180\n" +
             "VFX가 좌/우로 90도 틀어져 있으면 → Y = 90 또는 -90\n" +
             "정상이면 (0, 0, 0) 그대로 둠.")]
    public Vector3 rotationOffset = Vector3.zero;

    /// <summary>
    /// VFX 스폰 — 캐릭터 회전·위치를 기준으로 인스턴스화하고 destroyDelay 후 자동 파괴.
    /// offset과 rotationOffset은 캐릭터의 로컬 좌표/회전에 곱해져서 적용됨.
    /// </summary>
    public override IEnumerator Execute(GameObject user, int level, KeyCode key, float damageOverride = 0)
    {
        if (vfxPrefab == null) yield break;

        // 캐릭터 회전 + 추가 보정 = 최종 스폰 회전
        Quaternion spawnRot = user.transform.rotation * Quaternion.Euler(rotationOffset);

        // offset도 캐릭터의 로컬 좌표 기준으로 변환 — 캐릭터가 어느 방향을 보고 있어도 정확히 "앞쪽"에 뜨게
        Vector3 spawnPos = user.transform.position + user.transform.TransformDirection(offset);

        // 이펙트 생성
        GameObject instance = Instantiate(vfxPrefab, spawnPos, spawnRot);

        // 플레이어 몸에 붙일지(버프 등), 바닥에 남길지(폭발 등) 결정
        if (attachToPlayer)
        {
            instance.transform.SetParent(user.transform);
        }

        // 자동 삭제 예약
        Destroy(instance, destroyDelay);

        yield break; // 대기 없이 즉시 종료
    }
}
