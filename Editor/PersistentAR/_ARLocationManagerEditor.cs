#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Niantic.Lightship.AR.Subsystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(ARLocationManager))]
internal class _ARLocationManagerEditor : Editor
{
    private ARLocation[] _arLocations;

    private void Awake()
    {
        if (Application.isPlaying)
        {
            return;
        }
        var arLocationManager = (ARLocationManager)target;
        _arLocations = arLocationManager.GetComponentsInChildren<ARLocation>(true);
        ValidateARLocationManager(arLocationManager);
        ValidateARLocations();
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (Application.isPlaying)
        {
            return;
        }
        LayOutLocationSelector();
        if (GUILayout.Button($"Add AR Location"))
        {
            AddARLocation();
        }
    }

    private void ValidateARLocationManager(ARLocationManager arLocationManager)
    {
        if (arLocationManager.transform.position != Vector3.zero || arLocationManager.transform.rotation != Quaternion.identity)
        {
            arLocationManager.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Debug.LogWarning($"The AR Location Manager must remain at the origin (0,0,0).", arLocationManager.gameObject);
        }
    }

    private void ValidateARLocations()
    {
        if (_arLocations.Length > 0)
        {
            var activeRoots = _arLocations.Where(r => r.gameObject.activeSelf).ToArray();
            if (activeRoots.Length == 0) //If no roots are active, activate the first one
            {
                _arLocations[0].gameObject.SetActive(true);
            }
            else if (activeRoots.Length > 1) //If multiple roots are active, deactivate all but one
            {
                for (int i = 0; i < activeRoots.Length; i++)
                {
                    bool active = i == 0;
                    activeRoots[i].gameObject.SetActive(active);
                }
            }
        }

        //Check for duplicate locations
        for (int i = 0; i < _arLocations.Length - 1; i++)
        {
            for (int j = 1; j < _arLocations.Length; j++)
            {
                if (_arLocations[i] == _arLocations[j])
                {
                    continue;
                }

                if (_arLocations[i].Payload == _arLocations[j].Payload)
                {
                    Debug.LogError($"Duplicate AR Locations in the same scene ({_arLocations[i].name}) are not supported.", _arLocations[i].gameObject);
                }
            }
        }
    }

    private void LayOutLocationSelector()
    {
        var locationNames = new List<string>();
        locationNames.Add("None");
        locationNames.AddRange(_arLocations.Select(r => r.name));
        var selectedARLocation = _arLocations.FirstOrDefault(r => r.gameObject.activeSelf);
        if (selectedARLocation)
        {
            int previousSelectedLocation = Array.IndexOf(_arLocations, selectedARLocation) + 1;
            int selectedLocation = EditorGUILayout.Popup("AR Location", previousSelectedLocation, locationNames.ToArray());
            if (previousSelectedLocation != selectedLocation)
            {
                _arLocations[previousSelectedLocation - 1].gameObject.SetActive(false);
                EditorUtility.SetDirty(_arLocations[previousSelectedLocation - 1]);
                if (selectedLocation > 0)
                {
                    _arLocations[selectedLocation - 1].gameObject.SetActive(true);
                    EditorUtility.SetDirty(_arLocations[selectedLocation - 1]);
                }
            }
        }
        else
        {
            int selectedLocation = EditorGUILayout.Popup("AR Location", 0, locationNames.ToArray());
            if (selectedLocation > 0)
            {
                _arLocations[selectedLocation - 1].gameObject.SetActive(true);
                EditorUtility.SetDirty(_arLocations[selectedLocation - 1]);
            }
        }
    }

    private void AddARLocation()
    {
        var arLocationManager = (ARLocationManager)target;
        var selectedARLocation = _arLocations.FirstOrDefault(r => r.gameObject.activeSelf);
        if (selectedARLocation)
        {
            selectedARLocation.gameObject.SetActive(false);
        }
        var arLocationGameObject =
            new GameObject("AR Location", typeof(ARLocation));
        GameObjectUtility.SetParentAndAlign(arLocationGameObject, arLocationManager.gameObject);
        Undo.RegisterCreatedObjectUndo(arLocationGameObject,
            "Create " + arLocationGameObject.name);
        Selection.activeObject = arLocationGameObject;
        arLocationGameObject.SetActive(true);
    }
}
#endif
