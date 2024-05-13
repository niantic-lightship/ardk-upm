// Copyright 2022-2024 Niantic.

using System.Collections.Generic;
using System.Linq;
using Niantic.Lightship.AR.Utilities;
using UnityEngine;
using UnityEngine.Serialization;

namespace Niantic.Lightship.AR.NavigationMesh
{
    /// <summary>
    /// <c>LightshipNavMeshRenderer</c> is a helper <see cref="MonoBehaviour"/> which will draw the <see cref="LightshipNavMesh"/> tiles
    /// If you want to draw the <see cref="LightshipNavMesh"/> in a custom way you can create a similar renderer
    /// e.g. stylize the board as water/snow/sand etc.
    /// </summary>
    [PublicAPI("apiref/Niantic/Lightship/AR/NavigationMesh/LightshipNavMeshRenderer/")]
    public class LightshipNavMeshRenderer : MonoBehaviour
    {
        [FormerlySerializedAs("_navMeshManager")]
        [FormerlySerializedAs("_gameboardManager")]
        [Tooltip("The LightshipNavMeshManager that owns the LightshipNavMesh to render.")]
        [SerializeField]
        private LightshipNavMeshManager _lightshipNavMeshManager;

        [Tooltip("The material to apply to generated meshes")]
        [SerializeField]
        private Material _material;

        private MeshFilter _meshFilter;
        private Mesh _mesh;

        private void Start()
        {
            //add a render mesh
            _meshFilter = gameObject.AddComponent<MeshFilter>();
            var ren = gameObject.AddComponent<MeshRenderer>();
            GetComponent<MeshFilter>().mesh = new Mesh();
            _mesh = GetComponent<MeshFilter>().mesh;

            ren.material = _material;
        }

        private void OnEnable()
        {
            //when we enable/disable the object we also need to turn off the mesh rendering as that is static
            //we create the mesh components on start so we need to guard it here.
            MeshRenderer ren;
            gameObject.TryGetComponent<MeshRenderer>(out ren);
            if (ren)
                ren.enabled = true;
        }

        private void OnDisable()
        {
            var ren = gameObject.GetComponent<MeshRenderer>();
            ren.enabled = false;
        }

        private void UpdateMesh()
        {
            //this will build a mesh for the LightshipNavMesh nav tiles
            //you can copy and customise this if you would like to stylise the navigation mesh surface.
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var vIndex = 0;

            float offset = 0.002f;
            var halfSize = _lightshipNavMeshManager.LightshipNavMesh.Settings.TileSize / 2.0f;
            foreach (var surface in _lightshipNavMeshManager.LightshipNavMesh.Surfaces)
            {
                foreach (var center in surface.Elements.Select
                         (node => Utils.TileToPosition(node.Coordinates, surface.Elevation,
                             _lightshipNavMeshManager.LightshipNavMesh.Settings.TileSize)))
                {
                    var a = center + new Vector3(-halfSize + offset, 0.0f, -halfSize + offset);
                    var b = center + new Vector3(halfSize - offset, 0.0f, -halfSize + offset);
                    var c = center + new Vector3(halfSize - offset, 0.0f, halfSize - offset);
                    var d = center + new Vector3(-halfSize + offset, 0.0f, halfSize - offset);

                    // Vertices
                    vertices.Add(a);
                    vertices.Add(b);
                    vertices.Add(c);
                    vertices.Add(d);

                    // Indices
                    triangles.Add(vIndex + 2);
                    triangles.Add(vIndex + 1);
                    triangles.Add(vIndex);

                    triangles.Add(vIndex);
                    triangles.Add(vIndex + 3);
                    triangles.Add(vIndex + 2);

                    vIndex += 4;
                }
            }

            //now make the mesh
            if (vertices.Count >= 4)
            {
                _mesh.Clear();
                _mesh.vertices = vertices.ToArray();
                _mesh.triangles = triangles.ToArray();
                _mesh.UploadMeshData(markNoLongerReadable: false);
                _meshFilter.mesh = _mesh;
            }
        }

        private void Update()
        {
            UpdateMesh();
        }
    }
}
