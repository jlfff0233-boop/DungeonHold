using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// 튜토리얼 CSV 형식 - 한글과 따옴표로 감싼 셀의 읽기/쓰기
/// </summary>
static class TutorialCsvFormat
{
    public const string Header = "StepAssetName,TargetName,Description";    //단계 CSV 헤더
    public const string LegacyHeader = Header + ",PaddingX,PaddingY";    //이전에 내보낸 CSV의 헤더

    /// <summary>
    /// CSV 레코드 - 원본 줄 번호와 셀 목록
    /// </summary>
    public class Row
    {
        public int Line;    //레코드가 시작된 줄
        public string [ ] Cells;    //열 순서대로 읽은 값
    }

    /// <summary>
    /// UTF-8 원문을 읽고 BOM 제거
    /// </summary>
    /// <param name="path">CSV 파일 경로</param>
    /// <returns>CSV 원문</returns>
    public static string Read ( string path )
    {
        return new UTF8Encoding ( false , true ).GetString ( File.ReadAllBytes ( path ) ).TrimStart ( '\uFEFF' );
    }

    /// <summary>
    /// 따옴표, 쉼표, 줄바꿈을 처리하여 셀을 원문 그대로 읽음
    /// </summary>
    /// <param name="source">CSV 원문</param>
    /// <returns>빈 줄을 제외한 레코드</returns>
    public static List<Row> Parse ( string source )
    {
        List<Row> rows = new List<Row> ( );
        List<string> cells = new List<string> ( );
        StringBuilder cell = new StringBuilder ( );
        bool isQuoted = false;
        bool isClosed = false;
        int line = 1;
        int rowLine = 1;

        //마지막 줄에 개행이 없어도 같은 경로에서 레코드를 마감
        for ( int i = 0; i <= source.Length; i++ )
        {
            bool isEnd = i == source.Length;
            char character = isEnd == true ? '\n' : source [ i ];
            if ( isQuoted == true )
            {
                if ( isEnd == true )
                    throw new FormatException ( $"CSV {rowLine}행: 닫히지 않은 따옴표입니다." );

                if ( character == '"' )
                {
                    if ( i + 1 < source.Length && source [ i + 1 ] == '"' )
                    {
                        cell.Append ( '"' );
                        i++;
                    }
                    else
                    {
                        isQuoted = false;
                        isClosed = true;
                    }
                }
                else
                {
                    cell.Append ( character );
                    if ( character == '\n' || ( character == '\r' && ( i + 1 == source.Length || source [ i + 1 ] != '\n' ) ) )
                        line++;
                }
                continue;
            }

            if ( character == ',' || character == '\r' || character == '\n' )
            {
                cells.Add ( cell.ToString ( ) );
                cell.Clear ( );
                isClosed = false;
                if ( character == ',' )
                    continue;

                if ( cells.Count > 1 || string.IsNullOrWhiteSpace ( cells [ 0 ] ) == false )
                    rows.Add ( new Row { Line = rowLine , Cells = cells.ToArray ( ) } );

                cells.Clear ( );
                if ( character == '\r' && i + 1 < source.Length && source [ i + 1 ] == '\n' )
                    i++;
                rowLine = ++line;
                continue;
            }

            if ( character == '"' && cell.Length == 0 && isClosed == false )
            {
                isQuoted = true;
                continue;
            }

            if ( isClosed == true || character == '"' )
                throw new FormatException ( $"CSV {line}행: 따옴표 위치가 잘못됐습니다." );

            cell.Append ( character );
        }

        return rows;
    }

    /// <summary>
    /// 셀의 쉼표/따옴표/줄바꿈을 보존하도록 인용
    /// </summary>
    /// <param name="value">셀 원문</param>
    /// <returns>인용한 CSV 셀</returns>
    public static string Quote ( string value )
    {
        return "\"" + ( value ?? string.Empty ).Replace ( "\"" , "\"\"" ) + "\"";
    }
}
