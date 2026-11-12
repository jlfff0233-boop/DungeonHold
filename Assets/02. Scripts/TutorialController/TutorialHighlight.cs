using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 강조 연출 - 주변 패널/프레임과 설명 박스 전환
/// </summary>
public class TutorialHighlight : MonoBehaviour
{
    [Header ( "----- 컴포넌트 -----" )]
    [SerializeField] RectTransform _area;    //전체 연출 영역
    [SerializeField] RectTransform _frame;    //강조 영역 프레임
    [SerializeField] Button [ ] _panels;    //위/아래/왼쪽/오른쪽 패널

    [Header ( "----- 전환 -----" )]
    [SerializeField] float _duration = 0.35f;    //위치와 크기 전환 시간
    [SerializeField] Ease _ease = Ease.InOutQuad;    //전환 곡선

    [Header ( "----- 설명 -----" )]
    [SerializeField] RectTransform _descriptionBox;    //설명 박스
    [SerializeField] TMP_Text _descriptionText;    //단계 설명 출력
    [SerializeField] float _descriptionGap = 24f;    //강조 영역과 설명 사이 간격
    [SerializeField] float _screenMargin = 16f;    //설명의 화면 가장자리 여백

    Vector3 [ ] _corners = new Vector3 [ 4 ];    //대상 영역의 월드 모서리
    Vector2 _center;    //현재 강조 중심
    Vector2 _size;    //현재 강조 크기
    Tween _transition;    //현재 전환 연출
    bool _isMoving;    //강조 영역 전환 여부

    /// <summary>
    /// 다음 단계 요청 이벤트
    /// </summary>
    public event Action OnNextRequested;

    private void Awake ( )
    {
        //설명과 프레임은 연출 영역의 직계 자식으로 연결하고 중심 좌표를 통일
        PrepareRect ( _frame );
        PrepareRect ( _descriptionBox );

        foreach ( Button panel in _panels )
        {
            PrepareRect ( ( RectTransform ) panel.transform );
            panel.onClick.AddListener ( HandlePanelClicked );
        }
    }

    /// <summary>
    /// 강조 영역과 단계 설명을 함께 전환
    /// </summary>
    /// <param name="target">강조할 UI 영역</param>
    /// <param name="description">단계 설명</param>
    /// <param name="immediate">즉시 배치 여부</param>
    public void Show (
        RectTransform target ,
        string description ,
        bool immediate )
    {
        //이전 연출을 멈추고 현재 표시 위치를 새 전환의 시작점으로 사용
        _transition?.Kill ( );
        _transition = null;
        _isMoving = false;

        //문구 반영 후 레이아웃을 확정해 실제 설명 크기로 목적 위치 계산
        _descriptionText.text = description;
        Canvas.ForceUpdateCanvases ( );
        Rect targetRect = GetTargetRect ( target );
        Vector2 descriptionPosition = GetDescriptionPosition (
            targetRect , out Vector2 pivot );

        //자동 선택한 방향의 피벗을 적용하되 기존 화면상 위치는 보존
        SetDescriptionPivot ( pivot );

        if ( immediate == true || _duration <= 0f )
        {
            ApplyRect ( targetRect.center , targetRect.size );
            _descriptionBox.anchoredPosition = descriptionPosition;
            return;
        }

        Vector2 startCenter = _center;
        Vector2 startSize = _size;
        Vector2 startDescription = _descriptionBox.anchoredPosition;
        float progress = 0f;

        _isMoving = true;

        //새 피벗에서도 기존 화면 위치를 시작점으로 삼아 강조 영역과 함께 이동
        _transition = DOTween.To (
            ( ) => progress ,
            value =>
            {
                progress = value;

                ApplyRect (
                    Vector2.LerpUnclamped (
                        startCenter , targetRect.center , value ) ,
                    Vector2.LerpUnclamped (
                        startSize , targetRect.size , value ) );

                _descriptionBox.anchoredPosition = Vector2.LerpUnclamped (
                    startDescription , descriptionPosition , value );
            } ,
            1f ,
            _duration )
            .SetEase ( _ease )
            //일시정지 화면에서도 안내 전환은 계속 진행
            .SetUpdate ( true )
            .OnComplete ( ( ) =>
            {
                //모든 배치가 끝난 뒤 패널 클릭으로 다음 단계 진행 허용
                _transition = null;
                _isMoving = false;
            } );
    }

