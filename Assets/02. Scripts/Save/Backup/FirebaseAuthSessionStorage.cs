using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;

/// <summary>
/// 인증 정보 보관 - Windows 사용자 자격 증명 저장소 사용
/// </summary>
public class FirebaseAuthSessionStorage
{
    string _targetName;     //프로젝트별 자격 증명 이름

    /// <summary>
    /// 프로젝트별 인증 보관 위치 구성
    /// </summary>
    /// <param name="projectId">Firebase 프로젝트 ID</param>
    public FirebaseAuthSessionStorage ( string projectId )
    {
        _targetName = "TeamProject.FirebaseAuth." + projectId;
    }

    /// <summary>
    /// 보관한 익명 인증 정보 읽기
    /// </summary>
    /// <param name="session">읽은 인증 정보</param>
    /// <param name="error">토큰을 제외한 오류 내용</param>
    /// <returns>읽기 결과</returns>
    public AuthSessionReadResult TryLoad (
        out FirebaseAuthSession session , out string error )
    {
        session = null;
        error = null;

#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
        //종류 1은 일반 자격 증명(CRED_TYPE_GENERIC), 플래그 0은 기본 읽기
        if ( CredRead ( _targetName , 1 , 0 , out IntPtr pointer ) == false )
        {
            //SetLastError로 보관한 직전 Windows API 실패 원인 조회
            int code = Marshal.GetLastWin32Error ( );

            //1168(ERROR_NOT_FOUND)은 보관 정보 없음: 호출부에서 최초 익명 인증 진행
            if ( code == 1168 )
                return AuthSessionReadResult.NotFound;

            //권한 등 다른 읽기 실패를 새 사용자 생성으로 대체하지 않음
            error = $"인증 정보 읽기 실패: Windows 오류 {code}";
            return AuthSessionReadResult.Failed;
        }

        try
        {
            //Windows가 반환한 구조체 주소를 C# 전달 구조로 읽기
            NativeCredential credential =
                Marshal.PtrToStructure<NativeCredential> ( pointer );

            //본문은 UTF-16 JSON이므로 바이트 크기를 2로 나눠 문자 수로 전달
            string json = Marshal.PtrToStringUni (
                credential.CredentialBlob , ( int ) credential.CredentialBlobSize / 2 );

            if ( BackupJson.TryRead (
                json , out SessionData loaded ) == false
                || string.IsNullOrWhiteSpace ( loaded.UserId ) == true
                || string.IsNullOrWhiteSpace ( loaded.RefreshToken ) == true )
            {
                error = "보관된 인증 정보가 올바르지 않습니다.";
                return AuthSessionReadResult.Failed;
            }

            //필수 값을 확인한 인증 정보만 호출부에 전달
            session = new FirebaseAuthSession ( loaded.UserId , loaded.RefreshToken );
            return AuthSessionReadResult.Success;
        }
        finally
        {
            //구조체와 본문은 Windows 소유 버퍼이므로 CredFree로 함께 반환
            CredFree ( pointer );
        }
#else
        error = "현재 인증 정보 보관 구현은 Windows용입니다.";
        return AuthSessionReadResult.Unsupported;
#endif
    }

