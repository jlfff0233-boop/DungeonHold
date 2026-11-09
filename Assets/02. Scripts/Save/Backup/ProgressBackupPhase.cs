/// <summary>
/// 수동 백업 진행 단계
/// </summary>
public enum ProgressBackupPhase
{
    Idle,       //요청/확인 후보가 없는 대기 상태
    PreparingLocal,       //로컬 진행 준비
    Authenticating,       //익명 인증 준비
    ReadingCloud,         //서버 문서 조회
    AwaitingConfirmation,       //업로드 후보를 보관한 채 서버 백업 교체 확인 대기
    Uploading,      //서버 저장
    ValidatingRestore,    //다운로드한 백업 확인
    ApplyingRestore      //저장 데이터 검사와 로컬 반영
}
