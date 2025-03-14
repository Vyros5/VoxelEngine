using UnityEngine;

namespace VoxelEngine.Utilities
{
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;
    using VoxelEngine;
    using VoxelEngine.Core;

    [BurstCompile]
    public static class CubeBuilderUtils
    {
        public const int CubeMaxVertices24 = 24;
        public const int CubeMaxIndices = 36;


        #region Data

        /// <summary>
        /// Local positions of all 24 vertices in a cube, seperated by each face.
        /// There are 6 faces in a cube.
        /// </summary>
        public static readonly float3[] CubeVertices =
       {
          // Front face
          new( 0.5f, -0.5f,  0.5f),
          new(-0.5f, -0.5f,  0.5f),
          new(-0.5f,  0.5f,  0.5f),
          new( 0.5f,  0.5f,  0.5f),
          
          // Back face
          new(-0.5f, -0.5f, -0.5f),
          new( 0.5f, -0.5f, -0.5f),
          new( 0.5f,  0.5f, -0.5f),
          new(-0.5f,  0.5f, -0.5f),
          
          // Left face
          new(-0.5f, -0.5f,  0.5f),
          new(-0.5f, -0.5f, -0.5f),
          new(-0.5f,  0.5f, -0.5f),
          new(-0.5f,  0.5f,  0.5f),
          
          // Right face
          new( 0.5f, -0.5f, -0.5f),
          new( 0.5f, -0.5f,  0.5f),
          new( 0.5f,  0.5f,  0.5f),
          new( 0.5f,  0.5f, -0.5f),
          
          // Top face
          new(-0.5f,  0.5f, -0.5f),
          new( 0.5f,  0.5f, -0.5f),
          new( 0.5f,  0.5f,  0.5f),
          new(-0.5f,  0.5f,  0.5f),

          // Bottom face
          new(-0.5f, -0.5f,  0.5f),
          new( 0.5f, -0.5f,  0.5f),
          new( 0.5f, -0.5f, -0.5f),
          new(-0.5f, -0.5f, -0.5f),
       };

        public static readonly float2[] CubeUVs =
        {
            new(0, 0),
            new(1, 0),
            new(1, 1),
            new(0, 1)
        };

        #endregion


        /// <summary>
        /// Handles building every part of the cubes vertex data.
        /// Uses BurstCompile, meaning it will be compiled into optimized machine code.
        /// </summary>
        [BurstCompile]
        public static void BuildCube24(
            // Output
            ref VertexData vertexData,
            ref int vertexIndex,
            // Input
            in Block block,
            ref NativeHashMap<BlockTypeEquatable, BlockDefinition> blockMapping,
            IncludeFaces includeFaces = IncludeFaces.All)
        {
            // Skip if not building any faces.
            if (includeFaces == IncludeFaces.None)
                return;

            int vertexIndexOriginal = vertexIndex;

            BuildVertices(ref vertexData.vertices, ref vertexIndex, block.position, includeFaces);

            BuildIndices(ref vertexData.indices, vertexIndexOriginal, includeFaces);

            BuildNormals(ref vertexData.normals, includeFaces);

            BuildTangents(ref vertexData.tangents, includeFaces);

            // Material Index
            MaterialIndexMapping materialIndexMapping = default;

            if (blockMapping.TryGetValue(new BlockTypeEquatable(block.blockType), out BlockDefinition blockDefinition))
                materialIndexMapping = blockDefinition.materialIndexMapping;

            BuildUV0(ref vertexData.uv0, materialIndexMapping, includeFaces);
        }


        #region Vertices

