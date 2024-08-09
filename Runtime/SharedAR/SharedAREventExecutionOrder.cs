// Copyright 2022 - 2023 Niantic.

namespace Niantic.Lightship.SharedAR.Settings
{
    // Define the order of events that are fired from SharedAR.
    // Used with the MonoBehaviourEventDispatcher's queue.
    public enum SharedAREventExecutionOrder : int
    {
        Default = 0,
        Networking = 1,
    }
}
