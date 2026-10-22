using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// 검 무기 이펙트 관리 클래스
/// 기본 공격(1타, 2타) 및 FlashSlash 이펙트 재생
/// VFX와 Prefab 모두 지원
/// </summary>
public class SwordEffect : MonoBehaviour, IEffect
{
    [Header("Sword Attack VFX")]
    [Tooltip("1타 공격 VFX")]
    public VisualEffect attack1VFX;

    [Tooltip("1타 공격 지면 VFX")]
    public VisualEffect attack1_groundVFX;

    [Header("FlashSlash Effect")]
    [Tooltip("FlashSlash VFX (Visual Effect 사용 시)")]
    public VisualEffect flashSlashVFX;

    [Tooltip("FlashSlash Prefab (Prefab 사용 시)")]
    public GameObject flashSlashPrefab;

    [Tooltip("FlashSlash 이펙트 생성 위치 오프셋")]
    public Vector3 flashSlashOffset = Vector3.zero;

    /// <summary>기본 공격 이펙트 재생</summary>
    /// <param name="comboIndex">콤보 인덱스 (0=1타, 1=2타)</param>
    public void PlayComboAttackEffect(int comboIndex)
    {
        switch (comboIndex)
        {
            case 0:
                PlayAttack1Effect();
                break;

            default:
                Debug.LogWarning($"[SwordEffect] 잘못된 콤보 인덱스: {comboIndex}");
                break;
        }
    }

    /// <summary>1타 공격 이펙트 재생</summary>
    private void PlayAttack1Effect()
    {
        if (attack1VFX != null)
        {
            attack1VFX.SendEvent("OnPlay");
            Debug.Log("[SwordEffect] 1타 이펙트 재생");
        }

        if (attack1_groundVFX != null)
        {
            attack1_groundVFX.SendEvent("OnPlay");
        }
    }

    /// <summary>FlashSlash 이펙트 재생 (VFX 또는 Prefab)</summary>
    public void PlayFlashSlashEffect()
    {
        // VFX가 할당되어 있으면 VFX 재생
        if (flashSlashVFX != null)
        {
            flashSlashVFX.SendEvent("OnPlay");
            Debug.Log("[SwordEffect] FlashSlash VFX 재생");
        }
        // Prefab이 할당되어 있으면 Prefab 인스턴스 생성
        else if (flashSlashPrefab != null)
        {
            SpawnFlashSlashPrefab();
        }
        else
        {
            Debug.LogWarning("[SwordEffect] FlashSlash 이펙트가 할당되지 않았습니다.");
        }
    }

    /// <summary>FlashSlash Prefab 생성 및 자동 삭제 처리</summary>
    private void SpawnFlashSlashPrefab()
    {
        // 이펙트 생성 위치 계산
        Vector3 spawnPosition = transform.position + transform.TransformDirection(flashSlashOffset);
        Quaternion spawnRotation = transform.rotation;

        // Prefab 인스턴스화
        GameObject effectInstance = Instantiate(flashSlashPrefab, spawnPosition, spawnRotation);

        Debug.Log("[SwordEffect] FlashSlash Prefab 생성");

        // 파티클 시스템 자동 삭제 처리
        ParticleSystem particleSystem = effectInstance.GetComponent<ParticleSystem>();
        if (particleSystem != null)
        {
            // 파티클 시스템의 duration에 맞춰 자동 삭제
            float destroyTime = particleSystem.main.duration + particleSystem.main.startLifetime.constantMax;
            Destroy(effectInstance, destroyTime);
        }
        else
        {
            // 파티클 시스템이 없으면 기본 3초 후 삭제
            Destroy(effectInstance, 3f);
            Debug.LogWarning("[SwordEffect] Prefab에 ParticleSystem이 없습니다. 3초 후 자동 삭제됩니다.");
        }
    }
}
