using UnityEngine;

/// <summary>
/// 애니메이션 이벤트 핸들러
/// 애니메이션 클립에서 호출되는 이벤트를 받아 EffectGenerator에 전달
/// - 근거리 캐릭터(MeleeCharacter)가 비활성 상태면 이벤트 무시 (원거리 전환 시 검 이펙트 차단)
/// </summary>
public class AnimationEventHandler : MonoBehaviour
{
    [Header("References")]
    [Tooltip("EffectGenerator 참조")]
    public EffectGenerator effectGenerator;

    // 같은 GameObject의 MeleeCharacter 컴포넌트 (Player root)
    private MeleeCharacter meleeCharacter;

    /// <summary>초기화 — EffectGenerator + MeleeCharacter 자동 캐싱</summary>
    private void Awake()
    {
        if (effectGenerator == null)
        {
            effectGenerator = GetComponentInChildren<EffectGenerator>();
        }

        // 같은 GameObject(Player root)의 MeleeCharacter 캐싱
        meleeCharacter = GetComponent<MeleeCharacter>();

        if (effectGenerator == null)
        {
            Debug.LogError("[AnimationEventHandler] EffectGenerator를 찾을 수 없습니다!");
        }
    }

    /// <summary>
    /// 기본 공격 이펙트 재생 (애니메이션 이벤트에서 호출)
    /// 근거리 캐릭터가 비활성 상태면 무시 — 원거리 전환 시 검 이펙트 차단
    /// </summary>
    /// <param name="comboIndex">콤보 인덱스 (0=1타, 1=2타)</param>
    public void PlayComboAttackEffect(int comboIndex)
    {
        // 근거리 캐릭터가 비활성이면 검 이펙트 무시
        if (meleeCharacter != null && !meleeCharacter.IsActive)
        {
            return;
        }

        if (effectGenerator == null)
        {
            Debug.LogError("[AnimationEventHandler] EffectGenerator가 없습니다!");
            return;
        }

        IEffect swordEffect = effectGenerator.GetWeaponEffect("SwordEffects");

        if (swordEffect != null)
        {
            swordEffect.PlayComboAttackEffect(comboIndex);
            Debug.Log($"[AnimationEventHandler] {comboIndex + 1}타 공격 이펙트 재생");
        }
        else
        {
            Debug.LogWarning("[AnimationEventHandler] SwordEffects를 찾을 수 없습니다!");
        }
    }

    /// <summary>
    /// FlashSlash 이펙트 재생 (애니메이션 이벤트에서 호출)
    /// 근거리 캐릭터가 비활성 상태면 무시
    /// </summary>
    public void PlayFlashSlashEffect()
    {
        // 근거리 캐릭터가 비활성이면 무시
        if (meleeCharacter != null && !meleeCharacter.IsActive)
        {
            return;
        }

        if (effectGenerator == null)
        {
            Debug.LogError("[AnimationEventHandler] EffectGenerator가 없습니다!");
            return;
        }

        IEffect swordEffect = effectGenerator.GetWeaponEffect("SwordEffects");

        if (swordEffect != null)
        {
            swordEffect.PlayFlashSlashEffect();
            Debug.Log("[AnimationEventHandler] FlashSlash 이펙트 재생");
        }
        else
        {
            Debug.LogWarning("[AnimationEventHandler] SwordEffects를 찾을 수 없습니다!");
        }
    }
}
