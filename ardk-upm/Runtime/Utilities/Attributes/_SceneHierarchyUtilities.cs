#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Niantic.Lightship.AR.RCA.Editor.Utilities
{
    internal static class _SceneHierarchyUtilities
    {
        // Same behavior as Unity when creating objects from the context menu
        public static string BuildObjectName(string nameBase)
        {
            if (GameObject.Find(nameBase) == null)
                return nameBase;

            var regex = new Regex(nameBase + @"\s[(]\d+[)]");
            var allObjects = Object.FindObjectsOfType<GameObject>(true);
            var patternMatches = allObjects.Where(go => regex.IsMatch(go.name)).Select(go => go.name).ToList();
            patternMatches.Sort();

            var count = 1;
            foreach (var matchedName in patternMatches)
            {
                if (!matchedName.Contains(count.ToString()))
                    break;

                count++;
            }

            return $"{nameBase} ({count})";
        }

        // ReSharper restore Unity.ExpensiveCode

        public static GameObject[] FindGameObjects<T>(string regexPattern = null, Transform parent = null)
            where T : Component
        {
            IEnumerable<GameObject> objectsOfType;
            if (parent != null)
                objectsOfType = parent.GetComponentsInChildren<T>(true).Select(c => c.gameObject);
            else
                objectsOfType = Object.FindObjectsOfType<T>(true).Select(c => c.gameObject);

            if (!string.IsNullOrEmpty(regexPattern))
            {
                var patternMatches = objectsOfType.Where(go => new Regex(regexPattern).IsMatch(go.name)).ToList();
                return patternMatches.ToArray();
            }

            return objectsOfType.ToArray();
        }

        // ReSharper restore Unity.ExpensiveCode

        public static T[] FindComponents<T>(string regexPattern = null, Transform parent = null)
            where T : Component
        {
            var gameObjects = FindGameObjects<T>(regexPattern, parent);
            return gameObjects.Select(g => g.GetComponent<T>()).ToArray();
        }

        public static bool ValidateChildOf<T>(Component comp, bool destroyIfNotChild)
        {
            var isChild = comp.gameObject.GetComponentInParent<T>() != null;
            if (!isChild && destroyIfNotChild)
            {
                Debug.LogError($"The {comp.GetType()} component can only exist in the {typeof(T)} hierarchy.");
                GameObject.DestroyImmediate(comp);
                return false;
            }

            return true;
        }
    }
}
#endif