        /// <summary>
        /// Builds a single face of a cube.
        /// "fixed(float3* cubeVertices = CubeVertices)" - Pins the managed CubeVertices array and
        /// create a float3 pointer to the memory so it can be accessed faster. This also allows the
        /// managed array to be used in the burst code.
        /// </summary>
        [BurstCompile]
        public static unsafe void BuildVertices(
            // Output
            ref UnsafeList<float3> vertices,
            ref int vertexIndex,
            // Input
            in int3 offsetPosition,
            IncludeFaces includeFaces = IncludeFaces.All)
        {
            fixed(float3* cubeVertices = CubeVertices)
            {
                if ((includeFaces & IncludeFaces.Front) == IncludeFaces.Front)
                    BuildFaces(ref vertices, ref vertexIndex, offsetPosition, cubeVertices,  0);

                if ((includeFaces & IncludeFaces.Back) == IncludeFaces.Back)
                    BuildFaces(ref vertices, ref vertexIndex, offsetPosition, cubeVertices,  4);

                if ((includeFaces & IncludeFaces.Left) == IncludeFaces.Left)
                    BuildFaces(ref vertices, ref vertexIndex, offsetPosition, cubeVertices,  8);

                if ((includeFaces & IncludeFaces.Right) == IncludeFaces.Right)
                    BuildFaces(ref vertices, ref vertexIndex, offsetPosition, cubeVertices, 12);

                if ((includeFaces & IncludeFaces.Up) == IncludeFaces.Up)
                    BuildFaces(ref vertices, ref vertexIndex, offsetPosition, cubeVertices, 16);

                if ((includeFaces & IncludeFaces.Down) == IncludeFaces.Down)
                    BuildFaces(ref vertices, ref vertexIndex, offsetPosition, cubeVertices, 20);
            }

            return;

            void BuildFaces(
                //Output
                ref UnsafeList<float3> vertices,
                ref int vertexIndex,
                //Input
                in int3 offsetPosition,
                float3* cubeVertices,
                int offset)
            {
                for (int i = offset; i < offset + 4; ++i)
                {
                    vertices.Add(offsetPosition + CubeVertices[i]);
                }

                vertexIndex += 4;
            }
        }

        #endregion


        #region Indices

        /// <summary>
        /// Creates the indices for a face and stores them in "ref UnsafeList<int> indices".
        /// </summary>
        /// <param name="vertexIndex">Used to makes sure the indices being built line up with the vertices that have been built.
        /// "vertexIndex += 4;" goes up by four in the BuildFaces() every time a face is built.</param>
        [BurstCompile]
        public static void BuildIndices(
           //Output
           ref UnsafeList<int> indices,
           //Input
           int vertexIndex = 0,
           IncludeFaces includeFaces = IncludeFaces.All)
        {
            // Drawing Order. Draw Clockwise.
            // Triangle 1: bottom-left, top-right, bottom-right (0, 2, 1)
            // Triangle 2; bottom-left, top-left,  top-right    (0, 3, 2)

            int vertexOffset = 0;

            // Forward
            if ((includeFaces & IncludeFaces.Front) == IncludeFaces.Front)
            {
                indices.Add(vertexIndex); indices.Add(vertexIndex + 2); indices.Add(vertexIndex + 1);
                indices.Add(vertexIndex); indices.Add(vertexIndex + 3); indices.Add(vertexIndex + 2);
                vertexOffset += 4;
            }

            // Back
            if ((includeFaces & IncludeFaces.Back) == IncludeFaces.Back)
            {
                indices.Add(vertexIndex + vertexOffset); indices.Add(vertexIndex + (vertexOffset + 2)); indices.Add(vertexIndex + (vertexOffset + 1));
                indices.Add(vertexIndex + vertexOffset); indices.Add(vertexIndex + (vertexOffset + 3)); indices.Add(vertexIndex + (vertexOffset + 2));
                vertexOffset += 4;
            }

            // Left
            if ((includeFaces & IncludeFaces.Left) == IncludeFaces.Left)
            {
                indices.Add(vertexIndex + vertexOffset); indices.Add(vertexIndex + (vertexOffset + 2)); indices.Add(vertexIndex + (vertexOffset + 1));
                indices.Add(vertexIndex + vertexOffset); indices.Add(vertexIndex + (vertexOffset + 3)); indices.Add(vertexIndex + (vertexOffset + 2));
                vertexOffset += 4;
            }

            // Right
            if ((includeFaces & IncludeFaces.Right) == IncludeFaces.Right)
            {
                indices.Add(vertexIndex + vertexOffset); indices.Add(vertexIndex + (vertexOffset + 2)); indices.Add(vertexIndex + (vertexOffset + 1));
                indices.Add(vertexIndex + vertexOffset); indices.Add(vertexIndex + (vertexOffset + 3)); indices.Add(vertexIndex + (vertexOffset + 2));
                vertexOffset += 4;
            }

            // Up
            if ((includeFaces & IncludeFaces.Up) == IncludeFaces.Up)
            {
                indices.Add(vertexIndex + vertexOffset); indices.Add(vertexIndex + (vertexOffset + 2)); indices.Add(vertexIndex + (vertexOffset + 1));
                indices.Add(vertexIndex + vertexOffset); indices.Add(vertexIndex + (vertexOffset + 3)); indices.Add(vertexIndex + (vertexOffset + 2));
                vertexOffset += 4;
            }

            // Down
            if ((includeFaces & IncludeFaces.Down) == IncludeFaces.Down)
            {
                indices.Add(vertexIndex + vertexOffset); indices.Add(vertexIndex + (vertexOffset + 2)); indices.Add(vertexIndex + (vertexOffset + 1));
                indices.Add(vertexIndex + vertexOffset); indices.Add(vertexIndex + (vertexOffset + 3)); indices.Add(vertexIndex + (vertexOffset + 2));
                //vertexOffset += 4;   //Not needed
            }
        }

