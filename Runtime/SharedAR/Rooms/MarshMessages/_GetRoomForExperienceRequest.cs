// Copyright 2022-2024 Niantic.

using System;
using System.Collections.Generic;

namespace Niantic.Lightship.SharedAR.Rooms.MarshMessages
{
    /// @note This is an experimental feature. Experimental features should not be used in
    /// production products as they are subject to breaking changes, not officially supported, and
    /// may be deprecated without notice
    [Serializable]
    internal struct _GetRoomForExperienceRequest
    {
        // Lower camelCase names to match Json format that Marsh expects

        #region APIs to be serialized to Marsh

        // Note - these fields cannot be modified to maintain compatibility with Marsh.
        //  No additional public fields should be added without corresponding server changes

        public List<string> experienceIds;

        #endregion
    }
}
