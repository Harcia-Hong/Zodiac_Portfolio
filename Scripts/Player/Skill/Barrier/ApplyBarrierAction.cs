using UnityEngine;
using System.Collections;

/// <summary>
/// 베리어 부여 액션 — PlayerHealth에 베리어 부여.
/// 
/// 동작:
///   - 시전 사운드 재생
///   - PlayerHealth.ApplyBarrier(amount, duration, vfxPrefab) 호출
///   - 베리어 양은 damageOverride 우선 (SkillData 스탯 적용분 반영), 폴백은 barrierAmount
/// 
/// SkillAction 패턴 — 코루틴이지만 즉시 끝남 (지속 효과는 PlayerHealth가 관리).
/// </summary>
[CreateAssetMenu(menuName = "Skill/Action/Apply Barrier")]
public class ApplyBarrierAction : SkillAction
{
    [Header("베리어 설정")]
    [Tooltip("폴백 베리어 양. damageOverride > 0이면 그 값 우선 사용.")]
    public int barrierAmount = 100;

    [Tooltip("베리어 지속시간 (초). 1.5초 권장.")]
    public float duration = 1.5f;

    [Header("VFX")]
    [Tooltip("플레이어 자식으로 부착될 베리어 이펙트 프리팹.")]
    public GameObject vfxPrefab;

    [Header("사운드")]
    [Tooltip("시전 시 재생할 사운드.")]
    public AudioClip applySfx;

    public override IEnumerator Execute(GameObject user, int level, KeyCode key, float damageOverride = 0)
    {
        PlayerHealth playerHealth = user.GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            Debug.LogError("[ApplyBarrierAction] PlayerHealth를 찾을 수 없음");
            yield break;
        }

        // 베리어 양 결정 — damageOverride 우선 (스탯 보정 반영)
        int amount = (damageOverride > 0) ? Mathf.RoundToInt(damageOverride) : barrierAmount;

        // 시전 사운드
        if (applySfx != null && SoundManager.Instance != null)
            SoundManager.Instance.PlaySFX(applySfx);

        // 베리어 부여 — 지속 관리는 PlayerHealth가 담당
        playerHealth.ApplyBarrier(amount, duration, vfxPrefab);

        yield break;
    }
}
