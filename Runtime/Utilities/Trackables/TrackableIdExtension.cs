// Copyright 2022-2024 Niantic.

using System.Linq;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Utilities
{
    public static class TrackableIdExtension
    {
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
    }
}
