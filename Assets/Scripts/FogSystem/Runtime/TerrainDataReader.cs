using UnityEngine;
using System.IO;

namespace FogSystem
{
    /// <summary>
    /// 地形类型枚举
    /// </summary>
    public enum TerrainType
    {
        Plain = 0,      // 平原
        Hill = 1,       // 丘陵 
        Mountain = 2,   // 山脉
        Lake = 3,       // 湖泊
        Forest = 4,     // 森林
        Unlocked = 5    // 已解锁区域（下沉到0高度）
    }

    /// <summary>
    /// 地形角点数据结构（简化版本）
    /// </summary>
    [System.Serializable]
    public struct TerrainVertex
    {
        public float height;
    }

    /// <summary>
    /// 假数据配置
    /// </summary>
    [System.Serializable]
    public class TerrainDataConfig
    {
        public int seed = 12345;
        public float noiseScale = 0.01f;
        public float mountainHeight = 12f;
        public float hillHeight = 12f;
        public float plainHeight = 12f;
        public float lakeDepth = 12f;
        public float forestHeight = 12f;
        
        // 地形类型分布权重
        public float plainWeight = 0.4f;
        public float hillWeight = 0.25f;
        public float mountainWeight = 0.15f;
        public float lakeWeight = 0.1f;
        public float forestWeight = 0.1f;
    }

    /// <summary>
    /// 地形数据读取器 - 静态类，基于角点的数据存储
    /// 存储地形角点数据，支持高效的顶点高度查询
    /// </summary>
    public static class TerrainDataReader
    {
        private static TerrainVertex[,] vertexData;  // 角点数据数组
        private static float mapWidth;      // 地图总宽度（米）
        private static float mapHeight;     // 地图总高度（米）
        private static float dataCellSize;  // 数据格子大小（米）
        private static int dataGridCountX;  // 数据格子数量X（cell数量）
        private static int dataGridCountZ;  // 数据格子数量Z（cell数量）
        private static int vertexCountX;    // 角点数量X（比格子数多1）
        private static int vertexCountZ;    // 角点数量Z（比格子数多1）
        private static TerrainDataConfig config;
        
        /// <summary>
        /// 初始化并生成所有地形角点数据
        /// </summary>
        /// <param name="mapWidth">地图总宽度（米）</param>
        /// <param name="mapHeight">地图总高度（米）</param>
        /// <param name="dataCellSize">数据格子大小（米）</param>
        public static void Initialize(float mapWidth, float mapHeight, float dataCellSize)
        {
            TerrainDataReader.mapWidth = mapWidth;
            TerrainDataReader.mapHeight = mapHeight;
            TerrainDataReader.dataCellSize = dataCellSize;
            
            // 根据地图总尺寸和数据格子大小计算实际数据格子数量
            TerrainDataReader.dataGridCountX = Mathf.CeilToInt(mapWidth / dataCellSize);
            TerrainDataReader.dataGridCountZ = Mathf.CeilToInt(mapHeight / dataCellSize);
            
            // 角点数量比格子数量多1（因为格子的边界需要角点）
            TerrainDataReader.vertexCountX = dataGridCountX + 1;
            TerrainDataReader.vertexCountZ = dataGridCountZ + 1;
            
            // 加载配置数据
            LoadConfigData();
            
            // 初始化角点数据数组
            vertexData = new TerrainVertex[vertexCountX, vertexCountZ];
            
            // 生成所有角点数据
            GenerateAllVertexData();
            
            Debug.Log($"TerrainDataReader: 地图尺寸 {mapWidth}x{mapHeight}m，数据精度 {dataCellSize}m，生成了 {dataGridCountX}x{dataGridCountZ} 个格子，{vertexCountX}x{vertexCountZ} 个角点");
        }
        
