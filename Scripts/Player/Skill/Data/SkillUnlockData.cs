using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스킬 1개의 해금 조건 데이터
/// - 어떤 스킬인지, 해금 비용, 기본 해금 여부를 정의
/// </summary>
[System.Serializable]
public class SkillUnlockEntry
{
    [Tooltip("해금 대상 스킬")]
    public SkillData skill;

    [Tooltip("해금에 필요한 별가루")]
    public int stardustCost;

    [Tooltip("true면 처음부터 해금 (기본 스킬용)")]
    public bool unlockedByDefault;
}

/// <summary>
/// 전체 스킬 해금 테이블
/// - 게임에 존재하는 모든 스킬의 해금 조건을 한 곳에서 관리
/// - 에디터에서 스킬별 해금 비용, 기본 해금 여부를 설정
/// </summary>
[CreateAssetMenu(fileName = "SkillUnlockData", menuName = "Skill System/Skill Unlock Data")]
public class SkillUnlockData : ScriptableObject
{
    [Header("스킬 해금 목록")]
    [Tooltip("게임에 존재하는 모든 스킬의 해금 조건")]
    public List<SkillUnlockEntry> unlockEntries = new List<SkillUnlockEntry>();
}