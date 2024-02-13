// Copyright 2022-2024 Niantic.

namespace Niantic.Lightship.AR
{
    /// <summary>
    /// <c>ISubsystemWithMutableApi</c> defines methods for changing the implementation of a subsystem interface at runtime.
    /// </summary>
    /// <typeparam name="T">The API type</typeparam>
    internal interface ISubsystemWithMutableApi<T>
    {
        /// <summary>
        /// Changes the current API implementation to the one provided
        /// </summary>
        /// <param name="api">The API implementation which should replace the current one</param>
        void SwitchApiImplementation(T api);

        /// <summary>
        /// Changes the API implementation to a predefined mock implementation
        /// </summary>
        void SwitchToInternalMockImplementation();
    }
}
