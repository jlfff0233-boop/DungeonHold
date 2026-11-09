using System;
using UnityEngine;

/// <summary>
/// 백업 JSON 변환 - 외부 응답의 변환 실패 처리
/// </summary>
public static class BackupJson
{
    /// <summary>
    /// 외부 JSON을 전달 타입으로 변환
    /// </summary>
    /// <param name="json">외부 JSON</param>
    /// <param name="data">변환한 데이터</param>
    /// <returns>변환 성공 여부</returns>
    public static bool TryRead<T> ( string json , out T data ) where T : class
    {
        data = null;

        if ( string.IsNullOrWhiteSpace ( json ) == true )
            return false;

        try
        {
            //JSON 변환만 담당하며 필수 값과 사용 가능 여부는 호출부에서 검사
            data = JsonUtility.FromJson<T> ( json );
            return data != null;
        }
        catch ( ArgumentException )
        {
            //인증 응답 원문이나 토큰은 오류에 포함하지 않음
            return false;
        }
    }
}
