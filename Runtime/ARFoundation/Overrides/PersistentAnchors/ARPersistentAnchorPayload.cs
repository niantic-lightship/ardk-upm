// Copyright 2022-2024 Niantic.
using System;
using Niantic.Lightship.AR.Utilities.Logging;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;

namespace Niantic.Lightship.AR.PersistentAnchors
{
    /// <summary>
    /// The ARPersistentAnchorPayload is data used to save and restore persistent anchors.
    /// </summary>
    [PublicAPI]
    [Serializable]
    public class ARPersistentAnchorPayload
    {
        /// <summary>
        /// The data associated with the payload
        /// </summary>
        public byte[] Data;

        /// <summary>
        /// Creates a new ARPersistentAnchorPayload
        /// </summary>
        /// <param name="data">The data associated with the payload</param>
        public ARPersistentAnchorPayload(byte[] data)
        {
            Data = data;
        }

        /// <summary>
        /// Creates a new ARPersistentAnchorPayload
        /// </summary>
        /// <param name="data">The base 64 string to create the payload from</param>
        public ARPersistentAnchorPayload(string data)
        {
            var bytes = new Span<byte>(new byte[data.Length]);
            bool valid = Convert.TryFromBase64String(data, bytes, out int bytesWritten);
            if (valid)
            {
                Data = bytes[..bytesWritten].ToArray();
            }
            else
            {
                Log.Error($"Failed to create ARPersistentAnchorPayload due to invalid payload data: {data}");
            }
        }

        /// <summary>
        /// Converts a payload to a base 64 string.
        /// </summary>
        /// <returns>The string representation of the payload.  Returns null if no data exists in the payload.</returns>
        public string ToBase64()
        {
            if (Data != null)
            {
                return Convert.ToBase64String(Data);
            }
            else
            {
                return null;
            }
        }
    }
}
