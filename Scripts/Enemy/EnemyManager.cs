using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬 내 모든 Enemy를 추적하는 전역 관리자
/// - Enemy.Start()에서 Register, CleanupOnDeath()에서 Unregister 호출
/// - 씬 전환/런 리셋 시 ClearAll()로 리스트 정리
/// </summary>
public static class EnemyManager
{
    private static List<Enemy> enemies = new List<Enemy>();

    /// <summary>
    /// 적 등록 (Enemy.Start에서 호출)
    /// </summary>
    public static void Register(Enemy enemy)
    {
        if (!enemies.Contains(enemy))
            enemies.Add(enemy);
    }

    /// <summary>
    /// 적 해제 (Enemy 사망/파괴 시 호출)
    /// </summary>
    public static void Unregister(Enemy enemy)
    {
        enemies.Remove(enemy);
    }

    /// <summary>
    /// 현재 등록된 모든 적 반환
    /// </summary>
    public static List<Enemy> GetAllEnemies()
    {
        // null 참조 자동 정리 (씬 전환 시 파괴된 오브젝트 대비)
        enemies.RemoveAll(e => e == null);
        return enemies;
    }

    /// <summary>
    /// 리스트 전체 초기화 (런 리셋/씬 전환 시 RunManager에서 호출)
    /// </summary>
    public static void ClearAll()
    {
        enemies.Clear();
        Debug.Log("[EnemyManager] 적 리스트 전체 초기화");
    }
}