        #endregion


        #region Normals

        [BurstCompile]
        public static void BuildNormals(
            // Output
            ref UnsafeList<float3> normals,
            // Inputs
            IncludeFaces includeFaces = IncludeFaces.All)
        {
            if ((includeFaces & IncludeFaces.Front) == IncludeFaces.Front)
                BuildNormal(ref normals, new float3( 0,  0,  1));

            if ((includeFaces & IncludeFaces.Back) == IncludeFaces.Back)
                BuildNormal(ref normals, new float3( 0,  0, -1));

            if ((includeFaces & IncludeFaces.Left) == IncludeFaces.Left)
                BuildNormal(ref normals, new float3(-1,  0,  0));

            if ((includeFaces & IncludeFaces.Right) == IncludeFaces.Right)
                BuildNormal(ref normals, new float3( 1,  0,  0));

            if ((includeFaces & IncludeFaces.Up) == IncludeFaces.Up)
                BuildNormal(ref normals, new float3( 0,  1,  0));

            if ((includeFaces & IncludeFaces.Down) == IncludeFaces.Down)
                BuildNormal(ref normals, new float3( 0, -1,  0));

            return;

            void BuildNormal(ref UnsafeList<float3> normals, in float3 normalValue)
            {
                normals.Add(normalValue); // bottom-left
                normals.Add(normalValue); // bottom-right
                normals.Add(normalValue); // top-right
                normals.Add(normalValue); // top-left
            }
        }

        #endregion


        #region Tangents

        [BurstCompile]
        public static void BuildTangents(
            // Output
            ref UnsafeList<float4> tangents,
            // Input
            IncludeFaces includeFaces = IncludeFaces.All)
        {
            if ((includeFaces & IncludeFaces.Front) == IncludeFaces.Front)
                BuildTangent(ref tangents, new float4(1, 0, 0, 1));

            if ((includeFaces & IncludeFaces.Back) == IncludeFaces.Back)
                BuildTangent(ref tangents, new float4(-1, 0, 0, -1));
            
            if ((includeFaces & IncludeFaces.Left) == IncludeFaces.Left)
                BuildTangent(ref tangents, new float4(0, 0, -1, -1));
            
            if ((includeFaces & IncludeFaces.Right) == IncludeFaces.Right)
                BuildTangent(ref tangents, new float4(0, 0, 1, 1));
            
            if ((includeFaces & IncludeFaces.Up) == IncludeFaces.Up)
                BuildTangent(ref tangents, new float4(1, 0, 0, 1));
            
            if ((includeFaces & IncludeFaces.Down) == IncludeFaces.Down)
                BuildTangent(ref tangents, new float4(1, 0, 0, 1));

            return;

            void BuildTangent(ref UnsafeList<float4> tangents, in float4 tangentValue)
            {
                tangents.Add(tangentValue); // Bottom-left
                tangents.Add(tangentValue); // Bottom-right
                tangents.Add(tangentValue); // Top-right
                tangents.Add(tangentValue); // Top-left
            }
        }

        #endregion


        #region UVs

        [BurstCompile]
        public static unsafe void BuildUV0(
            //Output
            ref UnsafeList<float3> uv0,
            //Input
            in MaterialIndexMapping materialIndexMapping,
            IncludeFaces includeFaces = IncludeFaces.All)
        {
            fixed (float2* cubeUVs = CubeUVs)
            fixed (IncludeFaces* faces = IncludeFacesUtils.IncludeFacesArray)
            {
                for (int i = 0; i < 6; i++)
                {
                    int materialIndex = materialIndexMapping.GetMaterialIndex((Direction) i);

                    IncludeFaces face = faces[i];

                    if ((includeFaces & face) == face)
                    {
                        for (int j = 0; j < 4; j++)
                        {
                            uv0.Add(new float3(cubeUVs[j].x, cubeUVs[j].y, materialIndex));
                        }
                    }
                }
            }
        }

        #endregion
    }
}
