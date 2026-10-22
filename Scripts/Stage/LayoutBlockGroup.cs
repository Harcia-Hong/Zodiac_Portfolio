using System.Collections.Generic;
using UnityEngine;

/// ============================================================
/// LayoutBlockGroup.cs — Layout 단위 블록 묶음 + 공간 쿼리
/// ============================================================
/// 한 Layout(예: Layout_0)의 루트 GameObject에 부착되어,
/// 그 Layout에 속한 모든 변형 가능 블록(BlockUnit)을 모아 관리한다.
///
/// 핵심 역할:
///   1. 자식 BlockUnit 자동 수집 (또는 인스펙터 수동 등록)
///   2. 플레이어 발 밑 블록 검출 (Raycast 우선 + Bounds 폴백)
///   3. Visible / Hidden 블록 필터링
///   4. 변형 후보 목록 제공 (안전 블록 제외)
///
/// 사용 흐름:
///   Layout_X (이 컴포넌트 부착)
///     ├─ LayoutSpawnData (몬스터/보상 스폰 포인트)
///     ├─ LayoutZone (진입 감지)
///     └─ Block_0, Block_1, ... (각각 BlockUnit 부착)
///
///   BlockTransformManager
///     → GetBlockUnderPlayer(playerTransform)로 안전 블록 식별
///     → GetTransformableBlocksExcluding(safeBlock)로 변형 후보 조회
///     → 패턴에 따라 BlockUnit.RaiseUp / SinkDown 호출
///
/// 셋업:
///   - 인스펙터에서 blocks 배열을 비워두면 Awake에서 자식 BlockUnit 자동 수집
///   - 비워두지 않으면 인스펙터 등록 그대로 사용 (수동 제어)
/// ============================================================
public class LayoutBlockGroup : MonoBehaviour
{
    // ============================================================
    // 인스펙터 설정
    // ============================================================

    [Header("블록 수집")]
    [Tooltip("이 Layout에 속한 변형 가능 블록 목록.\n" +
             "비워두면 Awake에서 자식 BlockUnit을 자동 수집함.\n" +
             "수동 등록 시에는 자동 수집을 건너뜀")]
    [SerializeField] private BlockUnit[] blocks;

    [Header("자동 수집 옵션")]
    [Tooltip("blocks 배열이 비어있을 때 자식에서 BlockUnit을 자동으로 찾을지")]
    [SerializeField] private bool autoCollectChildren = true;

    [Tooltip("자동 수집 시 비활성 자식도 포함할지")]
    [SerializeField] private bool includeInactive = true;

    [Header("플레이어 발 밑 검출")]
    [Tooltip("플레이어 위치에서 아래로 쏠 Raycast의 최대 거리")]
    [SerializeField] private float playerCheckRayDistance = 50f;

    [Tooltip("Raycast가 검사할 레이어. 기본은 Default 레이어. " +
             "블록 콜라이더가 다른 레이어에 있다면 여기에 포함시킬 것")]
    [SerializeField] private LayerMask blockLayerMask = ~0; // 기본: 모든 레이어

    // ============================================================
    // 런타임 상태
    // ============================================================

    /// <summary>현재 등록된 블록 개수 (디버깅용)</summary>
    [Header("디버깅용")]
    [SerializeField] private int registeredBlockCount;

    /// <summary>매번 새로 할당하지 않기 위한 임시 작업 리스트 (필터링 결과용)</summary>
    private readonly List<BlockUnit> workBuffer = new List<BlockUnit>();

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    #region Unity Lifecycle

    private void Awake()
    {
        // 인스펙터에 등록된 게 없으면 자동 수집
        if ((blocks == null || blocks.Length == 0) && autoCollectChildren)
            CollectChildBlocks();

        registeredBlockCount = blocks != null ? blocks.Length : 0;
    }

    /// <summary>자식 GameObject들에서 BlockUnit 컴포넌트를 자동 수집</summary>
    private void CollectChildBlocks()
    {
        BlockUnit[] found = GetComponentsInChildren<BlockUnit>(includeInactive);
        blocks = found;

        Debug.Log($"[LayoutBlockGroup] {gameObject.name}에서 BlockUnit {found.Length}개 자동 수집");
    }

    #endregion

    // ============================================================
    // 외부 접근용 프로퍼티
    // ============================================================

    /// <summary>등록된 모든 블록 개수</summary>
    public int BlockCount => blocks != null ? blocks.Length : 0;

    /// <summary>등록된 블록이 있는지</summary>
    public bool HasBlocks => BlockCount > 0;

    // ============================================================
    // 블록 조회
    // ============================================================

    #region Block Queries

    /// <summary>등록된 모든 블록 배열 반환 (직접 수정 금지)</summary>
    public BlockUnit[] GetAllBlocks() => blocks;

    /// <summary>
    /// 현재 Visible 상태인 블록 리스트 반환.
    /// 내부 버퍼를 재사용하므로 호출자는 결과를 즉시 사용해야 함
    /// (다른 LayoutBlockGroup 메서드 호출 시 덮어씌워짐).
    /// </summary>
    public List<BlockUnit> GetVisibleBlocks()
    {
        workBuffer.Clear();
        if (blocks == null) return workBuffer;

        foreach (BlockUnit block in blocks)
        {
            if (block != null && block.IsVisible)
                workBuffer.Add(block);
        }

        return workBuffer;
    }

