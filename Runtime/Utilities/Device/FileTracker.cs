// Copyright 2022-2024 Niantic.

using System;
using System.IO;
using System.Text;
using UnityEngine;
using static Niantic.Lightship.AR.Utilities.Logging.Log;

namespace Niantic.Lightship.AR.Utilities.Device
{
    /// <summary>
    /// Encapsulates the File Operations for any file.
    /// This class is useless. its just a way to allow for testing and doing basic directory existence checks
    /// without devolving the using class into unreadable scramble
    /// </summary>
    internal class FileTracker
    {
        private readonly string _fullFilePath;

        public FileTracker(string path, string fileName)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException($"{path} is not a valid directory.");
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(fileName);
            }

            _fullFilePath = Path.Combine(path, fileName);

            var stream = File.Open(_fullFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            stream.Close();
            Info($"Creating tracker for {_fullFilePath}");
        }

        /// <summary>
        /// Tries to read the data in the file.
        /// </summary>
        /// <param name="data">the data of the file that has been provided.</param>
        /// <param name="ex">Exception in case reading the file was not successful</param>
        /// <returns>the status of the read operation</returns>
        public string ReadData()
        {
            return File.ReadAllText(_fullFilePath);
        }

        /// <summary>
        /// Writes data to file.
        /// </summary>
        /// <param name="content"></param>
        public void WriteData(string content)
        {
            File.WriteAllText(_fullFilePath, content, Encoding.UTF8);
        }
    }
}
