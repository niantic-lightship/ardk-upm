// Copyright 2022-2025 Niantic.

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

        /// <summary>
        /// Converts a native ARDK UUID string to a TrackableId.
        /// This can then be passed back into native to get the original UUID.
        /// This is the inverse of ToLightshipHexString.
        /// Takes either a 32 character string or a 33 character string with a dash.
        /// </summary>
        public static TrackableId FromNativeUuid(string uuid)
        {
            var gotTrackableIdComponents = LightshipHexStringToUlongs(uuid, out var upper, out var lower);
            if (!gotTrackableIdComponents)
            {
                throw new System.ArgumentException("Invalid input, expected a valid UUID");
            }

            return new TrackableId(upper, lower);
        }

        public static bool LightshipHexStringToUlongs(string hexString, out ulong upper, out ulong lower)
        {
            // Check if the string is a valid UUID
            if (!ValidateStringIsUuid(hexString))
            {
                upper = 0;
                lower = 0;
                return false;
            }

            // Split the string on the dash, and do a string reverse, then pairwise reverse.
            // @note This is a relatively expensive operation, only use it when necessary to line up managed
            //  and unmanaged representations.
            string[] halves;
            if (hexString.Contains('-'))
            {
                if (hexString.Length != 33)
                {
                    throw new System.ArgumentException("Invalid input, expected 33 characters");
                }

                halves = hexString.Split('-');
                if (halves.Length != 2)
                {
                    throw new System.ArgumentException("Invalid input, expected two halves");
                }
            }
            else
            {
                if (hexString.Length != 32)
                {
                    throw new System.ArgumentException("Invalid input, expected 32 characters");
                }

                halves = new string[2];
                halves[0] = hexString.Substring(0, 16);
                halves[1] = hexString.Substring(16, 16);
            }

            for (var i = 0; i < halves.Length; i++)
            {
                // Split each half into pairs
                var intermediate = halves[i].ToCharArray();
                var count = intermediate.Length;

                // Pairwise reverse along each half
                for (int j = 0; j < count; j+=2)
                {
                    var (a, b) = (intermediate[j], intermediate[j + 1]);
                    intermediate[j] = b;
                    intermediate[j + 1] = a;
                }

                // Reverse each half
                intermediate = intermediate.Reverse().ToArray();
                halves[i] = string.Join("", intermediate);
            }

            upper = System.Convert.ToUInt64(halves[0], 16);
            lower = System.Convert.ToUInt64(halves[1], 16);

            return true;
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

        private static bool ValidateStringIsUuid(string hexString)
        {
            // Check if the string is a valid UUID
            if (hexString.Length == 32)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(hexString, @"[0-9a-fA-F]+"))
                {
                    return false;
                }

                return true;
            }

            if (hexString.Length == 33)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(hexString, @"[0-9a-fA-F]{16}-[0-9a-fA-F]{16}"))
                {
                    return false;
                }

                return true;
            }

            return false;
        }
    }
}
