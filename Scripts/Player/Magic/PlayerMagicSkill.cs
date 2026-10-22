/*using System.Collections;
using UnityEngine;

/// <summary>
/// 마법 스킬 시스템 (임시 스텁 - Week 2에서 본격 구현 예정)
/// IWeaponSkill 인터페이스 구현으로 WeaponManager와 연동
/// </summary>
public class PlayerMagicSkill : MonoBehaviour
{
    [Header("Magic System (임시)")]
    public int maxMana = 100;
    public int currentMana = 100;
    public float manaRegenRate = 10f; // 초당 마나 회복

    [Header("Q : No Name")]
    public float qSkillManaCost = 25f;
    public float qSkillCooldown = 3f;
    public float qSkillCastTime = 1.0f;
    public int qSkillDamage = 35;

    [Header("E : No Naaaame")]
    public float eSkillManaCost = 40f;
    public float eSkillCooldown = 6f;
    public float eSkillCastTime = 1.5f;
    public int eSkillDamage = 50;

    [Header("Animation Trigger")]
    public string qSkillTrigger = "Magic_Q_Cast";
    public string eSkillTrigger = "Magic_E_Cast";
    public string Idle = "Magic_Idle";

    [Header("VFX & Sound ( 추후 구현 )")]
    public GameObject castingVFXPrefab;
    public GameObject QVFXPrefab;
    public GameObject EVFXPrefab;
    public AudioClip castingSound;
    public AudioClip spellSound;

    // 상태 열거
    public enum MagicState { Ready, Casting, Executing, Cooldown }

    // 상태 관리
    private MagicState qSkillState = MagicState.Ready;
    private MagicState eSkillState = MagicState.Ready;
    private bool isQSkillReady = true;
    private bool isESkillReady = true;
    private float qCooldownTimer = 0f;
    private float eCooldownTimer = 0f;

    // 캐스팅 코루틴 참조
    private Coroutine qCastingCoroutine;
    private Coroutine eCastingCoroutine;

    // 컴포넌트 참조
    private PlayerController playerController;
    private Animator anim;

    /// <summary>초기화</summary>
    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        anim = GetComponentInChildren<Animator>();
    }

    /// <summary>업데이트</summary>
    private void Update()
    {
        if (enabled) // 활성화된 경우에만
        {
            HandleCooldowns();
        }
    }

    /// <summary>쿨다운 처리</summary>
    private void HandleCooldowns()
    {
        // Q스킬 쿨다운
        if (!isQSkillReady && qSkillState == MagicState.Cooldown)
        {
            qCooldownTimer += Time.deltaTime;
            if (qCooldownTimer >= qSkillCooldown)
            {
                isQSkillReady = true;
                qSkillState = MagicState.Ready;
                qCooldownTimer = 0f;
            }
        }

        // E스킬 쿨다운
        if (!isESkillReady && eSkillState == MagicState.Cooldown)
        {
            eCooldownTimer += Time.deltaTime;
            if (eCooldownTimer >= eSkillCooldown)
            {
                isESkillReady = true;
                eCooldownTimer = 0f;
            }
        }
    }

    // =============================================================================
    // IWeaponSkill 인터페이스 구현
    // =============================================================================

    /// <summary>Q스킬: 파이어볼 (임시 구현)</summary>
    public void UseQSkill()
    {
        if (!CanUseQSkill()) return;

        // 코루틴 시작
        qCastingCoroutine = StartCoroutine(CastQRoutine());
    }

    /// <summary>E스킬: 아이스 스파이크 (임시 구현)</summary>
    public void UseESkill()
    {
        if (!CanUseESkill()) return;

        // 코루틴 시이작
        eCastingCoroutine = StartCoroutine(CastERoutine());
    }

    /// <summary>스킬 활성화</summary>
    public void ActivateSkills()
    {
        enabled = true;
        Debug.Log("[PlayerMagicSkill] 마법 스킬 활성화");

        // 마나바 UI 표시 등 (추후 구현)
    }

    /// <summary>스킬 비활성화</summary>
    public void DeactivateSkills()
    {
        enabled = false;

        // 진행중인 캐스팅 전면 중단
        CancelAllCasting();

        Debug.Log("[PlayerMagicSkill] 마법 스킬 비활성화");

        // 마나바 UI 숨김 등 (추후 구현)
    }

    /// <summary>쿨다운 상태 반환</summary>
    public object GetCooldownStatus()
    {
        return new
        {
            qReady = isQSkillReady,
            eReady = isESkillReady,
            qTime = qCooldownTimer,
            eTime = eCooldownTimer,
            qState = qSkillState,
            eState = eSkillState,
            currentMana = currentMana,
            maxMana = maxMana
        };
    }

    // Q스킬 캐스팅 시스템
    /// <summary>Q스킬 캐스팅 루틴</summary>
    private IEnumerator CastQRoutine()
    {
        // 캐스팅 시작
        qSkillState = MagicState.Casting;
        playerController.isSkillCasting = true;

        // anim 재생
        if (anim != null)
        {
            anim.SetTrigger(qSkillTrigger);
        }

        if (magicIndicator != null)
        {
            magicIndicator.ShowQSkillIndicator();
        }

        // 캐스팅 시간 대기
        float elapsed = 0f;
        while (elapsed < qSkillCastTime)
        {
            if (Input.GetButtonDown("Dash"))
            {
                CancelQSkillCasting();
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        ExecuteQSkill();
    }

    private void ExecuteQSkill()
    {
        qSkillState = MagicState.Executing;

        if (magicIndicator != null)
        {
            magicIndicator.HideQSkillIndicator();
        }

        // 스킬 완료 처리
        CompleteQSkill();
    }

    private void CompleteQSkill()
    {
        // 쿨타임 시작
        qSkillState = MagicState.Cooldown;
        isQSkillReady = false;
        qCooldownTimer = 0f;

        // 스킬 캐스팅 동안 제한된 움직임 헤제
        playerController.isSkillCasting = false;

        // UI 쿨타임 시작
        CombatUIManager.Instance?.StartSkillCoolDown(0, qSkillCooldown);
    }

    private void CancelQSkillCasting()
    {
        if (qCastingCoroutine != null)
        {
            StopCoroutine(qCastingCoroutine);
            qCastingCoroutine = null;
        }

        // 캔슬 시 부여되는 패널티 -> 쿨타임 절반 적용
        qSkillState = MagicState.Cooldown;
        isQSkillReady = false;
        qCooldownTimer = 0f;

        // 취소 했으니 플레이어 움직임 제한 해제
        playerController.isSkillCasting = false;

        if (magicIndicator != null)
        {
            magicIndicator.HideQSkillIndicator();
        }

        // UI 쿨타임 시작
        CombatUIManager.Instance?.StartSkillCoolDown(0, qCooldownTimer * 0.5f);
    }

    // E스킬 캐스팅 시스템

    /// <summary>E스킬 캐스팅 루틴</summary>
    private IEnumerator CastERoutine()
    {
        // 캐스팅 시이작
        eSkillState = MagicState.Casting;
        playerController.isSkillCasting = true;

        // anim 재생
        if (anim != null)
        {
            anim.SetTrigger(eSkillTrigger);
        }

        if (magicIndicator != null)
        {
            Vector3 targetPos = playerController.GetMouseWorldPosition();
            magicIndicator.ShowESkillIndicator(targetPos);
        }

        // 캐스팅 시간 대기
        float elapsed = 0f;
        while (elapsed < eSkillCastTime)
        {
            // Dodge 시 캔슬
            if (Input.GetButtonDown("Dash"))
            {
                CancelESkillCasting();
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        ExecuteESkill();
    }

    /// <summary>E 스킬 발동</summary>
    private void ExecuteESkill()
    {
        eSkillState = MagicState.Executing;

        // 마나 소모
        currentMana -= Mathf.RoundToInt(eSkillManaCost);

        if (magicIndicator != null)
        {
            magicIndicator.HideESkillIndicator();
        }

        // 스킬 완료
        CompleteESkill();
    }

    /// <summary>E 스킬 완료</summary>
    private void CompleteESkill()
    {
        // 쿨타임 시작
        eSkillState = MagicState.Cooldown;
        isESkillReady = false;
        eCooldownTimer = 0f;

        // 플레이어 움직임 제한 해제
        playerController.isSkillCasting = false;

        // UI 쿨타임 시작
        CombatUIManager.Instance?.StartSkillCoolDown(1, eSkillCooldown);
    }

    private void CancelESkillCasting()
    {
        if(eCastingCoroutine != null)
        {
            StopCoroutine(eCastingCoroutine);
            eCastingCoroutine = null;
        }

        // 캔슬에 대한 패널티 -> 쿨타임 절반
        eSkillState = MagicState.Cooldown;
        isESkillReady = false;
        eCooldownTimer = 0f;

        // 플레이어 움직임 해제
        playerController.isSkillCasting = false;

        if (magicIndicator != null)
        {
            magicIndicator.HideESkillIndicator();
        }

        // UI 쿨타임 절반 적용
        CombatUIManager.Instance?.StartSkillCoolDown(1, eSkillCooldown * 0.5f);
    }

    // 조건 체크 및 유틸리티

    /// <summary>Q스킬 사용 가능 여부</summary>
    private bool CanUseQSkill()
    {
        if (qSkillState != MagicState.Ready)
        {
            Debug.Log($"[PlayerMagicSkill] Q스킬 상태: {qSkillState}");
            return false;
        }

        if (!isQSkillReady)
        {
            Debug.Log("[PlayerMagicSkill] Q스킬 쿨다운 중");
            return false;
        }

        if (currentMana < qSkillManaCost)
        {
            Debug.Log("[PlayerMagicSkill] 마나 부족");
            return false;
        }

        if (playerController?.isDodging == true)
        {
            Debug.Log("[PlayerMagicSkill] 회피 중 스킬 사용 불가");
            return false;
        }

        if (playerController?.isSkillCasting == true)
        {
            Debug.Log("[PlayerMagicSkill] 다른 스킬 캐스팅 중");
            return false;
        }

        if (playerMana == null || !playerMana.TryConsumeMana(0))
        {
            Debug.Log("[PlayerMagicSkill] 마나 부족");
            return false;
        }

        return true;
    }

    /// <summary>E스킬 사용 가능 여부</summary>
    private bool CanUseESkill()
    {
        if (eSkillState != MagicState.Ready)
        {
            Debug.Log($"[PlayerMagicSkill] E스킬 상태: {eSkillState}");
            return false;
        }

        if (!isESkillReady)
        {
            Debug.Log("[PlayerMagicSkill] E스킬 쿨다운 중");
            return false;
        }

        if (currentMana < eSkillManaCost)
        {
            Debug.Log("[PlayerMagicSkill] 마나 부족");
            return false;
        }

        if (playerController?.isDodging == true)
        {
            Debug.Log("[PlayerMagicSkill] 회피 중 스킬 사용 불가");
            return false;
        }

        if (playerController?.isSkillCasting == true)
        {
            Debug.Log("[PlayerMagicSkill] 다른 스킬 캐스팅 중");
            return false;
        }

        return true;
    }

    /// <summary>모든 캐스팅 중단</summary>
    private void CancelAllCasting()
    {
        if (qSkillState == MagicState.Casting)
        {
            CancelQSkillCasting();
        }

        if (eSkillState == MagicState.Casting)
        {
            CancelESkillCasting();
        }
    }

    // =============================================================================
    // 공개 인터페이스 (마법 전용)
    // =============================================================================

    /// <summary>현재 마나 반환</summary>
    public int GetCurrentMana() => currentMana;

    /// <summary>최대 마나 반환</summary>
    public int GetMaxMana() => maxMana;

    /// <summary>마나 비율 반환 (0.0 ~ 1.0)</summary>
    public float GetManaRatio() => (float)currentMana / maxMana;

    /// <summary>마나 강제 설정 (테스트용)</summary>
    public void SetMana(int amount)
    {
        currentMana = Mathf.Clamp(amount, 0, maxMana);
    }

    /// <summary>현재 캐스팅 중인지 확인</summary>
    public bool IsCasting()
    {
        return qSkillState == MagicState.Casting ||
               eSkillState == MagicState.Casting;
    }

    // =============================================================================
    // 디버그
    // =============================================================================

    /// <summary>현재 마법 스킬 상태 로그</summary>
    [ContextMenu("마법 스킬 상태 확인")]
    public void LogMagicSkillStatus()
    {
        Debug.Log("=== 마법 스킬 상태 ===");
        Debug.Log($"마나: {currentMana}/{maxMana} ({GetManaRatio():P0})");
        Debug.Log($"Q스킬 상태: {qSkillState} (준비: {isQSkillReady})");
        Debug.Log($"E스킬 상태: {eSkillState} (준비: {isESkillReady})");
        Debug.Log($"플레이어 캐스팅 플래그: {playerController?.isSkillCasting}");
        Debug.Log($"현재 캐스팅 중: {IsCasting()}");
    }
}
*/
