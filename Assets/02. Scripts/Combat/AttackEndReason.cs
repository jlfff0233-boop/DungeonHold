/// <summary>
/// 공격 종료 사유
/// </summary>
public enum AttackEndReason
{
    Completed,    //정상 종료
    Canceled,     //공격 취소
    Died,         //사망
    Disabled      //비활성화
}
