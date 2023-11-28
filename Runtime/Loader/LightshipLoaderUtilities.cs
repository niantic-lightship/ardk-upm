// Copyright 2022-2023 Niantic.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR.Management;

namespace Niantic.Lightship.AR.Loader
{
    internal static class LightshipLoaderUtilities
    {
        public static void CreateSubsystem<TDescriptor, TSubsystem>(this XRLoaderHelper loader,
            List<TDescriptor> descriptors, string id)
            where TDescriptor : ISubsystemDescriptor
            where TSubsystem : ISubsystem
        {
            var methodInfo = loader.GetType().GetMethod("CreateSubsystem", BindingFlags.NonPublic | BindingFlags.Instance);
            var genericMethodInfo = methodInfo?.MakeGenericMethod(typeof(TDescriptor), typeof(TSubsystem));

            genericMethodInfo?.Invoke(loader, new object[] { descriptors, id });
        }

        public static void DestroySubsystem<TSubsystem>(this XRLoaderHelper loader)
            where TSubsystem : ISubsystem
        {
            var methodInfo = loader.GetType().GetMethod("DestroySubsystem", BindingFlags.NonPublic | BindingFlags.Instance);
            var genericMethodInfo = methodInfo?.MakeGenericMethod(typeof(TSubsystem));

            genericMethodInfo?.Invoke(loader, Array.Empty<object>());
        }
    }
}
