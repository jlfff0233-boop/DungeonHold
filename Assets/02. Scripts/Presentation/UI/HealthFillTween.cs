using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// Presentation state only; health values and death timing remain authoritative.
internal sealed class HealthFillTween
{
    private Tween _tween;
    private Image _image;
    private float _target;
    private bool _initialized;

    public void Set(Image image, float value)
    {
        if (image == null) return;
        value = Mathf.Clamp01(value);
        if (_image != image || !_initialized || !Application.isPlaying ||
            !image.gameObject.activeInHierarchy)
        {
            Reset();
            _image = image;
            _target = value;
            _initialized = true;
            image.fillAmount = value;
            return;
        }
        if (Mathf.Approximately(_target, value)) return;
        bool damage = value < _target;
        _target = value;
        _tween?.Kill();
        if (!damage)
        {
            image.fillAmount = value;
            return;
        }
        _tween = DOTween.To(() => image.fillAmount, amount => image.fillAmount = amount, value, 0.25f)
            .SetEase(Ease.OutQuad)
            .SetLink(image.gameObject, LinkBehaviour.KillOnDisable);
    }

    public void Reset()
    {
        _tween?.Kill();
        _tween = null;
        _initialized = false;
        _image = null;
    }
}
