namespace VoxelEngine.Jobs
{
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using VoxelEngine.Core;
    using VoxelEngine.Utilities;

    // I used a pointer here so that the chunk is not copied into the job.
    // Because of this you have to use [NativeDisableUnsafePtrRestriction] to allow
    // the use of pointers in a job as it is usually forbidden due to no safely checks. 

    /// <summary>
    /// Builds all the blocks vertex data.
    /// </summary>
    [BurstCompile]
    public struct BuildBlocksJob : IJob
    {
        //Output
        [NativeDisableUnsafePtrRestriction]
        public unsafe Chunk* chunk;

        //Input
        [ReadOnly]
        public NativeHashMap<BlockTypeEquatable, BlockDefinition> blockMapping;

        public unsafe void Execute()
        {
            //Build the chunk
            int vertexIndex = 0;
            for (int i = 0; i < chunk->blocks.Length; i++)
            {
                //Current block
                Block block = chunk->blocks[i];

                //Skip if empty
                if (block.blockType == BlockType.Empty) continue;

                //Check if there is a solid block in all six adjacent directions.
                //If so, remove building that face in that direction.
                IncludeFaces includeFaces = IncludeFaces.All;
                if (chunk->CheckAdjacentBlockIsSolid(Direction.Forward, block.position))
                    includeFaces &= ~IncludeFaces.Front;

                if (chunk->CheckAdjacentBlockIsSolid(Direction.Backward, block.position))
                    includeFaces &= ~IncludeFaces.Back;
                
                if (chunk->CheckAdjacentBlockIsSolid(Direction.Left, block.position))
                    includeFaces &= ~IncludeFaces.Left;
                
                if (chunk->CheckAdjacentBlockIsSolid(Direction.Right, block.position))
                    includeFaces &= ~IncludeFaces.Right;
                
                if (chunk->CheckAdjacentBlockIsSolid(Direction.Up, block.position))
                    includeFaces &= ~IncludeFaces.Up;
                
                if (chunk->CheckAdjacentBlockIsSolid(Direction.Down, block.position))
                    includeFaces &= ~IncludeFaces.Down;

                //Skip if not building any faces.
                if (includeFaces == IncludeFaces.None) continue;

                //Build block mesh
                CubeBuilderUtils.BuildCube24(ref chunk->meshVertexData, ref vertexIndex,
                   block, ref blockMapping, includeFaces);
            }
        }
    }
}
