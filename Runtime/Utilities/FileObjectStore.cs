using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Niantic.Lightship.AR.Utilities
{
    /// <summary>
    /// Interface to a file store for a class that can be serialised to json (and back).
    /// Once instanced, always writes to the same location.
    /// </summary>
    /// <typeparam name="T">the type of the serializable class</typeparam>
    internal interface IFileObjectStore<in T> where T : class
    {
        /// <summary>
        /// Save an instance of the class. Create any missing directories in the process.
        /// </summary>
        /// <param name="obj">instance of the class to save</param>
        void Save(T obj);

        /// <summary>
        /// Asynchronously load from the location, updating an instance of the class
        /// </summary>
        /// <param name="obj">instance of the class to update</param>
        /// <param name="cancellationToken">token with which to cancel the load</param>
        /// <returns>task that can be awaited</returns>
        Task LoadAsync(T obj, CancellationToken cancellationToken = default);

        void Load(T obj);

        /// <summary>
        /// Clear (delete) the file store
        /// </summary>
        void Clear();

        /// <summary>
        /// Does the file store exist
        /// </summary>
        bool Exists { get; }

        /// <summary>
        /// Get the path (for debugging purposes)
        /// </summary>
        string Path { get; }
    }

    internal class FileObjectStore<T> : IFileObjectStore<T> where T : class
    {
        /// <summary>
        /// Private constructor to prevent instantiation outside of singleton.
        /// </summary>
        private FileObjectStore(string path)
        {
            Path = path;
        }

        /// <summary>
        /// Factory definition for when classes declare this as a dependency
        /// (allows unit tests to mock IFileObjectStore)
        /// </summary>
        public delegate IFileObjectStore<T> Factory(string path);

        /// <summary>
        /// Create an instance of the class that points to a particular path for a particular type of object.
        /// Signature of this function confirms to the Factory definition so that it can be mocked.
        /// </summary>
        public static IFileObjectStore<T> Create(string path)
        {
            return new FileObjectStore<T>(path);
        }

        public void Save(T obj)
        {
            var directory = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                try
                {
                    Directory.CreateDirectory(directory);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to create shared directory {directory}: {e.Message}");
                    return;
                }
            }

            var json = JsonUtility.ToJson(obj);
            File.WriteAllText(Path, json);
        }

        public async Task LoadAsync(T obj, CancellationToken cancellationToken)
        {
            string json = await File.ReadAllTextAsync(Path, cancellationToken);
            JsonUtility.FromJsonOverwrite(json, obj);
        }

        public void Load(T obj)
        {
            string json = File.ReadAllText(Path);
            JsonUtility.FromJsonOverwrite(json, obj);
        }

        public void Clear()
        {
            File.Delete(Path);
        }

        public bool Exists => File.Exists(Path);

        public string Path { get; }
    }
}
