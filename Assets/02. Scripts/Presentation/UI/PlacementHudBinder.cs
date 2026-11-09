using TMPro;
using UnityEngine;

public class PlacementHudBinder : MonoBehaviour
{
    [SerializeField] private BuildPreviewController _preview;
    [SerializeField] private BuildPlacementController _placement;
    [SerializeField] private BuildPhaseGate _buildPhaseGate;

    [SerializeField] private TMP_Text _stateText;
    [SerializeField] private TMP_Text _reasonText;
    [SerializeField] private GameObject _forbiddenIcon;
    private ReferenceHudDetails _feedbackHud;

    private void Start()
    {
        if (_forbiddenIcon != null)
            _feedbackHud = _forbiddenIcon.GetComponentInParent<ReferenceHudDetails>(true);
    }

    private void LateUpdate()
    {
        if (_stateText == null || _reasonText == null)
            return;

        bool controllersReady =
            _preview != null &&
            _placement != null &&
            _buildPhaseGate != null &&
            _preview.isActiveAndEnabled &&
            _placement.isActiveAndEnabled &&
            _buildPhaseGate.isActiveAndEnabled;

        if (!controllersReady)
        {
            Show(
                "설치 불가",
                "설치 기능을 사용할 수 없습니다.",
                true,
                Color.gray);
            return;
        }

        if (!_buildPhaseGate.CanBuild)
        {
            Show(
                "설치 불가",
                "준비 시간에만 설치·철거할 수 있습니다.",
                true,
                Color.gray);
            return;
        }

        if (!_preview.HasCell)
        {
            Show(
                "설치 위치 선택",
                "설치 가능한 범위의 바닥을 선택하세요.",
                true,
                Color.white);
            return;
        }

        int angle = _preview.RotationDegrees;

        if (!_preview.IsAreaAvailable)
        {
            Show(
                $"설치 불가 · {angle}도",
                "설치 공간이 부족하거나 다른 대상과 겹칩니다.",
                true,
                new Color32(232, 90, 90, 255));
            return;
        }

        Show(
            $"설치 가능 · {angle}도",
            "F 설치 · R 회전 · V 철거",
            false,
            new Color32(98, 214, 139, 255));
    }

    private void Show(
        string state,
        string reason,
        bool forbidden,
        Color color)
    {
        _stateText.text = state;
        _stateText.color = color;
        _reasonText.text = reason;

        // The central feedback owns the cancel icon when present. Keep the
        // legacy indicator only for HUDs that do not provide that feedback.
        bool showForbidden = forbidden &&
            !(_feedbackHud != null && _feedbackHud.HandlesInstallationFeedback);
        if (_forbiddenIcon != null &&
            _forbiddenIcon.activeSelf != showForbidden)
        {
            _forbiddenIcon.SetActive(showForbidden);
        }
    }
}
