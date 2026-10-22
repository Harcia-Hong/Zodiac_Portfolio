using UnityEngine;
using TMPro;
using DG.Tweening;

public class DamageText : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text text;

    [Header("Settings")]
    public float floatDistance = 1.5f;
    public float duration = 0.8f;

    [Header("Visual Style")]
    public Color normalColor = Color.white;
    public Color criticalColor = new Color(1f, 0.8f, 0f);

    [Header("Scale Settings")]
    public float normalScale = 1.0f;
    public float criticalScale = 1.5f;

    void Awake()
    {
        if (text == null)
            text = GetComponentInChildren<TMP_Text>();
    }

    public void ShowDamage(int damage, bool isCritical)
    {
        // 1. 텍스트 설정
        text.text = damage.ToString();

        // 2. 색상 및 크기 설정 (성장에 따른 데미지 증가 고려)
        if (isCritical)
        {
            text.color = criticalColor;
            text.fontSize = text.fontSize * 1.2f; // 폰트 사이즈 자체를 키우거나
            transform.localScale = Vector3.one * criticalScale; // 오브젝트 크기를 키움

            // 크리티컬은 더 강렬하게! (살짝 흔들리면서 등장)
            transform.DOShakePosition(0.3f, 0.5f, 20, 90, false, true);
        }
        else
        {
            text.color = normalColor;
            transform.localScale = Vector3.one * normalScale;
        }

        // 3. 연출 실행 (위로 뜨면서 + 투명해짐)
        Vector3 targetPos = transform.position + Vector3.up * floatDistance;

        // 이동 (Ease.OutBack을 쓰면 팅~ 하고 튀어오르는 느낌)
        transform.DOMove(targetPos, duration).SetEase(Ease.OutCubic);

        // 페이드 아웃
        text.DOFade(0, duration * 0.5f).SetDelay(duration * 0.5f); // 절반 지난 후부터 흐려짐

        // 4. 자동 삭제
        Destroy(gameObject, duration + 0.1f);
    }

    void Update()
    {
        if (Camera.main != null)
        {
            // 항상 카메라 정면을 바라보게 회전
            transform.LookAt(Camera.main.transform);
            transform.Rotate(0, 180f, 0); // 텍스트가 뒤집히는 경우 대응
        }
    }
}

/*
=== 사용법 ===
- 이 스크립트를 포함한 프리팹을 만들고, DamageText 오브젝트를 Instantiate하여 사용
- ShowDamage(int damage) 메서드를 호출하여 데미지를 전달
- 예: Instantiate(prefab, 위치, 회전).GetComponent<DamageText>().ShowDamage(100);

=== 특징 ===
- DOTween을 이용한 부드러운 이동 및 페이드 아웃 애니메이션
- 텍스트가 항상 카메라를 바라보도록 처리 (월드 공간 캔버스 기반)
- 일정 시간 후 자동 파괴

=== 개선 아이디어 ===
- 치명타일 경우 색상 변경 또는 크기 증가
- 텍스트에 랜덤한 회전, 흔들림 효과 추가
- 데미지 종류별로 색상 구분 (예: 화염, 독 등)
*/
