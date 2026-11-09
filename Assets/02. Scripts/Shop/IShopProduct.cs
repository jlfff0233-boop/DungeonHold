/// <summary>
/// 상점 상품
/// </summary>
public interface IShopProduct
{
    /// <summary>
    /// 상품 ID
    /// </summary>
    string ProductId { get; }

    /// <summary>
    /// 기본 가격
    /// </summary>
    int Price { get; }
}