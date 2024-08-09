// Copyright 2022-2024 Niantic.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Niantic.Lightship.SharedAR.Rooms
{
    /// @note This is an experimental feature. Experimental features should not be used in
    /// production products as they are subject to breaking changes, not officially supported, and
    /// may be deprecated without notice
    public class RoomID : IEquatable<RoomID>
    {
        public static readonly RoomID InvalidRoomID = new RoomID("");

        private readonly string _string;

        public RoomID(string id)
        {
            this._string = id;
        }

        public static implicit operator string(RoomID id)
        {
            return id._string;
        }

        public static implicit operator RoomID(string id)
        {
            return new RoomID(id);
        }

        public override string ToString()
        {
            return _string;
        }

        public override int GetHashCode()
        {
            return _string.GetHashCode();
        }

        // Implementing IEquatable
        public bool Equals(RoomID info)
        {
            return info != null && _string == info.ToString();
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RoomID);
        }
    }
} // namespace Niantic.ARDK.SharedAR
