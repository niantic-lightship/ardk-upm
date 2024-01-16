// Copyright 2022-2024 Niantic.

using System.Collections.Generic;
using UnityEngine;

namespace Niantic.Lightship.AR.Loader
{
    internal interface ILightshipLoader
    {
        void InjectLightshipLoaderHelper(LightshipLoaderHelper lightshipLoaderHelper);

        // this is so we can write tests
        public bool InitializeWithLightshipHelper
        (
            LightshipLoaderHelper lightshipLoaderHelper,
            bool isTest = false
        );

        bool InitializePlatform();

        bool DeinitializePlatform();

        bool IsPlatformDepthAvailable();

        void CreateSubsystem<TDescriptor, TSubsystem>(List<TDescriptor> descriptors, string id)
            where TDescriptor : ISubsystemDescriptor
            where TSubsystem : ISubsystem;

        void DestroySubsystem<T>() where T : class, ISubsystem;

        T GetLoadedSubsystem<T>() where T : class, ISubsystem;

        internal void AddExternalLoader(ILightshipExternalLoader loader);
    }
}
