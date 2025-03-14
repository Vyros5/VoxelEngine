namespace VoxelEngine
{
    using System;
    using Unity.Collections;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;
    using UnityEngine.Rendering;
    using Utilities;
    using VoxelEngine.Core;
    using VoxelEngine.Jobs;

    public class VoxelTester : MonoBehaviour
    {
        [SerializeField]
        private MeshFilter meshFilter;

        private void Start()
        {
            //Create the chunk
            Chunk chunk = new(0, 0, Allocator.Persistent);

            //Initialize the chunk.
            InitializeBlocksJob initializeBlocksJob = new()
            {
                //Output
                blocks = chunk.blocks,
            };

            JobHandle initializeBlocksJobHandle = initializeBlocksJob.Schedule();
            initializeBlocksJobHandle.Complete();


            //Fill the chunk.
            FillBlocksJob fillBlocksJob = new()
            {
                //Output
                blocks = chunk.blocks,
                //Input
                blockType = BlockType.Dirt,
            };

            JobHandle fillBlocksJobHandle = fillBlocksJob.Schedule();
            fillBlocksJobHandle.Complete();


            unsafe
            {
                //Build the chunk
                BuildBlocksJob buildBlocksJob = new()
                {
                    //Output
                    chunk = &chunk,
                    //meshVertexData = &chunk.meshVertexData,
                    //Input
                    //blocks = chunk.blocks,
                    blockMapping = BlockMapping.blockMapping,
                };

                JobHandle buildBlocksJobHandle = buildBlocksJob.Schedule();
                buildBlocksJobHandle.Complete();
            }

            Mesh createdMesh = CreateMesh(ref chunk.meshVertexData);
            meshFilter.mesh = createdMesh;

            chunk.Dispose();
        }


        private Mesh CreateMesh(ref VertexData vertexData)
        {
            Mesh mesh = new()
            {
                indexFormat = IndexFormat.UInt32,

                vertices  = UnsafeUtils.ConvertToArray(vertexData.vertices),
                triangles = UnsafeUtils.ConvertToArray(vertexData.indices),
                normals   = UnsafeUtils.ConvertToArray(vertexData.normals),
                tangents  = UnsafeUtils.ConvertToArray(vertexData.tangents),
            };

            mesh.SetUVs(0, UnsafeUtils.ConvertToArray(vertexData.uv0));

            mesh.SetSubMesh(0, new SubMeshDescriptor(0, vertexData.indices.Length), MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontNotifyMeshUsers);

            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
