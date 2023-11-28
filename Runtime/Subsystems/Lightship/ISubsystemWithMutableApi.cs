// Copyright 2022-2023 Niantic.

namespace Niantic.Lightship.AR
{
    internal interface ISubsystemWithMutableApi<T>
    {
        void SwitchApiImplementation(T api);
        void SwitchToInternalMockImplementation();
    }
}
