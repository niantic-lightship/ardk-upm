// Copyright 2023-2024 Niantic.

using Niantic.Lightship.AR.Utilities;
using UnityEngine;
using System;

namespace Niantic.Lightship.AR.WorldPositioning
{
  /// <summary>
  /// The <c>ARWorldPositioningTangentialTransform</c> class represents a transform from a Euclidean tangential
  /// coordinate system to geographic coordinates and provides methods to convert between the two.
  /// </summary>
  /// <remarks>
  /// It is common to approximate world geographic coordinates within a small region using a
  /// tangential Euclidean coordinate system.  WPS uses this class to represent the transform
  /// between the AR tracking tangential coordinates and world geographic coordinates.
  /// <c>ARWorldPositioningTangentialTransform</c> can also be used to convert any map data which is provided in a
  /// tangential coordinate system.  The representation is similar to that used by GeoPose but with
  /// a left-handed East-Up-North coordinate system to align with Unity rather than the
  /// East-North-Up coordinate system defined in the GeoPose standard.
  /// </remarks>
  /// <value><c>TangentialToEUN</c> the matrix transform from local tangential coordinates to tangential EUN (East-Up-North) coordinates</value>
  /// <value><c>originLatitude</c> the latitude of the origin for the tangential world coordinate system</value>
  /// <value><c>originLongitude</c> the longitude of the origin for the tangential world coordinate system</value>
  /// <value><c>originAltitude</c> the altitude of the origin for the tangential world coordinate system</value>
  [PublicAPI]
  public class ARWorldPositioningTangentialTransform
  {
    public Matrix4x4 TangentialToEUN;
    public double OriginLatitude;
    public double OriginLongitude;
    public double OriginAltitude;


    public ARWorldPositioningTangentialTransform() { }

    /// <summary>
    /// Creates a new ARWorldPositioningTangentialTransform
    /// </summary>
    /// <param name="tangentialToEUN">the matrix transform from local tangential coordinates to tangential EUN (East-Up-North) coordinates</param>
    /// <param name="originLatitude">the latitude of the origin for the tangential world coordinate system</param>
    /// <param name="originLongitude">the longitude of the origin for the tangential world coordinate system</param>
    /// <param name="originAltitude">the altitude of the origin for the tangential world coordinate system</param>
    public ARWorldPositioningTangentialTransform(Matrix4x4 tangentialToEUN, double originLatitude, double originLongitude, double originAltitude)
    {
      TangentialToEUN = tangentialToEUN;
      OriginLatitude = originLatitude;
      OriginLongitude = originLongitude;
      OriginAltitude = originAltitude;
    }

    public static double DEGREES_TO_METRES = 111139.0;
    public static double METRES_TO_DEGREES = 1.0 / DEGREES_TO_METRES;

    /// <summary>
    /// Converts a pose in world geographic coordinates to a pose in the tangential Euclidean
    /// coordinate system
    /// </summary>
    /// <param name="latitudeDegrees">The latitude of the object measured in degrees </param>
    /// <param name="longitudeDegrees">The longitude of the object measured in degrees </param>
    /// <param name="altitudeMetres">The altitude of the object measured in metres above sea level</param>
    /// <param name="worldRotationEUN">The rotation of the object relative to East-Up-North axes</param>
    /// <param name="tangentialTranslationRUF">The corresponding translation in the tangential Euclidean coordinate system</param>
    /// <param name="tangentialRotationRUF">The corresponding rotation in the tangential Euclidean coordinate system</param>
    public void WorldToTangential(double latitudeDegrees, double longitudeDegrees, double altitudeMetres, Quaternion worldRotationEUN, out Vector3 tangentialTranslationRUF, out Quaternion tangentialRotationRUF)
    {

      // Calculate the world position relative to the origin of the tangential coordinate system:
      Vector3 tangentialEUN = new Vector3();
      tangentialEUN[0] = (float)(Math.Cos(latitudeDegrees * Math.PI / 180.0) * (longitudeDegrees - OriginLongitude) * DEGREES_TO_METRES);
      tangentialEUN[1] = (float)(altitudeMetres - OriginAltitude);
      tangentialEUN[2] = (float)((latitudeDegrees - OriginLatitude) * DEGREES_TO_METRES);

      // Convert from world to local coordinates:
      Matrix4x4 EUNToTangential = TangentialToEUN.inverse;
      tangentialTranslationRUF = EUNToTangential.MultiplyPoint(tangentialEUN);
      tangentialRotationRUF = EUNToTangential.rotation * worldRotationEUN;
    }

