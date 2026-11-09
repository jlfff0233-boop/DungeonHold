using System.Collections.Generic;
using LJTest;
using UnityEngine;
using UnityEngine.UI;

// Presentation only: observes existing scene points without changing their data.
public class WorldPointHudBinder : MonoBehaviour
{
    [SerializeField] private Texture2D _spawnArtwork;
    [SerializeField] private Texture2D _defenseArtwork;
    [SerializeField] private float _mergeDistance = 3f;
    [SerializeField] private float _heightOffset = 2f;
    [SerializeField] private float _iconSize = 85f;

    private sealed class Marker
    {
        public bool Defense;
        public readonly List<Transform> Targets = new List<Transform>();
        public Image Image;
    }

    private readonly List<Marker> _markers = new List<Marker>();
    private Sprite _spawnSprite, _defenseSprite;
    private RectTransform _root;
    private Canvas _canvas;
    private Camera _camera;
    private TestScene _scene;

    private void Start()
    {
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null) return;
        _spawnSprite = MakeSprite(_spawnArtwork);
        _defenseSprite = MakeSprite(_defenseArtwork);
        _root = new GameObject("WorldPointMarkers", typeof(RectTransform)).GetComponent<RectTransform>();
        _root.SetParent(transform, false);
        _root.gameObject.layer = gameObject.layer;
        _root.anchorMin = Vector2.zero;
        _root.anchorMax = Vector2.one;
        _root.offsetMin = _root.offsetMax = Vector2.zero;
        _root.SetAsFirstSibling();

        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            if (_scene == null) _scene = root.GetComponentInChildren<TestScene>(true);
            foreach (Transform point in root.GetComponentsInChildren<Transform>(true))
                if (point.name.StartsWith("EnemySpawnPoint", System.StringComparison.Ordinal))
                    AddPoint(point, false);
            foreach (EnemyPath path in root.GetComponentsInChildren<EnemyPath>(true))
                if (path.WaypointCount > 0)
                    AddPoint(path.transform.GetChild(path.WaypointCount - 1), true);
        }
    }

    private static Sprite MakeSprite(Texture2D texture) => texture == null ? null :
        Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), 100f);

    private void AddPoint(Transform target, bool defense)
    {
        Sprite sprite = defense ? _defenseSprite : _spawnSprite;
        if (sprite == null) return;
        foreach (Marker marker in _markers)
            if (marker.Defense == defense && (!defense ||
                Vector3.Distance(marker.Targets[0].position, target.position) <= _mergeDistance))
            {
                marker.Targets.Add(target);
                return;
            }

        Image image = new GameObject(defense ? "Defense_Point" : "Spawn_Point",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(_root, false);
        image.gameObject.layer = gameObject.layer;
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.rectTransform.sizeDelta = new Vector2(_iconSize, _iconSize);
        image.gameObject.SetActive(false);
        Marker added = new Marker { Defense = defense, Image = image };
        added.Targets.Add(target);
        _markers.Add(added);
    }

    private void LateUpdate()
    {
        if (_root == null) return;
        if (_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
        bool ready = _camera != null && (_scene == null ||
            (_scene.IsInitialized && _scene.IsStageReady && !_scene.IsEndingStage));
        foreach (Marker marker in _markers)
        {
            Vector3 position = Vector3.zero;
            int count = 0;
            foreach (Transform target in marker.Targets)
                if (target != null && target.gameObject.activeInHierarchy)
                {
                    position += target.position;
                    count++;
                }
            marker.Image.gameObject.SetActive(ready && count > 0);
            if (!ready || count == 0) continue;
            position = position / count + Vector3.up * _heightOffset;
            Vector3 screen = _camera.WorldToScreenPoint(position);
            // Mirror points behind the camera around the view center before clamping.
            if (screen.z <= 0f)
            {
                Vector2 center = _camera.pixelRect.center;
                screen.x = center.x * 2f - screen.x;
                screen.y = center.y * 2f - screen.y;
            }
            Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, uiCamera, out Vector2 local))
                continue;
            Rect bounds = _root.rect;
            float inset = _iconSize * 0.5f + 12f;
            if (screen.z <= 0f)
            {
                Vector2 direction = local - bounds.center;
                if (direction.sqrMagnitude < 0.001f) direction = Vector2.down;
                local = bounds.center + direction.normalized * (bounds.width + bounds.height);
            }
            local.x = Mathf.Clamp(local.x, bounds.xMin + inset, bounds.xMax - inset);
            local.y = Mathf.Clamp(local.y, bounds.yMin + inset, bounds.yMax - inset);
            marker.Image.rectTransform.anchoredPosition = local;
        }
    }

    private void OnDisable()
    {
        foreach (Marker marker in _markers)
            if (marker.Image != null) marker.Image.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_root != null) Destroy(_root.gameObject);
        if (_spawnSprite != null) Destroy(_spawnSprite);
        if (_defenseSprite != null) Destroy(_defenseSprite);
    }
}