    /// <summary>
    /// 현재 Hidden 상태인 블록 리스트 반환.
    /// 주의: GetVisibleBlocks와 동일한 내부 버퍼를 공유함.
    /// </summary>
    public List<BlockUnit> GetHiddenBlocks()
    {
        workBuffer.Clear();
        if (blocks == null) return workBuffer;

        foreach (BlockUnit block in blocks)
        {
            if (block != null && block.IsHidden)
                workBuffer.Add(block);
        }

        return workBuffer;
    }

    /// <summary>
    /// 안전 블록(예: 플레이어 발 밑)을 제외한 모든 블록을 새 List에 담아 반환.
    /// 결과는 호출자 소유의 새 리스트이므로 자유롭게 수정 가능.
    /// </summary>
    public List<BlockUnit> GetTransformableBlocksExcluding(BlockUnit excludeBlock)
    {
        List<BlockUnit> result = new List<BlockUnit>();
        if (blocks == null) return result;

        foreach (BlockUnit block in blocks)
        {
            if (block == null) continue;
            if (block == excludeBlock) continue;

            result.Add(block);
        }

        return result;
    }

    #endregion

    // ============================================================
    // 플레이어 발 밑 검출
    // ============================================================

    #region Player Foot Detection

    /// <summary>
    /// 플레이어가 현재 서 있는 블록을 반환.
    /// 1순위: 플레이어 위치에서 아래로 Raycast → Hit한 콜라이더의 BlockUnit
    /// 2순위(폴백): 모든 블록 순회 → IsPointAboveXZ 검사 (가장 가까운 표면 선택)
    /// 어느 쪽도 실패하면 null.
    /// </summary>
    public BlockUnit GetBlockUnderPlayer(Transform player)
    {
        if (player == null) return null;

        // 1순위 — Raycast
        BlockUnit byRay = GetBlockByRaycast(player.position);
        if (byRay != null) return byRay;

        // 2순위(폴백) — Bounds XZ 검사
        return GetBlockByBoundsCheck(player.position);
    }

    /// <summary>플레이어 위치에서 아래로 Raycast하여 블록 검출</summary>
    private BlockUnit GetBlockByRaycast(Vector3 playerPos)
    {
        // 플레이어가 살짝 떠있을 수 있으므로 약간 위에서 시작
        Vector3 origin = playerPos + Vector3.up * 1f;

        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                             playerCheckRayDistance, blockLayerMask, QueryTriggerInteraction.Ignore))
            return null;

        // 콜라이더에 직접 또는 부모에 BlockUnit이 붙어있는지 확인
        BlockUnit unit = hit.collider.GetComponentInParent<BlockUnit>();
        if (unit == null) return null;

        // 검출된 블록이 이 그룹 소속인지 확인 (다른 Layout의 블록일 수도 있음)
        return IsOwnedByThisGroup(unit) ? unit : null;
    }

    /// <summary>
    /// 모든 블록을 순회하면서 플레이어 위치(XZ)가 블록 영역 안에 있는지 검사.
    /// 여러 블록과 겹치는 경우 가장 높은 표면을 가진 블록을 선택.
    /// </summary>
    private BlockUnit GetBlockByBoundsCheck(Vector3 playerPos)
    {
        if (blocks == null) return null;

        BlockUnit best = null;
        float bestSurfaceY = float.NegativeInfinity;

        foreach (BlockUnit block in blocks)
        {
            if (block == null) continue;
            if (!block.IsVisible) continue;       // 가라앉은 블록은 후보 제외
            if (!block.IsPointAboveXZ(playerPos)) continue;

            // 더 높은 표면이 있다면 갱신 (블록이 겹쳐있을 때 위쪽 우선)
            if (block.SurfaceY > bestSurfaceY)
            {
                bestSurfaceY = block.SurfaceY;
                best = block;
            }
        }

        return best;
    }

    /// <summary>주어진 BlockUnit이 이 그룹의 소속인지 확인</summary>
    private bool IsOwnedByThisGroup(BlockUnit unit)
    {
        if (blocks == null) return false;

        foreach (BlockUnit b in blocks)
        {
            if (b == unit) return true;
        }

        return false;
    }

    #endregion

    // ============================================================
    // 일괄 제어 — 디버그/리셋용
    // ============================================================

    #region Bulk Control

    /// <summary>모든 블록을 즉시 Visible 상태로 강제 설정 (애니메이션 없음)</summary>
    public void ResetAllBlocksToVisible()
    {
        if (blocks == null) return;

        foreach (BlockUnit block in blocks)
        {
            if (block != null)
                block.SetVisibleImmediate();
        }
    }

    /// <summary>모든 블록을 즉시 Hidden 상태로 강제 설정 (애니메이션 없음)</summary>
    public void HideAllBlocksImmediate()
    {
        if (blocks == null) return;

        foreach (BlockUnit block in blocks)
        {
            if (block != null)
                block.SetHiddenImmediate();
        }
    }

    #endregion

    // ============================================================
    // 디버그
    // ============================================================

    #region Debug

    [ContextMenu("자식 BlockUnit 재수집")]
    private void DebugReCollect()
    {
        CollectChildBlocks();
        registeredBlockCount = blocks != null ? blocks.Length : 0;
    }

    [ContextMenu("모든 블록 Visible로 리셋")]
    private void DebugResetVisible() => ResetAllBlocksToVisible();

    [ContextMenu("모든 블록 Hidden으로 강제")]
    private void DebugHideAll() => HideAllBlocksImmediate();

    private void OnDrawGizmosSelected()
    {
        if (blocks == null) return;

        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.3f);
        foreach (BlockUnit block in blocks)
        {
            if (block == null) continue;
            Gizmos.DrawLine(transform.position + Vector3.up * 0.5f,
                            block.transform.position + Vector3.up * 0.5f);
        }
    }

    #endregion
}
