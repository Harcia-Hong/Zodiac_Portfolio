using UnityEngine;

/// <summary>
/// RangedVisual 자식 GameObject의 Animator에 부착되어,
/// Animation Event를 부모 GameObject의 RangedCharacter로 위임 호출.
/// 
/// Animator와 RangedCharacter가 다른 GameObject에 있을 때 필요한 다리 역할.
/// (Animation Event는 Animator가 부착된 GameObject의 컴포넌트 메서드만 직접 호출 가능)
/// 
/// 부착 위치: RangedVisual 안의 Animator가 부착된 GameObject (같은 오브젝트).
/// </summary>
public class RangedAnimEventRelay : MonoBehaviour
{
    [Tooltip("위임 대상 RangedCharacter. 비워두면 부모에서 자동 탐색")]
    [SerializeField] private RangedCharacter target;

    /// <summary>참조 자동 보충 — 부모 GameObject 거슬러 올라가 RangedCharacter 탐색</summary>
    private void Awake()
    {
        if (target == null)
            target = GetComponentInParent<RangedCharacter>();

        if (target == null)
            Debug.LogError("[RangedAnimEventRelay] 부모에 RangedCharacter가 없음! " +
                           "PlayerRoot 자식 트리 안에 부착되어야 함.");
    }

    /// <summary>
    /// Animation Event 콜백 — RangedCharacter.OnTagWindowOpen으로 위임.
    /// 사격/스킬 애니메이션의 후딜 시작 프레임에 Function 이름을 'OnTagWindowOpen'으로 심을 것.
    /// </summary>
    public void OnTagWindowOpen()
    {
        if (target != null) target.OnTagWindowOpen();
    }

    /// <summary>
    /// Animation Event 콜백 — RangedCharacter.OnTagWindowClose로 위임.
    /// 사격/스킬 애니메이션의 후딜 종료 프레임에 Function 이름을 'OnTagWindowClose'로 심을 것.
    /// </summary>
    public void OnTagWindowClose()
    {
        if (target != null) target.OnTagWindowClose();
    }
}
