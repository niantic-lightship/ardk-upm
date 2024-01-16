// Copyright 2022-2024 Niantic.

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Niantic.Lightship.AR.Utilities.UnityAssets
{
    internal static class AssetDatabaseUtilities
    {
        public struct Prefab
        {
            public string Guid;
            public string Name;

            public Prefab(string guid, string name)
            {
                Guid = guid;
                Name = name;
            }
        }

        /// Search the asset database for prefabs.
        public static Prefab[] FindPrefabsWithComponent<T>(params string[] searchInFolders)
        {
            var prefabs = new List<Prefab>();

            if (searchInFolders.Length == 0)
                searchInFolders = new string[] { "Assets" };

            var guids = AssetDatabase.FindAssets("t:prefab", searchInFolders);

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);

                var prefab = asset as GameObject;
                if (prefab == null)
                    continue;

                var comp = prefab.GetComponent<T>();
                if (comp != null)
                {
                    prefabs.Add(new Prefab(guid, asset.name));
                }
            }

            return prefabs.ToArray();
        }


        public static T[] FindAssets<T>(params string[] searchInFolders)
            where T : class
        {
            var assets = new List<T>();

            if (searchInFolders.Length == 0)
                searchInFolders = new string[] { "Assets" };

            var guids = AssetDatabase.FindAssets("t:" + typeof(T), searchInFolders);

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                assets.Add(asset as T);
            }

            return assets.ToArray();
        }
    }
}
#endif
