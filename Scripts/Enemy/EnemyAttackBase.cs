// EnemyAttackBase.cs — 모든 적 공격 클래스의 공통 부모 (추상 클래스)

using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 적 공격 클래스의 공통 베이스 (추상 클래스)
///
/// [역할]
/// - 모든 적 공격 클래스가 반복적으로 쓰던 코드(LookAtPlayer, NavMesh 제어 등)를 한 곳에 모음
/// - IEnemyAttack 인터페이스의 공통 부분을 미리 구현해 자식 클래스의 보일러플레이트 제거
/// - DRY 원칙: 같은 코드를 여러 클래스에 중복 작성하지 않음
///
/// [사용법]
/// 1. 새 공격 클래스를 만들 때 이 클래스를 상속받음
/// 2. abstract 메서드(StartAttack, GetAttackRange, GetCooldownTime)는 반드시 구현
/// 3. virtual 메서드(StopAttack 등)는 필요할 때만 override
/// 4. Awake/OnDisable을 override할 경우 반드시 base 호출 필수
///
/// [기존 코드 호환성]
/// - 기존 Enemy_A_Attack, Enemy_Charge_Attack 등은 이 클래스 사용 안 해도 동작함
/// - 점진적으로 마이그레이션 가능 (강제 아님)
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public abstract class EnemyAttackBase : MonoBehaviour, IEnemyAttack
{
    // =========================================================
    // 공통 컴포넌트 참조 (자식 클래스에서 protected로 접근)
    // =========================================================

    /// <summary>플레이어 트랜스폼 — Awake에서 "Player" 태그로 자동 캐싱</summary>
    protected Transform player;

    /// <summary>NavMeshAgent — 추적/이동 제어용</summary>
    protected NavMeshAgent nav;

    /// <summary>Animator — 자식 오브젝트에서 자동 캐싱</summary>
    protected Animator anim;

    /// <summary>Enemy 본체 — HP, EnemySO 데이터 접근용</summary>
    protected Enemy enemy;

    // =========================================================
    // 공통 상태
    // =========================================================

    /// <summary>현재 공격 중인지 여부 (자식 클래스에서 직접 set 가능)</summary>
    protected bool isAttacking = false;

    // =========================================================
    // 초기화 / 정리
    // =========================================================

    /// <summary>
    /// 컴포넌트 자동 캐싱
    /// 자식 클래스에서 추가 초기화가 필요하면 override 후 반드시 base.Awake() 호출
    /// </summary>
    protected virtual void Awake()
    {
        nav = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();
        enemy = GetComponent<Enemy>();

        // 플레이어 캐싱 (씬에 Player 태그 오브젝트가 있어야 함)
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
    }

    /// <summary>
    /// 오브젝트 비활성화 시 안전한 정리
    /// 자식 클래스에서 override 시 반드시 base.OnDisable() 호출
    /// </summary>
    protected virtual void OnDisable()
    {
        StopAllCoroutines();
        isAttacking = false;
    }

    // =========================================================
    // IEnemyAttack — 공통 구현 부분
    // =========================================================

    /// <summary>
    /// 현재 공격 중인지 반환 (FSM 상태 전환 판단용)
    /// </summary>
    public virtual bool IsAttacking() => isAttacking;

    /// <summary>
    /// 공격 시작 — 자식 클래스에서 반드시 구현
    /// 각 적마다 공격 패턴이 다르므로 abstract로 강제
    /// </summary>
    public abstract void StartAttack();

    /// <summary>
    /// 공격 중단 — 기본 구현 제공 (자식이 override 가능)
    /// 피격, 사망, 상태 전환 등 외부 요인으로 공격이 끊길 때 호출됨
    /// </summary>
    public virtual void StopAttack()
    {
        if (!isAttacking) return;

        StopAllCoroutines();
        isAttacking = false;

        // 안전하게 NavMesh 복구 (공격 중 NavMesh를 껐을 수 있으므로)
        RestoreNavMesh();
    }

    /// <summary>
    /// 공격 사거리 반환 — 자식 클래스에서 반드시 구현
    /// </summary>
    public abstract float GetAttackRange();

    /// <summary>
    /// 공격 쿨타임 반환 — 자식 클래스에서 반드시 구현
    /// </summary>
    public abstract float GetCooldownTime();

    // =========================================================
    // 공통 유틸리티 — 플레이어 관련
    // =========================================================

    /// <summary>
    /// 플레이어 방향으로 즉시 회전 (Y축 무시 — 수평 회전만)
    /// 공격 시작 직전, 플레이어를 정확히 바라보게 할 때 사용
    /// </summary>
    protected void LookAtPlayer()
    {
        if (player == null) return;

        Vector3 dir = player.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    /// <summary>
    /// 플레이어와의 수평 거리 계산 (XZ 평면 기준)
    /// 높이 차이를 무시하므로 점프/낙하 중인 플레이어에 대해서도 일관된 거리 산출
    /// </summary>
    /// <returns>플레이어가 null이면 float.MaxValue (안전값)</returns>
    protected float GetDistanceToPlayer()
    {
        if (player == null) return float.MaxValue;

        Vector3 selfXZ = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 playerXZ = new Vector3(player.position.x, 0f, player.position.z);
        return Vector3.Distance(selfXZ, playerXZ);
    }

    /// <summary>
    /// 플레이어가 공격 가능한 유효 상태인지 확인 (존재 + 살아있음)
    /// 공격 직전에 체크해서 이미 죽은 플레이어에게 헛스윙하지 않도록 방지
    /// </summary>
    protected bool IsPlayerValid()
    {
        if (player == null) return false;

        PlayerHealth hp = player.GetComponent<PlayerHealth>();
        if (hp == null) return true; // PlayerHealth가 없는 환경(테스트 등)에선 통과

        return !hp.isDead;
    }

    // =========================================================
    // 공통 유틸리티 — NavMesh 제어
    // =========================================================

    /// <summary>
    /// NavMesh를 비활성화 (직접 위치 제어 모드로 전환)
    /// Root Motion 점프 / DOTween 이동 / 돌진 공격 등 NavMesh와 충돌하는 이동 시 사용
    /// </summary>
    protected void DisableNavMesh()
    {
        if (nav != null && nav.enabled)
            nav.enabled = false;
    }

    /// <summary>
    /// NavMesh를 활성화 + 현재 위치로 동기화 (Warp)
    /// Warp 누락 시 이전 NavMesh 위치로 순간이동하는 버그가 발생하므로 반드시 함께 호출
    /// </summary>
    protected void RestoreNavMesh()
    {
        if (nav != null && !nav.enabled)
        {
            nav.enabled = true;
            nav.Warp(transform.position);
        }
    }
}
