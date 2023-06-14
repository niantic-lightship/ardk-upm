using System;
using Niantic.Lightship.AR.Utilities;

namespace Niantic.Lightship.AR.Subsystems
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
            Data = Convert.FromBase64String(data);
        }

        /// <summary>
        /// Converts a payload to a base 64 string
        /// </summary>
        /// <returns>The string representation of the payload</returns>
        public string ToBase64()
        {
            return Convert.ToBase64String(Data);
        }
    }
}
