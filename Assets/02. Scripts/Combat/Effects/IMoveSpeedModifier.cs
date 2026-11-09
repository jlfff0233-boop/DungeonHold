/// <summary>
/// 이동속도 배율 적용 계약
/// </summary>
public interface IMoveSpeedModifier
{
    /// <summary>
    /// 기본 이동속도에 적용할 배율 설정
    /// </summary>
    /// <param name="multiplier">최종 이동속도 배율</param>
    void SetMoveSpeedMultiplier ( float multiplier );
}
