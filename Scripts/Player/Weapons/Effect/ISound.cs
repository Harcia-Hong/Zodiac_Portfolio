/// <summary>
/// 무기 사운드 인터페이스
/// 모든 무기 사운드 클래스가 구현해야 하는 메서드 정의
/// </summary>
public interface ISound
{
    /// <summary>기본 공격 사운드 재생 (콤보 카운트에 따라)</summary>
    void PlayComboAttackSound(int comboIndex);

    /// <summary>우클릭 공격 사운드 재생</summary>
    void PlayFlashSlashSound();
}