    /// <summary>
    /// 화면상 위치를 유지하면서 설명 박스 피벗 변경
    /// </summary>
    /// <param name="pivot">새 설명 배치 기준점</param>
    void SetDescriptionPivot ( Vector2 pivot )
    {
        Vector3 previousCorner = _descriptionBox.TransformPoint ( _descriptionBox.rect.min );
        _descriptionBox.pivot = pivot;
        Vector3 currentCorner = _descriptionBox.TransformPoint ( _descriptionBox.rect.min );

        //같은 모서리의 월드 위치 차이를 복구해 피벗 변경에 의한 순간이동 방지
        _descriptionBox.position += previousCorner - currentCorner;
    }

    /// <summary>
    /// 네 방향의 화면 노출 범위와 중심 거리를 비교해 설명 배치 선택
    /// </summary>
    /// <param name="highlight">강조 영역</param>
    /// <param name="pivot">선택된 설명 피벗</param>
    /// <returns>선택된 설명 피벗의 연출 영역 로컬 위치</returns>
    Vector2 GetDescriptionPosition ( Rect highlight , out Vector2 pivot )
    {
        Rect bounds = _area.rect;
        Vector2 size = _descriptionBox.rect.size;
        Vector2 halfSize = size * 0.5f;

        //작은 화면에서도 여백 때문에 유효 화면 경계가 뒤집히지 않도록 제한
        Vector2 margin = new Vector2 (
            Mathf.Min ( _screenMargin , bounds.width * 0.5f ) ,
            Mathf.Min ( _screenMargin , bounds.height * 0.5f ) );
        Rect visible = Rect.MinMaxRect (
            bounds.xMin + margin.x , bounds.yMin + margin.y ,
            bounds.xMax - margin.x , bounds.yMax - margin.y );

        float bestOverflow = float.PositiveInfinity;
        float bestDistance = float.PositiveInfinity;
        Vector2 bestCenter = highlight.center;
        pivot = new Vector2 ( 0.5f , 0.5f );

        for ( int i = 0 ; i < 4 ; i++ )
        {
            Vector2 center = highlight.center;
            Vector2 candidatePivot;

            //설명 중심과 강조 박스 쪽을 향하는 피벗을 함께 계산
            switch ( i )
            {
                case 0:
                    center.x = highlight.xMax + _descriptionGap + halfSize.x;
                    candidatePivot = new Vector2 ( 0f , 0.5f );
                    break;
                case 1:
                    center.x = highlight.xMin - _descriptionGap - halfSize.x;
                    candidatePivot = new Vector2 ( 1f , 0.5f );
                    break;
                case 2:
                    center.y = highlight.yMax + _descriptionGap + halfSize.y;
                    candidatePivot = new Vector2 ( 0.5f , 0f );
                    break;
                default:
                    center.y = highlight.yMin - _descriptionGap - halfSize.y;
                    candidatePivot = new Vector2 ( 0.5f , 1f );
                    break;
            }

            Vector2 min = center - halfSize;
            Vector2 max = center + halfSize;
            float visibleWidth = Mathf.Max ( 0f ,
                Mathf.Min ( max.x , visible.xMax ) - Mathf.Max ( min.x , visible.xMin ) );
            float visibleHeight = Mathf.Max ( 0f ,
                Mathf.Min ( max.y , visible.yMax ) - Mathf.Max ( min.y , visible.yMin ) );
            float overflow = Mathf.Max ( 0f , size.x * size.y - visibleWidth * visibleHeight );
            float distance = ( center - visible.center ).sqrMagnitude;

            //화면 밖 면적을 먼저 비교하고 같은 면적이면 화면 중심에 가까운 후보 우선
            bool sameOverflow = Mathf.Approximately ( overflow , bestOverflow );
            if ( overflow < bestOverflow
                || ( sameOverflow == true && distance < bestDistance ) )
            {
                bestOverflow = overflow;
                bestDistance = distance;
                bestCenter = center;
                pivot = candidatePivot;
            }
        }

        //어느 방향에도 들어가지 않으면 선택한 후보를 화면 안으로 보정
        //설명이 화면보다 큰 축은 중앙에 배치하며 강조 영역과 겹칠 수 있음
        float minX = visible.xMin + halfSize.x;
        float maxX = visible.xMax - halfSize.x;
        float minY = visible.yMin + halfSize.y;
        float maxY = visible.yMax - halfSize.y;
        bestCenter.x = minX <= maxX
            ? Mathf.Clamp ( bestCenter.x , minX , maxX ) : visible.center.x;
        bestCenter.y = minY <= maxY
            ? Mathf.Clamp ( bestCenter.y , minY , maxY ) : visible.center.y;

        //설명 중심을 실제 배치에 사용하는 피벗 위치로 변환
        return bestCenter + Vector2.Scale ( pivot - new Vector2 ( 0.5f , 0.5f ) , size );
    }

