// Copyright 2022-2024 Niantic.

using Niantic.Lightship.AR.Utilities;

namespace Niantic.Lightship.SharedAR.Rooms
{
    /// <summary>
    /// Visibility of the room. Public means accessible from any users using same application (API key).
    /// When the room is private, passcode is required to access.
    /// </summary>
    [PublicAPI]
    public enum RoomVisibility : byte
    {
        Unknown = 0,

        /// Publicly visible and can be found through the ExperienceService
        Public,

        /// Private room that can only be joined through RoomId
        Private
    }

    /// <summary>
    /// The RoomParams struct contains properties of the room
    /// </summary>
    [PublicAPI]
    public struct RoomParams
    {
        /// <summary>
        /// Room ID of the room. This is only set by RoomManagementService.
        /// </summary>
        [PublicAPI]
        public string RoomID { get; internal set; }

        /// <summary>
        /// Visibility of the room
        /// </summary>
        [PublicAPI]
        public RoomVisibility Visibility { get; internal set; }

        /// <summary>
        /// Capacity of the room. Value should be between 2 to 32 peers.
        /// </summary>
        [PublicAPI]
        public int Capacity { get; internal set; }

        /// <summary>
        /// Name of the room. Name does not need to be unique between rooms
        /// </summary>
        [PublicAPI]
        public string Name { get; internal set; }

        internal string ExperienceId { get; set; }

        /// <summary>
        /// Description of the room
        /// </summary>
        [PublicAPI]
        public string Description { get; internal set; }

        /// <summary>
        /// Passcode to access the room. Required when visibility is set to private.
        /// </summary>
        [PublicAPI]
        public string Passcode { internal get; set; }

        // Endpoint prefix
        internal string Endpoint {  get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="capacity">Capacity of the room. Value should be between 2 to 32 peers.</param>
        /// <param name="name">Name of the room</param>
        /// <param name="description">Description of the room</param>
        /// <param name="passcode">Passcode of the room</param>
        /// <param name="visibility">Visibility of the room</param>
        [PublicAPI]
        public RoomParams
        (
            int capacity,
            string name = "",
            string description = "",
            string passcode = "",
            RoomVisibility visibility = RoomVisibility.Public
        ) : this("", capacity, name, "", description, passcode, visibility: visibility)
        {
        }

        /// <summary>
        /// Constructor for construction through ID. Typically used in conjunction with
        /// Room.GetOrCreateRoomAsync which returns the necessary string ID.
        /// </summary>
        /// <param name="id">The specific RoomID GUID from the backend</param>
        /// <param name="visibility">Visibility of the room</param>
        [PublicAPI]
        public RoomParams
        (
            string id,
            RoomVisibility visibility = RoomVisibility.Public
        ) : this(id, default, visibility: visibility)
        {
        }

        internal RoomParams
        (
            string id,
            int capacity,
            string name = "",
            string experienceId = "",
            string description = "",
            string passcode = "",
            RoomVisibility visibility = RoomVisibility.Public,
            string endpoint = ""
        )
        {
            RoomID = id;
            Capacity = capacity;
            Name = name;
            ExperienceId = experienceId;
            Description = description;
            Visibility = visibility;
            // Don't apply a passcode unless the room is private
            Passcode = Visibility == RoomVisibility.Private ? passcode : "";
            Endpoint = endpoint;
        }
    }
} // namespace Niantic.ARDK.SharedAR
