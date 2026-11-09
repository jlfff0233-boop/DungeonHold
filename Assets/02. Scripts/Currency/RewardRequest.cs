/// <summary>
/// 보상 요청 - 원래 도전과 생명별 처치/단계 지급 정보
/// </summary>
public readonly struct RewardRequest
{
    /// <summary>
    /// 처치가 발생한 원래 도전 번호
    /// </summary>
    public int StageId { get; }

    /// <summary>
    /// 해당 적의 한 생명 식별값
    /// </summary>
    public long LifeId { get; }

    /// <summary>
    /// 처치 당시 콤보 처치 수
    /// </summary>
    public int ComboKillCount { get; }

    /// <summary>
    /// 처치 보상액
    /// </summary>
    public int KillAmount { get; }

    /// <summary>
    /// 해당 처치에서 예약한 단계 보상액
    /// </summary>
    public int ComboAmount { get; }

    /// <summary>
    /// 한 처치의 합산 지급액
    /// </summary>
    public int TotalAmount => KillAmount + ComboAmount;

    /// <summary>
    /// 보상 연결부가 검사한 지급 요청 생성
    /// </summary>
    /// <param name="stageId">원래 도전 번호</param>
    /// <param name="lifeId">생명 식별값</param>
    /// <param name="comboKillCount">처치 당시 콤보 처치 수</param>
    /// <param name="killAmount">처치 보상액</param>
    /// <param name="comboAmount">단계 보상액</param>
    public RewardRequest (
        int stageId, long lifeId, int comboKillCount,
        int killAmount, int comboAmount )
    {
        StageId = stageId;
        LifeId = lifeId;
        ComboKillCount = comboKillCount;
        KillAmount = killAmount;
        ComboAmount = comboAmount;
    }
}
