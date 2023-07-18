// Copyright 2023 Niantic, Inc. All Rights Reserved.

namespace Niantic.Lightship.AR
{
    internal interface ISubsystemWithMutableApi<T>
    {
        void SwitchApiImplementation(T api);
        void SwitchToInternalMockImplementation();
    }
}