    /// <summary>
    /// 인증 정보를 현재 PC의 Windows 사용자에게 보관
    /// </summary>
    /// <param name="session">보관할 인증 정보</param>
    /// <param name="error">토큰을 제외한 오류 내용</param>
    /// <returns>보관 성공 여부</returns>
    public bool TrySave ( FirebaseAuthSession session , out string error )
    {
        error = null;

#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
        //기존 인증 JSON의 필드 이름 유지
        string json = JsonUtility.ToJson ( new SessionData
        {
            UserId = session.UserId ,
            RefreshToken = session.RefreshToken
        } );
        //Windows API에 전달할 UTF-16 버퍼를 직접 할당
        IntPtr blob = Marshal.StringToCoTaskMemUni ( json );

        try
        {
            //Windows CREDENTIALW 규격에 맞춰 본문 주소와 보관 범위 구성
            NativeCredential credential = new NativeCredential
            {
                Type = 1 , //CRED_TYPE_GENERIC: 앱이 관리하는 일반 자격 증명
                TargetName = _targetName ,
                CredentialBlobSize = ( uint ) Encoding.Unicode.GetByteCount ( json ) ,
                CredentialBlob = blob ,
                Persist = 2 , //CRED_PERSIST_LOCAL_MACHINE: 같은 PC/Windows 사용자로 재실행 시 유지
                UserName = session.UserId
            };

            //동일한 보관 이름/종류의 정보를 기록 또는 갱신, 플래그 0은 기본 쓰기
            if ( CredWrite ( ref credential , 0 ) == false )
            {
                //CredWrite 실패 원인을 가져오며 인증 JSON은 로그에 포함하지 않음
                error = "인증 정보 보관 실패: Windows 오류 "
                    + Marshal.GetLastWin32Error ( );
                return false;
            }

            return true;
        }
        finally
        {
            //CredWrite 호출이 끝나면 직접 할당한 인증 버퍼를 지운 뒤 반환
            Marshal.ZeroFreeCoTaskMemUnicode ( blob );
        }
#else
        error = "현재 인증 정보 보관 구현은 Windows용입니다.";
        return false;
#endif
    }

#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
    /// <summary>
    /// 인증 보관 JSON 전용 전달 구조
    /// </summary>
    [Serializable]
    class SessionData
    {
        public string UserId;       //기존 보관 필드 이름
        public string RefreshToken; //재실행 시 같은 사용자의 토큰 갱신에 사용
    }

    /// <summary>
    /// Windows 자격 증명 전달 구조 - CREDENTIALW 규격 대응
    /// </summary>
    //필드 순서와 타입은 Windows 규격 유지, 문자열은 유니코드로 전달
    [StructLayout ( LayoutKind.Sequential , CharSet = CharSet.Unicode )]
    struct NativeCredential
    {
        public uint Flags;              //자격 증명 옵션, 현재 0
        public uint Type;               //종류, 현재 1(일반 자격 증명)
        public string TargetName;       //정보를 조회/갱신할 프로젝트별 보관 이름
        public string Comment;          //추가 설명, 현재 미사용
        public FILETIME LastWritten;        //Windows가 기록한 최종 갱신 시각
        public uint CredentialBlobSize;     //인증 JSON 본문의 바이트 크기
        public IntPtr CredentialBlob;   //인증 JSON 본문이 있는 메모리 주소
        public uint Persist;            //보관 범위, 현재 2(같은 PC의 Windows 사용자)
        public uint AttributeCount;     //추가 속성 수, 현재 0
        public IntPtr Attributes;       //추가 속성 배열 주소, 현재 미사용
        public string TargetAlias;      //대상 별칭, 현재 미사용
        public string UserName;         //함께 기록할 Firebase UID, Windows 로그인 계정 아님
    }

    /// <summary>
    /// 현재 Windows 사용자의 자격 증명 읽기
    /// </summary>
    /// <param name="target">프로젝트별 보관 이름</param>
    /// <param name="type">자격 증명 종류</param>
    /// <param name="flags">읽기 플래그</param>
    /// <param name="credential">Windows가 할당한 응답 주소</param>
    /// <returns>읽기 성공 여부</returns>
    [DllImport ( "Advapi32.dll" , EntryPoint = "CredReadW" ,
        CharSet = CharSet.Unicode , SetLastError = true , ExactSpelling = true )]
    //Windows BOOL 반환값을 C# bool로 변환
    [return: MarshalAs ( UnmanagedType.Bool )]
    static extern bool CredRead (
        string target , uint type , uint flags , out IntPtr credential );

    /// <summary>
    /// 현재 Windows 사용자에게 자격 증명 보관
    /// </summary>
    /// <param name="credential">보관할 자격 증명</param>
    /// <param name="flags">쓰기 플래그</param>
    /// <returns>보관 성공 여부</returns>
    [DllImport ( "Advapi32.dll" , EntryPoint = "CredWriteW" ,
        CharSet = CharSet.Unicode , SetLastError = true , ExactSpelling = true )]
    [return: MarshalAs ( UnmanagedType.Bool )]
    static extern bool CredWrite ( ref NativeCredential credential , uint flags );

    /// <summary>
    /// Windows가 읽기 응답에 할당한 버퍼 반환
    /// </summary>
    /// <param name="buffer">CredRead 응답 주소</param>
    [DllImport ( "Advapi32.dll" , ExactSpelling = true )]
    static extern void CredFree ( IntPtr buffer );
#endif
}
