using UnityEngine;
using UnityEngine.UI;

public class HelpBookNavigator : MonoBehaviour
{
    [SerializeField] private GameObject[] _pages;
    [SerializeField] private Button _previousButton;
    [SerializeField] private Button _nextButton;

    [Header("페이지 순서와 동일하게 지정")]
    [SerializeField] private Image[] _dotImages;
    [SerializeField] private Color _normalColor =
        new Color32(140, 140, 140, 255);
    [SerializeField] private Color _selectedColor =
        new Color32(255, 204, 77, 255);

    private int _index;

    private void OnEnable()
    {
        _index = 0;
        Refresh();
    }

    public void Previous()
    {
        GoToPage(_index - 1);
    }

    public void Next()
    {
        GoToPage(_index + 1);
    }

    public void GoToPage(int index)
    {
        if (_pages == null || index < 0 || index >= _pages.Length)
            return;

        _index = index;
        Refresh();
    }

    private void Refresh()
    {
        int count = _pages == null ? 0 : _pages.Length;

        for (int i = 0; i < count; i++)
        {
            if (_pages[i] != null)
                _pages[i].SetActive(i == _index);
        }

        if (_dotImages != null)
        {
            for (int i = 0; i < _dotImages.Length; i++)
            {
                if (_dotImages[i] != null)
                {
                    _dotImages[i].color = i == _index
                        ? _selectedColor
                        : _normalColor;
                }
            }
        }

        if (_previousButton != null)
            _previousButton.interactable = count > 0 && _index > 0;

        if (_nextButton != null)
            _nextButton.interactable = _index < count - 1;
    }
}
