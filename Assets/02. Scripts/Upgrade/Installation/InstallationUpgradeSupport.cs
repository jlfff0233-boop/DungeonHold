using System;

/// <summary>
/// 설치물 강화 지원 종류
/// </summary>
[Flags]
public enum InstallationUpgradeSupport
{
    None = 0,        //지원 없음
    Damage = 1,      //피해 증가
    Interval = 2,    //공격 주기 단축
    Slow = 4         //감속 증가
}