        /// <summary>
        /// 加载配置数据（从假数据文件）
        /// </summary>
        private static void LoadConfigData()
        {
            string configPath = Path.Combine(Application.streamingAssetsPath, "terrain_config.json");
            
            // 如果文件不存在，创建默认配置并保存
            if (!File.Exists(configPath))
            {
                config = new TerrainDataConfig();
                CreateDefaultConfigFile(configPath);
                Debug.Log("TerrainDataReader: 创建了默认配置文件");
            }
            else
            {
                try
                {
                    string jsonData = File.ReadAllText(configPath);
                    config = JsonUtility.FromJson<TerrainDataConfig>(jsonData);
                    Debug.Log("TerrainDataReader: 成功加载配置文件");
                }
                catch
                {
                    config = new TerrainDataConfig();
                    Debug.LogWarning("TerrainDataReader: 配置文件读取失败，使用默认配置");
                }
            }
        }
        
        /// <summary>
        /// 创建默认配置文件
        /// </summary>
        private static void CreateDefaultConfigFile(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string jsonData = JsonUtility.ToJson(config, true);
                File.WriteAllText(path, jsonData);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"TerrainDataReader: 无法创建配置文件 - {e.Message}");
            }
        }
        
        /// <summary>
        /// 生成所有角点数据
        /// </summary>
        private static void GenerateAllVertexData()
        {
            Random.InitState(config.seed);
            
            // 第一步：生成所有角点的随机高度（不考虑解锁状态）
            for (int x = 0; x < vertexCountX; x++)
            {
                for (int z = 0; z < vertexCountZ; z++)
                {
                    vertexData[x, z] = GenerateRandomHeightVertex(x, z);
                }
            }
            
            // 第二步：处理解锁区域，将解锁格子的四个角点高度设为0
            for (int cellX = 0; cellX < dataGridCountX; cellX++)
            {
                for (int cellZ = 0; cellZ < dataGridCountZ; cellZ++)
                {
                    if (IsCellUnlocked(cellX, cellZ))
                    {
                        // 将格子的四个角点高度设为0
                        SetVertexHeight(cellX, cellZ, 0f);         // 左下
                        SetVertexHeight(cellX + 1, cellZ, 0f);     // 右下
                        SetVertexHeight(cellX + 1, cellZ + 1, 0f); // 右上
                        SetVertexHeight(cellX, cellZ + 1, 0f);     // 左上
                    }
                }
            }
        }
        
        /// <summary>
        /// 生成随机高度的角点（第一步：不考虑解锁状态）
        /// </summary>
        private static TerrainVertex GenerateRandomHeightVertex(int x, int z)
        {
            TerrainVertex vertex = new TerrainVertex();
            
            // 生成基础噪声值
            float noiseX = x * config.noiseScale;
            float noiseZ = z * config.noiseScale;
            float noiseValue = Mathf.PerlinNoise(noiseX, noiseZ);
            
            // 根据噪声值生成高度变化（基础高度12，添加±2的高度浮动）
            float heightVariation = (noiseValue - 0.5f) * 4f; // -2 to 2
            vertex.height = 5f + heightVariation;
            
            return vertex;
        }
        
        /// <summary>
        /// 判断指定格子是否解锁
        /// </summary>
        private static bool IsCellUnlocked(int cellX, int cellZ)
        {
            // 使用独立的噪声判断格子是否解锁
            float unlockNoise = Mathf.PerlinNoise(cellX * 0.1f + 10f, cellZ * 0.1f + 10f);
            return unlockNoise > 0.6f; // 约40%的区域解锁
        }
        
        /// <summary>
        /// 设置指定角点的高度
        /// </summary>
        private static void SetVertexHeight(int x, int z, float height)
        {
            if (x >= 0 && x < vertexCountX && z >= 0 && z < vertexCountZ)
            {
                var vertex = vertexData[x, z];
                vertex.height = height;
                vertexData[x, z] = vertex;
            }
        }
        
        /// <summary>
        /// 获取指定角点坐标的角点数据
        /// </summary>
        public static TerrainVertex GetVertex(int x, int z)
        {
            if (x < 0 || x >= vertexCountX || z < 0 || z >= vertexCountZ)
                return new TerrainVertex { height = 0f };
                
            return vertexData[x, z];
        }
        
        /// <summary>
        /// 获取指定角点坐标的高度
        /// </summary>
        public static float GetVertexHeight(int x, int z)
        {
            return GetVertex(x, z).height;
        }
        
        /// <summary>
        /// 根据世界坐标获取最近角点的高度
        /// </summary>
        public static float GetHeightAtWorldPos(Vector3 worldPos)
        {
            int x = Mathf.RoundToInt(worldPos.x / dataCellSize);
            int z = Mathf.RoundToInt(worldPos.z / dataCellSize);
            return GetVertexHeight(x, z);
        }
        
        /// <summary>
        /// 高效获取指定格子的四个角点高度（专为FogSystem优化）
        /// </summary>
        /// <param name="cellX">格子X坐标</param>
        /// <param name="cellZ">格子Z坐标</param>
        /// <param name="bottomLeft">左下角点高度</param>
        /// <param name="bottomRight">右下角点高度</param>
        /// <param name="topRight">右上角点高度</param>
        /// <param name="topLeft">左上角点高度</param>
        /// <returns>是否成功获取</returns>
        public static bool GetCellCornerHeights(int cellX, int cellZ, out float bottomLeft, out float bottomRight, out float topRight, out float topLeft)
        {
            if (cellX < 0 || cellX >= dataGridCountX || cellZ < 0 || cellZ >= dataGridCountZ)
            {
                bottomLeft = bottomRight = topRight = topLeft = 0f;
                return false;
            }
            
            // 直接获取四个角点的高度
            bottomLeft = GetVertexHeight(cellX, cellZ);
            bottomRight = GetVertexHeight(cellX + 1, cellZ);
            topRight = GetVertexHeight(cellX + 1, cellZ + 1);
            topLeft = GetVertexHeight(cellX, cellZ + 1);
            
            return true;
        }
        
        /// <summary>
        /// 解锁指定格子（将其四个角点高度设为0）
        /// </summary>
        public static void UnlockCell(int cellX, int cellZ)
        {
            if (cellX >= 0 && cellX < dataGridCountX && cellZ >= 0 && cellZ < dataGridCountZ)
            {
                SetVertexHeight(cellX, cellZ, 0f);         // 左下
                SetVertexHeight(cellX + 1, cellZ, 0f);     // 右下
                SetVertexHeight(cellX + 1, cellZ + 1, 0f); // 右上
                SetVertexHeight(cellX, cellZ + 1, 0f);     // 左上
            }
        }
        
        /// <summary>
        /// 解锁指定区域内的所有格子
        /// </summary>
        public static void UnlockArea(Vector3 centerWorldPos, float radius)
        {
            // 转换为格子坐标
            int centerCellX = Mathf.RoundToInt(centerWorldPos.x / dataCellSize);
            int centerCellZ = Mathf.RoundToInt(centerWorldPos.z / dataCellSize);
            int radiusCells = Mathf.RoundToInt(radius / dataCellSize);
            
            for (int cellX = centerCellX - radiusCells; cellX <= centerCellX + radiusCells; cellX++)
            {
                for (int cellZ = centerCellZ - radiusCells; cellZ <= centerCellZ + radiusCells; cellZ++)
                {
                    float distance = Vector2.Distance(new Vector2(cellX, cellZ), new Vector2(centerCellX, centerCellZ));
                    if (distance <= radiusCells)
                    {
                        UnlockCell(cellX, cellZ);
                    }
                }
            }
        }
        
        // 公共属性
        public static float MapWidth => mapWidth;
        public static float MapHeight => mapHeight;
        public static float DataCellSize => dataCellSize;
        public static int DataGridCountX => dataGridCountX;  // 格子数量
        public static int DataGridCountZ => dataGridCountZ;  // 格子数量
        public static int VertexCountX => vertexCountX;      // 角点数量
        public static int VertexCountZ => vertexCountZ;      // 角点数量
        public static TerrainDataConfig Config => config;
    }
} 