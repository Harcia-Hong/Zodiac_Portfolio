using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무기 이펙트 중앙 관리 클래스
/// Dictionary로 무기별 이펙트를 관리하고 제공
/// </summary>
public class EffectGenerator : MonoBehaviour
{
    [Header("Weapon Effects")]
    [Tooltip("무기 이름을 key로, IEffect 구현체를 value로 저장")]
    public Dictionary<string, IEffect> weaponEffects = new Dictionary<string, IEffect>();

    /// <summary>초기화 - 자식 오브젝트에서 IEffect 찾아서 등록</summary>
    private void Awake()
    {
        RegisterWeaponEffects();
    }

    /// <summary>자식 오브젝트에서 IEffect를 찾아 Dictionary에 등록</summary>
    private void RegisterWeaponEffects()
    {
        // 자식 오브젝트들에서 IEffect 구현체 찾기
        IEffect[] effects = GetComponentsInChildren<IEffect>();

        foreach (var effect in effects)
        {
            // MonoBehaviour로 캐스팅하여 게임오브젝트 이름 가져오기
            if (effect is MonoBehaviour mono)
            {
                string weaponName = mono.gameObject.name;

                if (!weaponEffects.ContainsKey(weaponName))
                {
                    weaponEffects.Add(weaponName, effect);
                    Debug.Log($"[EffectGenerator] 무기 이펙트 등록: {weaponName}");
                }
            }
        }

        Debug.Log($"[EffectGenerator] 총 {weaponEffects.Count}개 무기 이펙트 등록 완료");
    }

    /// <summary>특정 무기의 이펙트 가져오기</summary>
    /// <param name="weaponName">무기 이름</param>
    /// <returns>해당 무기의 IEffect, 없으면 null</returns>
    public IEffect GetWeaponEffect(string weaponName)
    {
        if (weaponEffects.TryGetValue(weaponName, out IEffect effect))
        {
            return effect;
        }

        Debug.LogWarning($"[EffectGenerator] '{weaponName}' 무기 이펙트를 찾을 수 없습니다.");
        return null;
    }
}
