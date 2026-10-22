using System.Collections;
using UnityEngine;

/// <summary>
/// 콤보 스킬용 단계별 애니메이션 트리거 액션.
/// 
/// SkillManager.GetCurrentComboStage(key)로 지금 실행 중인 콤보 단계 인덱스를 받아와
/// 그에 대응하는 애니메이션 트리거를 발동시킴.
/// 
/// 특징:
/// - SO 1개로 모든 단계 커버 (triggersPerStage 배열에 순서대로 트리거 이름만 채워두면 됨)
/// - SetTrigger 후 즉시 yield break — Dash/Damage 같은 후속 액션을 블로킹하지 않음
///   → 베기 모션과 대시 이동이 동시에 진행되어 자연스러운 연출
/// - (옵션) 마우스 방향 회전도 같이 처리 가능
/// 
/// 사용 예 (분노 연참):
///   triggersPerStage = ["Combo1", "Combo2", "Combo3"]
///   1단 실행 시 → Combo1 트리거
///   2단 실행 시 → Combo2 트리거
///   3단 실행 시 → Combo3 트리거
/// </summary>
[CreateAssetMenu(menuName = "Skill/Action/Combo Anim")]
public class ComboAnimAction : SkillAction
{
    [Header("단계별 트리거")]
    [Tooltip("콤보 단계별 애니메이터 트리거 이름.\n" +
             "인덱스 0 = 1단, 1 = 2단, 2 = 3단 ...\n" +
             "비어있는 슬롯은 무시됨 (해당 단계에서 트리거 발동 안 함).")]
    public string[] triggersPerStage;

    [Header("회전 설정")]
    [Tooltip("애니메이션 발동 직전에 캐릭터를 마우스 방향으로 회전시킬지 여부.\n" +
             "Dash 액션이 이미 회전을 처리하지만, ComboAnim을 Dash 앞에 두면 베기 모션 시작 시점부터 방향이 맞춰져 더 자연스러움.")]
    public bool rotateToMouse = true;

    [Header("애니메이션 속도 (선택)")]
    [Tooltip("0보다 크면 애니메이터 speed를 이 값으로 강제 설정.\n" +
             "0이면 현재 speed 그대로 사용. PlayerStats의 공격속도 반영하려면 useAttackSpeed 옵션 사용.")]
    public float forcedAnimSpeed = 0f;

    [Tooltip("체크하면 PlayerStats.AttackSpeed에 비례해 애니메이션 속도가 자동 조정됨.\n" +
             "forcedAnimSpeed보다 우선순위 낮음 (forcedAnimSpeed가 0일 때만 적용).")]
    public bool useAttackSpeed = false;

    [Header("단계별 SFX (선택)")]
    [Tooltip("콤보 단계별 효과음.\n" +
         "인덱스 0 = 1단, 1 = 2단, 2 = 3단 ...\n" +
         "triggersPerStage와 같은 순서. 비어있는 슬롯은 무음.")]
    public AudioClip[] sfxPerStage;

    /// <summary>
    /// 액션 실행 — 현재 콤보 단계에 맞는 트리거 발동 + (옵션) 마우스 회전.
    /// 블로킹 없이 즉시 yield break하므로 후속 액션과 동시 진행 가능.
    /// </summary>
    public override IEnumerator Execute(GameObject user, int level, KeyCode key, float damageOverride = 0)
    {
        // 1. 현재 콤보 단계 인덱스 가져오기 (SkillManager에 없거나 콤보 미진행이면 0 = 1단)
        int stageIdx = 0;
        if (SkillManager.Instance != null)
        {
            stageIdx = SkillManager.Instance.GetCurrentComboStage(key);
        }

        // 2. 마우스 방향 회전 (옵션)
        if (rotateToMouse)
        {
            PlayerController pc = user.GetComponent<PlayerController>();
            if (pc != null) pc.RotateToMouseDirection();
        }

        // 2.5 단계별 SFX 재생 (베기 발동 순간 — 클립 지정된 단계만)
        if (sfxPerStage != null && stageIdx >= 0 && stageIdx < sfxPerStage.Length)
        {
            AudioClip clip = sfxPerStage[stageIdx];
            if (clip != null && SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySFX(clip);
            }
        }

        // 3. 단계별 트리거 발동
        if (triggersPerStage != null && stageIdx >= 0 && stageIdx < triggersPerStage.Length)
        {
            string triggerName = triggersPerStage[stageIdx];

            if (!string.IsNullOrEmpty(triggerName))
            {
                Animator anim = user.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    // 애니 속도 처리 — forcedAnimSpeed > useAttackSpeed > 그대로 두기 순
                    if (forcedAnimSpeed > 0f)
                    {
                        anim.speed = forcedAnimSpeed;
                    }
                    else if (useAttackSpeed && PlayerStats.Instance != null)
                    {
                        anim.speed = PlayerStats.Instance.AttackSpeed;
                    }

                    anim.SetTrigger(triggerName);
                }
                else
                {
                    Debug.LogWarning($"[ComboAnimAction] Animator를 찾을 수 없음 — {user.name}");
                }
            }
        }
        else
        {
            Debug.LogWarning($"[ComboAnimAction] 단계 {stageIdx}에 해당하는 트리거가 triggersPerStage에 없음.");
        }

        // 블로킹 없이 즉시 종료 — 후속 액션과 동시 진행
        yield break;
    }
}
