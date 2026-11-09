/// <summary>
/// 공통 피해 수신 계약
/// </summary>
public interface IDamageable
{
    /// <summary>
    /// 피해 요청을 처리
    /// </summary>
    /// <param name="damageInfo">피해 정보</param>
    /// <returns>감소한 체력</returns>
    float TakeDamage ( DamageInfo damageInfo );
}
