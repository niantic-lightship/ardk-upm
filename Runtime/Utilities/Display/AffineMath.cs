// Copyright 2022-2024 Niantic.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Niantic.Lightship.AR.Utilities
{
  /// <summary>
  /// This utility is a collection of functions that produce 2D affine transformations.
  /// Affine transformation is a linear mapping method that preserves points, straight
  /// lines, and planes. The functions here are used to calculate matrices that fit
  /// images to the viewport.
  /// Visit to learn more: https://en.wikipedia.org/wiki/Affine_transformation#Image_transformation
  /// </summary>
  internal static class AffineMath
  {
    /// <summary>
    /// Returns an affine transformation such that if multiplied with normalized
    /// coordinates in the target coordinate frame, the results are the normalized
    /// coordinates in the source coordinate frame.
    /// </summary>
    /// <param name="sourceWidth">The width of the source coordinate frame.</param>
    /// <param name="sourceHeight">The height of the source coordinate frame.</param>
    /// <param name="sourceOrientation">The orientation of the source coordinate frame.</param>
    /// <param name="targetWidth">The width of the target coordinate frame.</param>
    /// <param name="targetHeight">The height of the target coordinate frame.</param>
    /// <param name="targetOrientation">The orientation of the target coordinate frame.</param>
    /// <param name="reverseRotation">If true, rotation will be interpreted counter-clockwise.</param>
    /// <returns>An affine transformation matrix embedded in a Matrix4x4.</returns>
    internal static Matrix4x4 Fit
    (
      float sourceWidth,
      float sourceHeight,
      ScreenOrientation sourceOrientation,
      float targetWidth,
      float targetHeight,
      ScreenOrientation targetOrientation,
      bool reverseRotation = false
    )
    {
      var rotatedContainer = RotateResolution
        (sourceWidth, sourceHeight, sourceOrientation, targetOrientation);

      // Calculate scaling
      var squareTarget = Mathf.FloorToInt(targetWidth) == Mathf.FloorToInt(targetHeight);
      var s = squareTarget || targetOrientation is ScreenOrientation.Portrait or ScreenOrientation.PortraitUpsideDown
          ? new Vector2(targetWidth / (targetHeight / rotatedContainer.y * rotatedContainer.x), 1.0f)
          : new Vector2(1.0f, targetHeight / (targetWidth / rotatedContainer.x * rotatedContainer.y));

      var rotate = reverseRotation
          ? ScreenRotation(from: targetOrientation, to: sourceOrientation)
          : ScreenRotation(from: sourceOrientation, to: targetOrientation);
      var scale = Scaling(s);
      var translate = Translation(new Vector2((1.0f - s.x) * 0.5f, (1.0f - s.y) * 0.5f));

      return scale * translate * rotate;
    }

    /// Produces a 2D rotation matrix that preserves parallel lines
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 Rotation(double rad)
    {
      return new Matrix4x4
      (
        new Vector4((float) Math.Cos(rad), (float) -Math.Sin(rad), 0, 0),
        new Vector4((float) Math.Sin(rad), (float) Math.Cos(rad), 0, 0),
        new Vector4(0, 0, 1, 0),
        new Vector4(0, 0, 0, 1)
      );
    }

    /// Produces a 2D translation matrix that preserves parallel lines
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 Translation(Vector2 translation)
    {
      return new Matrix4x4
      (
        new Vector4(1, 0, translation.x, 0),
        new Vector4(0, 1, translation.y, 0),
        new Vector4(0, 0, 1, 0),
        new Vector4(0, 0,0, 1)
      );
    }

    /// Produces a 2D scaling matrix that preserves parallel lines
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Matrix4x4 Scaling(Vector2 scale)
    {
      return new Matrix4x4
      (
        new Vector4(scale.x, 0, 0, 0),
        new Vector4(0, scale.y, 0, 0),
        new Vector4(0, 0, 1, 0),
        new Vector4(0, 0, 0, 1)
      );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector2 RotateResolution
    (
      float sourceWidth,
      float sourceHeight,
      ScreenOrientation sourceOrientation,
      ScreenOrientation targetOrientation
    )
    {
      if (sourceOrientation == ScreenOrientation.LandscapeLeft)
      {
        return
          targetOrientation == ScreenOrientation.LandscapeLeft ||
          targetOrientation == ScreenOrientation.LandscapeRight
            ? new Vector2(sourceWidth, sourceHeight)
            : new Vector2(sourceHeight, sourceWidth);
      }

      return
        targetOrientation == ScreenOrientation.Portrait ||
        targetOrientation == ScreenOrientation.PortraitUpsideDown
          ? new Vector2(sourceWidth, sourceHeight)
          : new Vector2(sourceHeight, sourceWidth);
    }

    /// <summary>
    /// Calculates an affine transformation to rotate from one screen orientation to another
    /// around the pivot.
    /// </summary>
    /// <param name="from">Original orientation.</param>
    /// <param name="to">Target orientation.</param>
    /// <returns>An affine matrix to be applied to normalized image coordinates.</returns>
    internal static Matrix4x4 ScreenRotation(ScreenOrientation from, ScreenOrientation to)
    {
      // Rotate around the center
      return Translation(-s_center) * Rotation(GetRadians(from, to)) * Translation(s_center);
    }

    /// <summary>
    /// Calculates the angle to rotate from one screen orientation to another in radians.
    /// </summary>
    /// <param name="from">Original orientation.</param>
    /// <param name="to">Target orientation.</param>
    /// <returns>Angle to rotate to get from one orientation to the other.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double GetRadians(ScreenOrientation from, ScreenOrientation to)
    {
      const double rotationUnit = Math.PI / 2.0;
      return (s_screenOrientationLookup[to] - s_screenOrientationLookup[from]) * rotationUnit;
    }

    #region Constants

    /// Screen orientation to rotation id
    private static readonly IDictionary<ScreenOrientation, int> s_screenOrientationLookup =
      new Dictionary<ScreenOrientation, int>
      {
        {
          ScreenOrientation.LandscapeLeft, 0
        },
        {
          ScreenOrientation.Portrait, 1
        },
        {
          ScreenOrientation.LandscapeRight, 2
        },
        {
          ScreenOrientation.PortraitUpsideDown, 3
        }
      };

    /// Matrix to invert an UV vertically
    internal static readonly Matrix4x4 s_invertVertical
      = new(
        new Vector4(1, 0, 0, 0),
        new Vector4(0, -1, 1, 0),
        new Vector4(0, 0, 1, 0),
        new Vector4(0, 0, 0, 1)
      );

    /// Matrix to invert an UV horizontally
    internal static readonly Matrix4x4 s_invertHorizontal
      = new(
        new Vector4(-1, 0, 1, 0),
        new Vector4(0, 1, 0, 0),
        new Vector4(0, 0, 1, 0),
        new Vector4(0, 0, 0, 1)
      );

    private static readonly Vector2 s_center = new(0.5f, 0.5f);

    #endregion
  }
}
