// Copyright 2022-2024 Niantic.

using System.Linq;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Utilities
{
    public static class TrackableIdExtension
    {
        internal const int TrackableIdByteSize = 16;

        // Formats a trackableId into a string that is compatible with Lightship's native components
        // By default, trackableId uses a {X16}-{X16} format for its string representation.
        // For example, E8EF31BACFB54BADB27AEC85FBE35C6F becomes AD4BB5CFBA31EFE8-6F5CE3FB85EC7AB2
        // Split the string on the dash, and do a string reverse, then pairwise reverse.
        // @note This is a relatively expensive operation, only use it when necessary to line up managed
        //  and unmanaged representations.
        public static string ToLightshipHexString(this TrackableId trackableId)
        {
            var baseString = trackableId.ToString();

            // Split on dash, expect two halves
            var halves = baseString.Split('-');
            if (halves.Length != 2)
            {
                return baseString;
            }

            for (var i = 0; i < halves.Length; i++)
            {
                // Reverse each half
                var intermediate = halves[i].Reverse().ToArray();
                var count = intermediate.Length;

                // Pairwise reverse along each half
                for (int j = 0; j < count; j+=2)
                {
                    var (a, b) = (intermediate[j], intermediate[j + 1]);
                    intermediate[j] = b;
                    intermediate[j + 1] = a;
                }

                halves[i] = string.Join("", intermediate);
            }

            return string.Join("", halves);
        }

        public static byte[] ToBytes(this TrackableId trackableId)
        {
            // Serialize lower and upper parts of the trackableId
            var lower = trackableId.subId1;
            var upper = trackableId.subId2;
            var bytes = new byte[TrackableIdByteSize];

            for (var i = 0; i < 8; i++)
            {
                bytes[i] = (byte)(lower >> (i * 8));
                bytes[i + 8] = (byte)(upper >> (i * 8));
            }

            return bytes;
        }

        public static TrackableId FromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length != TrackableIdByteSize)
            {
                throw new System.ArgumentException("Invalid input, expected 16 bytes");
            }

            // Deserialize lower and upper parts of the trackableId
            ulong lower = 0;
            ulong upper = 0;

            for (var i = 0; i < 8; i++)
            {
                lower |= (ulong)bytes[i] << (i * 8);
                upper |= (ulong)bytes[i + 8] << (i * 8);
            }

            return new TrackableId(lower, upper);
        }
    }
}
