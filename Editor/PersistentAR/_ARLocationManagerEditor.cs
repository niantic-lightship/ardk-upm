// Copyright 2022-2024 Niantic.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.LocationAR;
using Niantic.Lightship.AR.Subsystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Niantic.Lightship.AR.Loader;

[CustomEditor(typeof(ARLocationManager))]
[InitializeOnLoad]
internal class _ARLocationManagerEditor : Editor
{
    private ARLocation[] _arLocations;
    private string[] _arLocationNames;

    private int _selectedLocationIndex = 0;

    static _ARLocationManagerEditor()
    {
        EditorApplication.playModeStateChanged += HandleOnPlayModeChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static class Contents
    {
        public static readonly GUIContent locationSelectorLabel =
            new GUIContent
            (
                "AR Location",
                $"Currently selected {nameof(ARLocation)} object. Manages the active states of all in the scene."
            );

        public static readonly GUIContent addLocationButtonLabel =
            new GUIContent
            (
                "Add AR Location",
                $"Creates a new child {nameof(ARLocation)} object and selects it."
            );
    }

    private void Awake()
    {
        if (Application.isPlaying)
        {
            return;
        }

        var arLocationManager = (ARLocationManager)target;
        ValidateARLocationManager(arLocationManager);
        ValidateARLocations(arLocationManager);
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (Application.isPlaying)
        {
            return;
        }

        LayOutLocationSelector();
        if (GUILayout.Button(Contents.addLocationButtonLabel))
        {
            AddARLocation();
        }
    }

    private void ValidateARLocationManager(ARLocationManager arLocationManager)
    {
        if (arLocationManager.transform.position != Vector3.zero ||
            arLocationManager.transform.rotation != Quaternion.identity)
        {
            arLocationManager.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Log.Warning
            (
                $"The AR Location Manager must remain at the origin (0,0,0)." +
                arLocationManager.gameObject
            );
        }
    }

    private void ValidateARLocations(ARLocationManager arLocationManager)
    {
        _arLocations = arLocationManager.GetComponentsInChildren<ARLocation>(true).Prepend(null).ToArray();
        _arLocationNames = _arLocations.Select(r => r != null ? r.name : "None").ToArray();

        // Check for non-null and active locations
        List<int> activeLocationIndices = new List<int>();
        for (int i = 0; i < _arLocations.Length; i++)
        {
            if (_arLocations[i] != null)
            {
                if (_arLocations[i].gameObject.activeSelf)
                {
                    activeLocationIndices.Add(i);
                }
            }
        }

        // Select the appropriate location and validate there is only one active
        switch (activeLocationIndices.Count)
        {
            case 0: // No active locations, do nothing
                break;
            case 1: // One active location, select it
                SelectLocationIndex(activeLocationIndices[0]);
                break;
            case > 1: // Multiple active locations, deactivate all but the last one
                for (int i = 0; i < activeLocationIndices.Count - 1; ++i)
                {
                    ARLocation extraActiveLocation = _arLocations[activeLocationIndices[i]];
                    extraActiveLocation.gameObject.SetActive(false);
                    EditorUtility.SetDirty(extraActiveLocation);
                }

                SelectLocationIndex(activeLocationIndices[^1]);
                break;
        }

        // Check for duplicate locations
        for (int i = 0; i < _arLocations.Length - 1; i++)
        {
            for (int j = i + 1; j < _arLocations.Length; j++)
            {
                if (_arLocations[i] == null || _arLocations[j] == null || _arLocations[i] == _arLocations[j])
                {
                    continue;
                }

                if (_arLocations[i].Payload == _arLocations[j].Payload)
                {
                    Log.Error
                    (
                        $"Duplicate AR Locations in the same scene ({_arLocations[i].name} and {_arLocations[j].name}) are not supported." +
                        _arLocations[i].gameObject
                    );
                }
            }
        }

        // Unity dropdowns filter out identical strings (and remove trailing spaces)
        // To handle the case where a user names two locations the same, we add n tab characters to the nth location name.
        // This ensures every entry is unique, allowing the dropdown to display all locations.
        // The tab characters aren't rendered in the dropdown, so the entries will appear normal.
        for (int i = 0; i < _arLocationNames.Length; ++i)
        {
            for (int j = 0; j < i; ++j)
            {
                _arLocationNames[i] += '\t';
            }
        }
    }

    private void LayOutLocationSelector()
    {
        SelectLocationIndex
        (
            EditorGUILayout.Popup
            (
                Contents.locationSelectorLabel,
                _selectedLocationIndex,
                _arLocationNames
            )
        );
    }

    private void AddARLocation()
    {
        var arLocationManager = (ARLocationManager)target;

        var arLocationGameObject = new GameObject(ARLocationEditor.DefaultARLocationName);
        var arLocationComponent = arLocationGameObject.AddComponent<ARLocation>();
        GameObjectUtility.SetParentAndAlign(arLocationGameObject, arLocationManager.gameObject);
        GameObjectUtility.EnsureUniqueNameForSibling(arLocationGameObject);
        ValidateARLocations(arLocationManager);
        SelectLocation(arLocationComponent);

        Undo.RegisterCreatedObjectUndo
        (
            arLocationGameObject,
            "Create " + arLocationGameObject.name
        );
        Selection.activeObject = arLocationGameObject;
    }

    private void SelectLocation(ARLocation location)
    {
        for (int i = 0; i < _arLocations.Length; i++)
        {
            if (_arLocations[i] == location)
            {
                SelectLocationIndex(i);
                return;
            }
        }
    }

    private void SelectLocationIndex(int value)
    {
        if (value == _selectedLocationIndex) return;

        // Deactivate the previously selected location
        ARLocation previousLocation = _arLocations[_selectedLocationIndex];
        if (previousLocation != null)
        {
            bool wasActive = previousLocation.gameObject.activeSelf;
            previousLocation.gameObject.SetActive(false);
            if (wasActive)
            {
                EditorUtility.SetDirty(previousLocation);
            }
        }

        if (value > _arLocations.Length) return;

        // Activate the newly selected location
        ARLocation selectedLocation = _arLocations[value];
        if (selectedLocation != null)
        {
            bool wasInactive = selectedLocation.gameObject.activeSelf == false;
            selectedLocation.gameObject.SetActive(true);
            if (wasInactive)
            {
                EditorUtility.SetDirty(selectedLocation);
            }
        }

        _selectedLocationIndex = value;
    }

    private static void HandleOnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            HandleMeshVisibility();
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HandleMeshVisibility();
    }

    private static void HandleMeshVisibility()
    {
        if (!LightshipSettings.Instance.UsePlayback)
            return;

        // Because we hide the object right as we enter play mode, the de-activated state
        // does not get serialized to the scene. Meaning Unity automatically handles restoring
        // the active flag to it's previous state when exiting play mode.
        var allManagers = UnityEngine.Object.FindObjectsOfType<ARLocationManager>();
        foreach (var locationManager in allManagers)
        {
            foreach (var arLocation in locationManager.ARLocations)
            {
                if (!arLocation.IncludeMeshInBuild && arLocation.MeshContainer != null)
                {
                    arLocation.MeshContainer.SetActive(false);
                }
            }
        }
    }
}
#endif
