using UnityEngine;

/// <summary>
/// 런 리셋 매니저
/// - 사망 시 런 중 획득한 데이터를 초기화
/// - 별가루(영구 재화)만 유지
/// - DeathUI에서 거점 복귀 전에 호출
/// 
/// 호출 흐름:
///   PlayerStats.Die() → DeathUI.ShowDeathUI() → "서재로 귀환" 클릭
///   → RunManager.ResetRun() → SceneLoader.LoadScene("Base")
/// </summary>
public static class RunManager
{
    /// <summary>
    /// 런 데이터 전체 리셋
    /// - 골드, 레벨, 스킬, 디버프 초기화
    /// - 별가루는 유지
    /// - 씬 전환 전에 호출해야 함 (매니저들이 아직 살아있을 때)
    /// </summary>
    public static void ResetRun()
    {
        Debug.Log("[RunManager] ===== 런 데이터 리셋 시작 =====");

        // 1. 별가루 백업 (영구 재화)
        int savedStardust = 0;
        if (PlayerStats.Instance != null)
        {
            savedStardust = PlayerStats.Instance.Stardust;
            Debug.Log($"[RunManager] 별가루 보존: {savedStardust}");
        }

        // 2. 디버프 제거 (스탯 리셋보다 먼저 — 디버프가 스탯을 변경했을 수 있음)
        if (RewardManager.Instance != null)
        {
            RewardManager.Instance.ClearAllDebuffs();
        }

        // 3. 플레이어 스탯 초기화 (별가루 복원 포함)
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.ResetForNewRun(savedStardust);
        }

        // 4. 스킬 전부 해제
        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.ResetAllSkills();
        }

        // 4-1. 정적 백업 데이터도 클리어 — 다음 런에 이전 스킬·불씨가 남지 않도록.
        //      SkillManager가 씬 전환 시 슬롯을 정적 변수에 보존하므로, 새 런 시작 전 명시적 클리어 필수.
        SkillManager.ClearPersistedSlots();

        // 5. 적 리스트 정리 (static이라 씬 바뀌어도 남아있음)
        EnemyManager.ClearAll();

        // 6. 층 효과 초기화
        if (FloorEffectManager.Instance != null)
        {
            FloorEffectManager.Instance.ClearAllEffects();
        }

        Debug.Log("[RunManager] ===== 런 데이터 리셋 완료 =====");
    }
}
