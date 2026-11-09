using System;
using System.Collections.Generic;
using LJTest;
using TMPro;
using UnityEngine;

// Updates authored labels only; all artwork and layout stay in the prefab.
[DisallowMultipleComponent]
public sealed class PlacementCountHudBinder : MonoBehaviour
{
    [SerializeField] private TMP_Text _installationCountText;
    [SerializeField] private TMP_Text _companionCountText;
    [SerializeField, Min(1)] private int _installationKindLimit = 5;
    [SerializeField, Min(1)] private int _companionLimit = 2;

    private TestScene _scene;
    private float _nextRefresh;
    private readonly HashSet<Type> _installationKinds = new HashSet<Type>();

    private void OnEnable()
    {
        _nextRefresh = 0f;
        Refresh();
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + 0.15f;
        Refresh();
    }

    private void Refresh()
    {
        if (_scene == null || !_scene.isActiveAndEnabled)
        {
            _scene = null;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (TestScene candidate in root.GetComponentsInChildren<TestScene>(true))
                    if (candidate.isActiveAndEnabled) { _scene = candidate; break; }
                if (_scene != null) break;
            }
        }

        _installationKinds.Clear();
        int companions = 0;
        if (_scene != null && _scene.IsInitialized && _scene.IsStageReady && !_scene.IsEndingStage)
        {
            foreach (PlacedInstallation installation in FindObjectsByType<PlacedInstallation>(FindObjectsSortMode.None))
            {
                if (!installation.isActiveAndEnabled || installation.Area == null ||
                    installation.gameObject.scene != gameObject.scene ||
                    installation.PaidStageId != _scene.CurrentStageId) continue;

                // Identify the five existing gameplay kinds without changing their owners.
                AddKind<SpikeTrap>(installation);
                AddKind<FrostTrap>(installation);
                AddKind<ArrowTower>(installation);
                AddKind<ShockTrap>(installation);
                AddKind<FireTrap>(installation);
            }

            CompanionHireController hire = _scene.HireController;
            if (hire != null && hire.CurrentStageId == _scene.CurrentStageId)
                for (int i = 0; i < hire.EntryCount; i++)
                {
                    if (!hire.TryGetEntryAt(i, out CompanionHireEntry entry) || entry == null) continue;
                    // Hiring during preparation reserves a slot before an instance is placed.
                    if (entry.State == CompanionHireState.Hired ||
                        ((entry.State == CompanionHireState.Prepared || entry.State == CompanionHireState.Alive) &&
                         entry.Instance != null && entry.Instance.isActiveAndEnabled))
                        companions++;
                }
        }

        SetCount(_installationCountText, _installationKinds.Count, _installationKindLimit);
        SetCount(_companionCountText, companions, _companionLimit);
    }

    private void AddKind<T>(PlacedInstallation installation) where T : Component
    {
        if (installation.TryGetComponent<T>(out _)) _installationKinds.Add(typeof(T));
    }

    private static void SetCount(TMP_Text label, int count, int limit)
    {
        if (label == null) return;
        string value = $"{count}/{limit}";
        if (label.text != value) label.text = value;
    }
}
