namespace VoxelEngine.Jobs
{
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Mathematics;
    using VoxelEngine.Core;

    /// <summary>
    /// Initializes the blocks with essential data like the position of the block.
    /// </summary>
    [BurstCompile]
    public struct InitializeBlocksJob : IJob
    {
        //Output
        [WriteOnly]
        public UnsafeList<Block> blocks;

        public void Execute()
        {
            for (int y = 0; y < Chunk.Height; y++)
            {
                for (int z = 0; z < Chunk.Width; z++)
                {
                    for (int x = 0; x < Chunk.Height; x++)
                    {
                        int blockIndex = Chunk.GetBlockIndex(x, y, z);

                        // Create and Initialize the new block.
                        blocks[blockIndex] = new Block
                        {
                            position = new int3(x, y, z),
                        };
                    }
                }
            }
        }
    }
}
