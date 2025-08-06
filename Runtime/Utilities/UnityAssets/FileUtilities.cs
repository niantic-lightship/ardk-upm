// Copyright 2022-2025 Niantic.

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Niantic.Lightship.Utilities.UnityAssets
{
    // Utility class for reading from LOCAL files.
    // Support for reading from the streaming assets folder, which unity treats as a remote source
    // when running on android, is handled by the url check "://" followed by code paths that
    // perform 'local' web requests rather than a direct file read.
    internal class FileUtilities
    {
        // Reads from streaming assets return near-immediately and should never timeout.
        // But we still need to provide some timeout?
        private const int LocalWebRequestMaxWaitMs = 100;

        public static bool TryReadAllText(string filePath, out string result)
        {
            result = null;
            try
            {
                // File.Exists(filePath); returns false for android even when the file exists.
                // so we use try catch instead of something sensible
                result = GetAllText(filePath);
                return true;
            }
            catch
            {
                // ignored
            }

            return false;
        }

        public static string GetAllText(string filePath)
        {
            // All error-checking and exception-throwing is done via the below method invocations
            if (filePath.Contains("://"))
            {
                return GetTextViaWebRequest(filePath);
            }

            return File.ReadAllText(filePath);
        }

        private static string GetTextViaWebRequest(string filePath)
        {
            using UnityWebRequest request = UnityWebRequest.Get(filePath);
            {
                var content = SendWebRequestAndWait(request);
                return Encoding.UTF8.GetString(content);
            }
        }

        public static byte[] GetAllBytes(string filePath)
        {
            // All error-checking and exception-throwing is done via the below method invocations
            if (filePath.Contains("://"))
            {
                return GetBytesViaWebRequest(filePath);
            }

            return File.ReadAllBytes(filePath);
        }

        private static byte[] GetBytesViaWebRequest(string filePath)
        {
            using UnityWebRequest request = UnityWebRequestTexture.GetTexture(filePath);
            {
                var content = SendWebRequestAndWait(request);
                return content;
            }
        }

        public static void ExtractToDirectory(string filePath, string destination, bool overwrite)
        {
            if (filePath.Contains("://"))
            {
                ExtractFromRemoteSource(filePath, destination, overwrite);
            }
            else
            {
                ZipFile.ExtractToDirectory(filePath, destination, overwrite);
            }
        }

        private static void ExtractFromRemoteSource(string filePath, string destination, bool overwrite)
        {
            if (!filePath.Contains(".zip", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Remote source filePath must be a .zip file. Provided: {filePath}", nameof(filePath));
            }
            var bytes = GetAllBytes(filePath);
            Directory.CreateDirectory(destination);
            using MemoryStream zipStream = new MemoryStream(bytes);
            using ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string entryPath = Path.Combine(destination, entry.FullName);
                //entry.Name is the file name, if it is empty the entry is a directory
                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(entryPath);
                }
                else
                {
                    using FileStream fileStream = new FileStream(entryPath, FileMode.Create);
                    using Stream entryStream = entry.Open();
                    entryStream.CopyTo(fileStream);
                }
            }
        }

        private static byte[] SendWebRequestAndWait(UnityWebRequest request)
        {
            var requestOp = request.SendWebRequest();
            var stopWatch = System.Diagnostics.Stopwatch.StartNew();

            while (requestOp.progress < 1f && stopWatch.ElapsedMilliseconds < LocalWebRequestMaxWaitMs)
            {
                // Yield control to avoid blocking the main thread
                System.Threading.Thread.Sleep(1);
            }

            stopWatch.Stop();
            // Reads from StreamingAssets return near-immediately and should never timeout. But just in case...
            if (requestOp.progress < 1f)
            {
                throw new TimeoutException($"Failed to load file at {request.uri.AbsolutePath} due to timeout.");
            }

            if (!string.IsNullOrWhiteSpace(request.error))
            {
                throw new FileLoadException
                (
                    $"Failed to load file at {request.uri.AbsolutePath} due to " +
                    $"UnityWebRequest error {request.error}"
                );
            }

            return request.downloadHandler.data;
        }
    }
}