    /// <summary>
    /// 대상 화면 영역을 연출 영역의 로컬 좌표로 변환
    /// </summary>
    /// <param name="target">강조할 UI 영역</param>
    /// <returns>화면 범위 안의 강조 영역</returns>
    Rect GetTargetRect ( RectTransform target )
    {
        Canvas targetCanvas = target.GetComponentInParent<Canvas> ( ).rootCanvas;
        Canvas areaCanvas = _area.GetComponentInParent<Canvas> ( ).rootCanvas;

        Camera targetCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : targetCanvas.worldCamera;
        Camera areaCamera = areaCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : areaCanvas.worldCamera;

        target.GetWorldCorners ( _corners );

        Vector2 min = Vector2.positiveInfinity;
        Vector2 max = Vector2.negativeInfinity;

        //서로 다른 Canvas에서도 같은 영역을 얻도록 화면 좌표를 거쳐 변환
        foreach ( Vector3 corner in _corners )
        {
            Vector2 screenPoint =
                RectTransformUtility.WorldToScreenPoint ( targetCamera , corner );

            RectTransformUtility.ScreenPointToLocalPointInRectangle (
                _area , screenPoint , areaCamera , out Vector2 localPoint );

            min = Vector2.Min ( min , localPoint );
            max = Vector2.Max ( max , localPoint );
        }

        Rect bounds = _area.rect;

        //화면 밖으로 나간 부분은 잘라 패널 크기가 음수가 되지 않도록 처리
        min.x = Mathf.Clamp ( min.x , bounds.xMin , bounds.xMax );
        min.y = Mathf.Clamp ( min.y , bounds.yMin , bounds.yMax );
        max.x = Mathf.Clamp ( max.x , bounds.xMin , bounds.xMax );
        max.y = Mathf.Clamp ( max.y , bounds.yMin , bounds.yMax );

        return Rect.MinMaxRect ( min.x , min.y , max.x , max.y );
    }

    /// <summary>
    /// 강조 영역과 주변 패널을 같은 기준으로 배치
    /// </summary>
    /// <param name="center">강조 중심</param>
    /// <param name="size">강조 크기</param>
    void ApplyRect ( Vector2 center , Vector2 size )
    {
        _center = center;
        _size = size;

        Vector2 min = center - size * 0.5f;
        Vector2 max = center + size * 0.5f;
        Rect bounds = _area.rect;

        SetRect ( _frame , min.x , min.y , max.x , max.y );

        //위/아래는 전체 폭을 덮고 좌/우는 강조 높이만 덮어 패널 중첩 방지
        SetRect ( ( RectTransform ) _panels [ 0 ].transform ,
            bounds.xMin , max.y , bounds.xMax , bounds.yMax );
        SetRect ( ( RectTransform ) _panels [ 1 ].transform ,
            bounds.xMin , bounds.yMin , bounds.xMax , min.y );
        SetRect ( ( RectTransform ) _panels [ 2 ].transform ,
            bounds.xMin , min.y , min.x , max.y );
        SetRect ( ( RectTransform ) _panels [ 3 ].transform ,
            max.x , min.y , bounds.xMax , max.y );
    }

    /// <summary>
    /// 연출 사각형의 앵커와 중심 기준 통일
    /// </summary>
    /// <param name="rect">배치할 사각형</param>
    void PrepareRect ( RectTransform rect )
    {
        rect.anchorMin = _area.pivot;
        rect.anchorMax = _area.pivot;
        rect.pivot = new Vector2 ( 0.5f , 0.5f );
    }

    /// <summary>
    /// 경계 좌표로 사각형 배치
    /// </summary>
    /// <param name="rect">배치할 사각형</param>
    /// <param name="left">왼쪽 경계</param>
    /// <param name="bottom">아래 경계</param>
    /// <param name="right">오른쪽 경계</param>
    /// <param name="top">위쪽 경계</param>
    void SetRect (
        RectTransform rect , float left , float bottom , float right , float top )
    {
        rect.anchoredPosition = new Vector2 (
            ( left + right ) * 0.5f ,
            ( bottom + top ) * 0.5f );

        rect.sizeDelta = new Vector2 ( right - left , top - bottom );
    }

    /// <summary>
    /// 전환 완료 후 패널 클릭을 다음 단계 요청으로 전달
    /// </summary>
    void HandlePanelClicked ( )
    {
        if ( _isMoving == true )
            return;

        OnNextRequested?.Invoke ( );
    }

    private void OnDestroy ( )
    {
        foreach ( Button panel in _panels )
            panel.onClick.RemoveListener ( HandlePanelClicked );
    }

    private void OnDisable ( )
    {
        //닫힌 화면의 전환이 이후 다시 열린 안내 위치를 덮어쓰지 않도록 종료
        _transition?.Kill ( );
        _transition = null;
        _isMoving = false;
    }
}
