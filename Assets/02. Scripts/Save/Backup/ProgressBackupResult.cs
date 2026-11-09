/// <summary>
/// 수동 백업 처리 결과
/// </summary>
public enum ProgressBackupResult
{
    Success,        //처리 완료
    ConfirmationRequired,       //서버 백업 교체 확인 필요
    Unavailable,        //씬 연결 또는 다른 작업 중
    ConfigurationMissing,       //필요한 설정 미연결
    UnsupportedPlatform,        //인증 정보 보관 미지원 플랫폼
    NoLocalProgress,        //백업할 로컬 진행 없음
    NoCloudBackup,      //서버 백업 없음
    NoRecoveryData,     //복원 전 보관본 없음
    InvalidData,        //형식/값/설정 호환 오류
    UnsupportedVersion,     //지원하지 않는 버전
    TooLarge,       //백업 JSON 크기 초과
    AuthenticationFailed,       //인증 실패
    AuthenticationStorageFailed,        //인증 정보 읽기/보관 실패
    NetworkFailed,      //통신 실패 또는 제한시간 초과
    PermissionDenied,       //접근 권한 거절
    RemoteFailed,       //그 외 서버 요청 실패
    PreconditionFailed,     //조회 후 서버 문서가 변경/생성/삭제되어 교체 조건 불일치
    UploadUnconfirmed,      //서버 반영 가능성이 있어 업로드 완료 여부 재확인 필요
    LocalFileFailed,        //로컬 파일 보존/교체 실패
    Canceled        //서비스 종료에 따른 취소
}
