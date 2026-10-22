/// <summary>
/// 무기 이펙트 인터페이스
/// 모든 무기 이펙트 클래스가 구현해야 하는 메서드 정의
/// </summary>
public interface IEffect
{
    /// <summary>기본 공격 이펙트 재생 (콤보 카운트에 따라)</summary>
    void PlayComboAttackEffect(int comboIndex);

    /// <summary>우클릭 공격 이펙트 재생 (FlashSlash)</summary>
    void PlayFlashSlashEffect();
}
