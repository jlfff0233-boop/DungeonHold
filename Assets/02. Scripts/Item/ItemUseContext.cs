/// <summary>
/// 아이템 사용 대상 - 효과 적용에 필요한 모듈 전달
/// </summary>
public class ItemUseContext
{
    Health _health;     //효과를 적용할 체력 모듈
    Energy _energy;     //효과를 적용할 에너지 모듈

    /// <summary>
    /// 체력 모듈
    /// </summary>
    public Health Health => _health;

    /// <summary>
    /// 에너지 모듈
    /// </summary>
    public Energy Energy => _energy;

    /// <summary>
    /// 사용 대상의 모듈 연결
    /// </summary>
    /// <param name="health">체력 모듈</param>
    /// <param name="energy">에너지 모듈</param>
    public ItemUseContext ( Health health , Energy energy )
    {
        _health = health;
        _energy = energy;
    }
}
