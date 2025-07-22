using UnityEngine;
using System.Collections.Generic;

namespace FogSystem
{
    /// <summary>
    /// 单个迷雾Mesh生成器
    /// 负责生成单个mesh块的所有逻辑，直接基于TerrainDataReader的角点数据
    /// </summary>
    public static class SingleFogMeshGenerator
    {
        /// <summary>
        /// 生成单个mesh块的数据
        /// </summary>
        /// <param name="mesh">要生成的mesh对象</param>
        /// <param name="startGridX">起始格子X坐标</param>
        /// <param name="startGridZ">起始格子Z坐标</param>
        /// <param name="blockGridCountX">块内格子数量X</param>
        /// <param name="blockGridCountZ">块内格子数量Z</param>
        /// <param name="cellSize">格子大小</param>
        public static void GenerateMeshBlock(Mesh mesh, int startGridX, int startGridZ, 
            int blockGridCountX, int blockGridCountZ, float cellSize)
        {
            // 计算顶点数量：每个格子需要4个顶点，但相邻格子共享顶点
            int vertexCountX = blockGridCountX + 1;
            int vertexCountZ = blockGridCountZ + 1;
            
            // 存储顶点、UV和颜色数据
            List<Vector3> verticesList = new List<Vector3>();
            List<Vector2> uvsList = new List<Vector2>();
            List<Color> colorsList = new List<Color>();
            List<int> trianglesList = new List<int>();
            
            // 生成所有顶点数据
            for (int localZ = 0; localZ <= blockGridCountZ; localZ++)
            {
                for (int localX = 0; localX <= blockGridCountX; localX++)
                {
                    // 计算全局角点坐标
                    int globalVertexX = startGridX + localX;
                    int globalVertexZ = startGridZ + localZ;
                    
                    // 计算世界坐标
                    float worldX = globalVertexX * cellSize;
                    float worldZ = globalVertexZ * cellSize;
                    
                    // 从TerrainDataReader获取角点高度
                    float height = TerrainDataReader.GetVertexHeight(globalVertexX, globalVertexZ);
                    
                    // 判断是否解锁（高度为0表示解锁）
                    bool isUnlocked = Mathf.Approximately(height, 0f);
                    
                    // 添加顶点数据
                    verticesList.Add(new Vector3(worldX, height, worldZ));
                    uvsList.Add(new Vector2((float)localX / blockGridCountX, (float)localZ / blockGridCountZ));
                    colorsList.Add(GetVertexColor(isUnlocked));
                }
            }
            
            // 生成三角形（基于格子的解锁状态）
            for (int localZ = 0; localZ < blockGridCountZ; localZ++)
            {
                for (int localX = 0; localX < blockGridCountX; localX++)
                {
                    // 检查当前格子的四个角点解锁状态
                    bool shouldGenerateTriangles = ShouldGenerateTrianglesForCell(
                        startGridX + localX, startGridZ + localZ);
                    
                    if (shouldGenerateTriangles)
                    {
                        // 计算四个顶点的索引
                        int bottomLeft = localZ * vertexCountX + localX;
                        int bottomRight = bottomLeft + 1;
                        int topLeft = (localZ + 1) * vertexCountX + localX;
                        int topRight = topLeft + 1;
                        
                        // 生成两个三角形
                        GenerateStandardTriangles(trianglesList, bottomLeft, bottomRight, topLeft, topRight);
                    }
                }
            }
            
            // 应用到mesh
            mesh.Clear();
            mesh.vertices = verticesList.ToArray();
            mesh.triangles = trianglesList.ToArray();
            mesh.uv = uvsList.ToArray();
            mesh.colors = colorsList.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
        
        /// <summary>
        /// 判断指定格子是否应该生成三角形
        /// </summary>
        private static bool ShouldGenerateTrianglesForCell(int cellX, int cellZ)
        {
            // 获取格子四个角点的高度
            float bottomLeft, bottomRight, topRight, topLeft;
            if (!TerrainDataReader.GetCellCornerHeights(cellX, cellZ, out bottomLeft, out bottomRight, out topRight, out topLeft))
            {
                return true; // 如果获取失败，默认生成三角形
            }
            
            // 统计解锁的角点数量（高度为0表示解锁）
            int unlockedCorners = 0;
            if (Mathf.Approximately(bottomLeft, 0f)) unlockedCorners++;
            if (Mathf.Approximately(bottomRight, 0f)) unlockedCorners++;
            if (Mathf.Approximately(topRight, 0f)) unlockedCorners++;
            if (Mathf.Approximately(topLeft, 0f)) unlockedCorners++;
            
            // 如果四个角点都解锁，不生成三角形（创建空洞）
            return unlockedCorners < 4;
        }
        
        /// <summary>
        /// 生成标准的两个三角形（一个格子）
        /// </summary>
        private static void GenerateStandardTriangles(List<int> trianglesList, 
            int bottomLeft, int bottomRight, int topLeft, int topRight)
        {
            // 第一个三角形（逆时针：左下-左上-右下）
            trianglesList.Add(bottomLeft);
            trianglesList.Add(topLeft);
            trianglesList.Add(bottomRight);
            
            // 第二个三角形（逆时针：左上-右上-右下）
            trianglesList.Add(topLeft);
            trianglesList.Add(topRight);
            trianglesList.Add(bottomRight);
        }
        
        /// <summary>
        /// 根据解锁状态获取顶点颜色
        /// </summary>
        private static Color GetVertexColor(bool isUnlocked)
        {
            // 根据解锁状态设置Vector2值
            Vector2 unlockState;
            if (isUnlocked)
            {
                unlockState = new Vector2(0f, 1f); // 已解锁: (0, 1)
            }
            else
            {
                unlockState = new Vector2(1f, 1f); // 未解锁: (1, 1)
            }
            
            // 将Vector2信息编码到Color的r,g通道中，b,a通道设为0
            return new Color(unlockState.x, unlockState.y, 0f, 0f);
        }
    }
}
