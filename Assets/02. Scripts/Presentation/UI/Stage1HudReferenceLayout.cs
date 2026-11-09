using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

// Attached only to the comparison HUD instance in !_Play_Stage1.
// Formats the wave and score labels without changing authored HUD positions.
[DefaultExecutionOrder(32000)]
[DisallowMultipleComponent]
public sealed class Stage1HudReferenceLayout : MonoBehaviour
{
    private readonly Dictionary<string, RectTransform> _rects = new Dictionary<string, RectTransform>();
    private RectTransform _canvas;

    private void Awake() { _canvas = GetComponent<RectTransform>(); }

    private void LateUpdate()
    {
        if (_canvas == null) return;
        const string stage = "HUDLayoutRoot/StageStatus/";
        FormatLabel(stage + "Wave/WaveText", false);
        FormatLabel(stage + "Score/ScoreText", false);
    }

    private void FormatLabel(string path, bool timer)
    {
        RectTransform rect = Find(path);
        if (rect == null || !rect.TryGetComponent(out TMP_Text label)) return;
        string value = label.text;
        if (timer)
        {
            Match time = Regex.Match(value, @"\d{1,3}:\d{2}");
            if (time.Success) value = time.Value;
        }
        else
        {
            value = Regex.Replace(value, @"\s+", " ").Trim();
            value = Regex.Replace(value, @"^점수\s*:?\s*", "점수:");
        }
        if (label.text != value) label.text = value;
    }

    private RectTransform Find(string path)
    {
        if (_rects.TryGetValue(path, out RectTransform rect) && rect != null) return rect;
        rect = transform.Find(path) as RectTransform;
        if (rect != null) _rects[path] = rect;
        return rect;
    }

}
