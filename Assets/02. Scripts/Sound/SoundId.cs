/// <summary>
/// 사운드 식별자
/// </summary>
public enum SoundId
{
    None = 0, //미사용
    TitleBgm = 1, //타이틀 배경음
    PlayBgm = 2, //플레이 배경음
    UiClick = 10, //선택
    UiBack = 11, //뒤로
    HeroFootstep = 20, //플레이어 발소리
    CompanionFootstep = 21, //동료 발소리
    HeroJump = 22, //점프
    HeroDodge = 23, //회피
    HeroHurt = 24, //플레이어 피격
    HeroDeath = 25, //플레이어 사망
    ItemUse = 26, //아이템 사용
    CompanionHurt = 27, //동료 피격
    CompanionDeath = 28, //동료 사망
    SwordSwing = 100, //검 기본 공격
    SwordSkill = 101, //검 스킬
    SwordUltimate = 102, //검 궁극기
    BowLaunch = 110, //활 발사
    BowImpact = 111, //화살 충돌
    BowSkillLaunch = 112, //관통 화살 발사
    BowSkillImpact = 113, //관통 화살 충돌
    BowUltimate = 114, //화살비 생성
    StaffLaunch = 120, //마법탄 발사
    StaffImpact = 121, //마법탄 폭발
    StaffSkillLaunch = 122, //강화 마법탄 발사
    StaffSkillImpact = 123, //강화 마법탄 폭발
    StaffUltimate = 124, //불기둥 생성
    WaveStart = 200, //전투 구간 시작
    StageClear = 201, //승리
    StageFail = 202, //패배
    BowUltimateTick = 210, //화살비 주기 타격
    StaffUltimateTick = 211, //불기둥 주기 타격
    HealthItemUse = 220, //체력 아이템 전용 사용음
    EnergyItemUse = 221, //에너지 아이템 전용 사용음
    HeroRevive = 222, //플레이어 부활
    WaveComplete = 230, //웨이브 완료
    UiNotice = 240, //일반 알림
    UiReject = 241, //요청 거절
    UiEquip = 242, //장비 선택
    UiPage = 243, //페이지 이동
    ShopPurchase = 250, //구매 성공
    UpgradeSuccess = 251, //강화 성공
    CurrencyReward = 252, //재화 보상
    ComboReward = 253, //연속 처치 보상
    MissionReward = 254, //미션 보상
    CompanionHire = 260, //동료 고용
    CompanionPlace = 261, //동료 배치
    CompanionSpawn = 262, //동료 출현
    InstallationPlace = 300, //설치 확정
    InstallationRemove = 301, //철거
    InstallationRefund = 302, //철거 환불
    SpikeTrapActivate = 310, //가시 함정 발동
    ArrowTowerLaunch = 311, //화살 포탑 발사
    ArrowTowerImpact = 312, //화살 포탑 충돌
    FireTrapActivate = 313, //화염 장판 발동
    FrostTrapActivate = 314, //냉기 장판 발동
    ShockTrapActivate = 315, //충격 장치 발동
    FireTrapLoop = 316, //화염 장판 지속음
    EnemyVoice = 400, //일반 적 보이스
    TankVoice = 401, //탱커 보이스
    BossVoice = 402, //보스 보이스
    EnemyAttack = 403, //적 공격
    EnemyHurt = 404, //적 피격
    EnemyDeath = 405, //적 사망
    FlyingEnemyMove = 406, //비행 적 이동
    ObjectiveHurt = 420, //방어 목표 피격
    DangerNotice = 421, //위기 알림
    EnvironmentExplosion = 430, //환경 폭발
    GateMove = 431, //게이트 작동
    MechanismActivate = 432 //기믹 작동
}

/// <summary>
/// 사운드 출력 구분
/// </summary>
public enum SoundType
{
    Bgm, //배경음
    Ui, //일시정지 제외 효과음
    Sfx, //플레이 효과음
    Footstep, //캐릭터 발소리
    LoopSfx //소유 오브젝트에 연결하는 반복 효과음
}