    /// <summary>
    /// Converts a position in world geographic coordinates to a position in the tangential
    /// Euclidean coordinate system
    /// </summary>
    /// <param name="latitudeDegrees">The latitude of the object measured in degrees </param>
    /// <param name="longitudeDegrees">The longitude of the object measured in degrees </param>
    /// <param name="altitudeMetres">The altitude of the object measured in metres above sea level</param>
    /// <param name="tangentialTranslationRUF">The corresponding rotation in the tangential Euclidean coordinate system</param>
    public void WorldToTangential(double latitudeDegrees, double longitudeDegrees, double altitudeMetres, out Vector3 tangentialTranslationRUF)
    {
      // Calculate the world position relative to the origin of the tangential coordinate system:
      Vector3 tangentialEUN = new Vector3();
      tangentialEUN[0] = (float)(Math.Cos(latitudeDegrees * Math.PI / 180.0) * (longitudeDegrees - OriginLongitude) * DEGREES_TO_METRES);
      tangentialEUN[1] = (float)(altitudeMetres - OriginAltitude);
      tangentialEUN[2] = (float)((latitudeDegrees - OriginLatitude) * DEGREES_TO_METRES);

      Matrix4x4 EUNToTangential = TangentialToEUN.inverse;
      tangentialTranslationRUF = EUNToTangential.MultiplyPoint(tangentialEUN);
    }
    /// <summary>
    /// Converts a position in the tangential Euclidean coordinate system to a position in world
    /// geographic coordinates
    /// </summary>
    /// <param name="tangentialTranslationRUF">The translation in the tangential Euclidean coordinate system</param>
    /// <param name="tangentialRotationRUF">The rotation in the tangential Euclidean coordinate system</param>
    /// <param name="latitudeDegrees">The corresponding latitude of the object measured in degrees </param>
    /// <param name="longitudeDegrees">The corresponding longitude of the object measured in degrees </param>
    /// <param name="altitudeMetres">The corresponding altitude of the object measured in metres above sea level</param>
    /// <param name="worldRotationEUN">The rotation of the object relative to East-Up-North axes</param>
    public void TangentialToWorld(Vector3 tangentialTranslationRUF, Quaternion tangentialRotationRUF, out double latitudeDegrees, out double longitudeDegrees, out double altitudeMetres, out Quaternion worldRotationEUN)
    {

      // Calculate the world position relative to the origin of the tangential coordinate system:
      Vector3 tangentialEUN = TangentialToEUN.MultiplyPoint(tangentialTranslationRUF);
      worldRotationEUN = TangentialToEUN.rotation * tangentialRotationRUF;
      latitudeDegrees = METRES_TO_DEGREES * tangentialEUN[2] + OriginLatitude;
      longitudeDegrees = tangentialEUN[0] * METRES_TO_DEGREES / Math.Cos(latitudeDegrees * Math.PI / 180.0) + OriginLongitude;
      altitudeMetres = tangentialEUN[1] + OriginAltitude;
    }

    /// <summary>
    /// Converts a position in the tangential Euclidean coordinate system to a position in world
    /// geographic coordinates
    /// </summary>
    /// <param name="tangentialPose">The pose of the object in the local tangential Euclidean coordinate system</param>
    /// <param name="latitudeDegrees">The corresponding latitude of the object measured in degrees </param>
    /// <param name="longitudeDegrees">The corresponding longitude of the object measured in degrees </param>
    /// <param name="altitudeMetres">The corresponding altitude of the object measured in metres above sea level</param>
    /// <param name="worldRotationEUN">The rotation of the object relative to East-Up-North axes</param>
    public void TangentialToWorld(Transform tangentialPose, out double latitudeDegrees, out double longitudeDegrees, out double altitudeMetres, out Quaternion worldRotationEUN)
    {
      TangentialToWorld(tangentialPose.localPosition, tangentialPose.localRotation, out latitudeDegrees, out longitudeDegrees, out altitudeMetres, out worldRotationEUN);
    }
  };
}
