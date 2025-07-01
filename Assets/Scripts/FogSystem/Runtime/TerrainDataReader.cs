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
    /// 地形角点数据结构
    /// </summary>
    [System.Serializable]
    public struct TerrainVertex
    {
        public float height;
        public TerrainType terrainType;
        public bool isUnlocked;
        public float noiseValue;  // 用于额外的随机性
    }

    /// <summary>
    /// 地形格子数据结构（基于四个角点计算）
    /// </summary>
    [System.Serializable]
    public struct TerrainCell
    {
        public float height;
        public TerrainType terrainType;
        public bool isUnlocked;
        public float noiseValue;  // 用于额外的随机性
        
        // 四个角点的引用信息
        public TerrainVertex bottomLeft;
        public TerrainVertex bottomRight;
        public TerrainVertex topRight;
        public TerrainVertex topLeft;
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
    /// 地形数据读取器 - 基于角点的数据存储
    /// 存储地形角点数据，支持高效的cell查询
    /// </summary>
    public class TerrainDataReader
    {
        private TerrainVertex[,] vertexData;  // 角点数据数组
        private float mapWidth;      // 地图总宽度（米）
        private float mapHeight;     // 地图总高度（米）
        private float dataCellSize;  // 数据格子大小（米）
        private int dataGridCountX;  // 数据格子数量X（cell数量）
        private int dataGridCountZ;  // 数据格子数量Z（cell数量）
        private int vertexCountX;    // 角点数量X（比格子数多1）
        private int vertexCountZ;    // 角点数量Z（比格子数多1）
        private TerrainDataConfig config;
        
        /// <summary>
        /// 初始化并生成所有地形角点数据
        /// </summary>
        /// <param name="mapWidth">地图总宽度（米）</param>
        /// <param name="mapHeight">地图总高度（米）</param>
        /// <param name="dataCellSize">数据格子大小（米）</param>
        public void Initialize(float mapWidth, float mapHeight, float dataCellSize)
        {
            this.mapWidth = mapWidth;
            this.mapHeight = mapHeight;
            this.dataCellSize = dataCellSize;
            
            // 根据地图总尺寸和数据格子大小计算实际数据格子数量
            this.dataGridCountX = Mathf.CeilToInt(mapWidth / dataCellSize);
            this.dataGridCountZ = Mathf.CeilToInt(mapHeight / dataCellSize);
            
            // 角点数量比格子数量多1（因为格子的边界需要角点）
            this.vertexCountX = dataGridCountX + 1;
            this.vertexCountZ = dataGridCountZ + 1;
            
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
        private void LoadConfigData()
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
        private void CreateDefaultConfigFile(string path)
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
        private void GenerateAllVertexData()
        {
            Random.InitState(config.seed);
            
            for (int x = 0; x < vertexCountX; x++)
            {
                for (int z = 0; z < vertexCountZ; z++)
                {
                    vertexData[x, z] = GenerateTerrainVertex(x, z);
                }
            }
        }
        
        /// <summary>
        /// 生成单个角点数据
        /// </summary>
        private TerrainVertex GenerateTerrainVertex(int x, int z)
        {
            TerrainVertex vertex = new TerrainVertex();
            
            // 生成基础噪声值
            float noiseX = x * config.noiseScale;
            float noiseZ = z * config.noiseScale;
            vertex.noiseValue = Mathf.PerlinNoise(noiseX, noiseZ);
            
            // 首先判断是否为解锁区域
            float unlockNoise = Mathf.PerlinNoise(x * 0.02f + 1000f, z * 0.02f + 1000f);
            bool isUnlockedArea = unlockNoise > 0.6f; // 约40%的区域解锁
            
            if (isUnlockedArea)
            {
                // 解锁区域直接设置为Unlocked类型
                vertex.terrainType = TerrainType.Unlocked;
                vertex.isUnlocked = true;
            }
            else
            {
                // 未解锁区域确定地形类型
                vertex.terrainType = DetermineTerrainType(x, z, vertex.noiseValue);
                vertex.isUnlocked = false;
            }
            
            // 根据地形类型确定高度
            vertex.height = CalculateHeight(vertex.terrainType, vertex.noiseValue, x, z);
            
            return vertex;
        }
        
        /// <summary>
        /// 确定地形类型
        /// </summary>
        private TerrainType DetermineTerrainType(int x, int z, float noiseValue)
        {
            // 使用额外的噪声来确定地形类型
            float typeNoise = Mathf.PerlinNoise(x * 0.005f, z * 0.005f);
            
            // 根据权重分布确定地形类型
            if (typeNoise < config.lakeWeight)
                return TerrainType.Lake;
            else if (typeNoise < config.lakeWeight + config.mountainWeight)
                return TerrainType.Mountain;
            else if (typeNoise < config.lakeWeight + config.mountainWeight + config.forestWeight)
                return TerrainType.Forest;
            else if (typeNoise < config.lakeWeight + config.mountainWeight + config.forestWeight + config.hillWeight)
                return TerrainType.Hill;
            else
                return TerrainType.Plain;
        }
        
        /// <summary>
        /// 计算地形高度
        /// </summary>
        private float CalculateHeight(TerrainType terrainType, float noiseValue, int x, int z)
        {
            switch (terrainType)
            {
                case TerrainType.Plain:
                case TerrainType.Hill:
                case TerrainType.Mountain:
                case TerrainType.Lake:
                case TerrainType.Forest:
                    // 非解锁地形基础高度为12，添加±2的高度浮动
                    float heightVariation = (noiseValue - 0.5f) * 4f; // -2 to 2
                    return 12f + heightVariation;
                case TerrainType.Unlocked:
                    return 0f; // 解锁区域固定高度为0
                default:
                    return 12f;
            }
        }
        
        /// <summary>
        /// 获取指定角点坐标的角点数据
        /// </summary>
        public TerrainVertex GetVertex(int x, int z)
        {
            if (x < 0 || x >= vertexCountX || z < 0 || z >= vertexCountZ)
                return new TerrainVertex { height = 0f, terrainType = TerrainType.Plain, isUnlocked = false };
                
            return vertexData[x, z];
        }
        
        /// <summary>
        /// 获取指定数据格子坐标的地形数据（通过四个角点计算）
        /// </summary>
        public TerrainCell GetTerrainCell(int x, int z)
        {
            if (x < 0 || x >= dataGridCountX || z < 0 || z >= dataGridCountZ)
                return new TerrainCell { height = 0f, terrainType = TerrainType.Plain, isUnlocked = false };
            
            // 获取格子的四个角点（左下、右下、右上、左上）
            TerrainVertex bottomLeft = GetVertex(x, z);
            TerrainVertex bottomRight = GetVertex(x + 1, z);
            TerrainVertex topRight = GetVertex(x + 1, z + 1);
            TerrainVertex topLeft = GetVertex(x, z + 1);
            
            // 基于四个角点构建TerrainCell
            return ConstructTerrainCell(bottomLeft, bottomRight, topRight, topLeft);
        }
        
        /// <summary>
        /// 基于四个角点构建地形格子数据
        /// </summary>
        private TerrainCell ConstructTerrainCell(TerrainVertex bottomLeft, TerrainVertex bottomRight, TerrainVertex topRight, TerrainVertex topLeft)
        {
            TerrainCell cell = new TerrainCell();
            
            // 存储四个角点数据
            cell.bottomLeft = bottomLeft;
            cell.bottomRight = bottomRight;
            cell.topRight = topRight;
            cell.topLeft = topLeft;
            
            // 计算cell的综合属性（取平均值或主导属性）
            cell.height = (bottomLeft.height + bottomRight.height + topRight.height + topLeft.height) / 4f;
            cell.noiseValue = (bottomLeft.noiseValue + bottomRight.noiseValue + topRight.noiseValue + topLeft.noiseValue) / 4f;
            
            // 解锁状态：四个角点都解锁才算解锁
            cell.isUnlocked = bottomLeft.isUnlocked && bottomRight.isUnlocked && topRight.isUnlocked && topLeft.isUnlocked;
            
            // 地形类型：取主导类型（简单起见，取左下角的类型）
            cell.terrainType = bottomLeft.terrainType;
            
            return cell;
        }
        
        /// <summary>
        /// 获取指定数据格子坐标的高度
        /// </summary>
        public float GetHeight(int x, int z)
        {
            TerrainCell cell = GetTerrainCell(x, z);
            return cell.height;
        }
        
        /// <summary>
        /// 设置指定角点坐标的解锁状态
        /// </summary>
        public void SetVertexUnlocked(int x, int z, bool unlocked)
        {
            if (x < 0 || x >= vertexCountX || z < 0 || z >= vertexCountZ)
                return;
                
            vertexData[x, z].isUnlocked = unlocked;
        }
        
        /// <summary>
        /// 检查指定角点坐标是否已解锁
        /// </summary>
        public bool IsVertexUnlocked(int x, int z)
        {
            if (x < 0 || x >= vertexCountX || z < 0 || z >= vertexCountZ)
                return false;
                
            return vertexData[x, z].isUnlocked;
        }
        
        /// <summary>
        /// 根据世界坐标获取高度
        /// </summary>
        public float GetHeightAtWorldPos(Vector3 worldPos)
        {
            int x = Mathf.RoundToInt(worldPos.x / dataCellSize);
            int z = Mathf.RoundToInt(worldPos.z / dataCellSize);
            return GetHeight(x, z);
        }
        
        /// <summary>
        /// 根据世界坐标获取地形数据
        /// </summary>
        public TerrainCell GetTerrainCellAtWorldPos(Vector3 worldPos)
        {
            int x = Mathf.RoundToInt(worldPos.x / dataCellSize);
            int z = Mathf.RoundToInt(worldPos.z / dataCellSize);
            return GetTerrainCell(x, z);
        }
        
        /// <summary>
        /// 根据任意精度格子坐标获取角点数据（用于不同精度查询）
        /// </summary>
        /// <param name="gridX">查询精度下的格子X坐标</param>
        /// <param name="gridZ">查询精度下的格子Z坐标</param>
        /// <param name="queryCellSize">查询使用的格子大小</param>
        /// <returns>对应的角点数据</returns>
        public TerrainVertex GetVertexAtGrid(int gridX, int gridZ, float queryCellSize)
        {
            // 将查询格子坐标转换为世界坐标
            float worldX = gridX * queryCellSize;
            float worldZ = gridZ * queryCellSize;
            
            // 转换为角点坐标
            float vertexX = worldX / dataCellSize;
            float vertexZ = worldZ / dataCellSize;
            
            // 使用最近邻采样
            int nearestX = Mathf.RoundToInt(vertexX);
            int nearestZ = Mathf.RoundToInt(vertexZ);
            
            return GetVertex(nearestX, nearestZ);
        }
        
        /// <summary>
        /// 根据任意精度格子坐标获取地形数据（用于不同精度查询）
        /// </summary>
        /// <param name="gridX">查询精度下的格子X坐标</param>
        /// <param name="gridZ">查询精度下的格子Z坐标</param>
        /// <param name="queryCellSize">查询使用的格子大小</param>
        /// <returns>插值后的地形数据</returns>
        public TerrainCell GetTerrainCellAtGrid(int gridX, int gridZ, float queryCellSize)
        {
            // 将查询格子坐标转换为世界坐标
            float worldX = gridX * queryCellSize;
            float worldZ = gridZ * queryCellSize;
            
            // 转换为数据格子坐标
            float dataX = worldX / dataCellSize;
            float dataZ = worldZ / dataCellSize;
            
            // 使用最近邻采样（可以后续改为双线性插值）
            int nearestX = Mathf.RoundToInt(dataX);
            int nearestZ = Mathf.RoundToInt(dataZ);
            
            return GetTerrainCell(nearestX, nearestZ);
        }
        
        /// <summary>
        /// 高效获取指定格子的四个角点数据（专为FogSystem优化）
        /// </summary>
        /// <param name="cellX">格子X坐标</param>
        /// <param name="cellZ">格子Z坐标</param>
        /// <param name="bottomLeft">左下角点</param>
        /// <param name="bottomRight">右下角点</param>
        /// <param name="topRight">右上角点</param>
        /// <param name="topLeft">左上角点</param>
        /// <returns>是否成功获取</returns>
        public bool GetCellCorners(int cellX, int cellZ, out TerrainVertex bottomLeft, out TerrainVertex bottomRight, out TerrainVertex topRight, out TerrainVertex topLeft)
        {
            if (cellX < 0 || cellX >= dataGridCountX || cellZ < 0 || cellZ >= dataGridCountZ)
            {
                bottomLeft = bottomRight = topRight = topLeft = new TerrainVertex { height = 0f, terrainType = TerrainType.Plain, isUnlocked = false };
                return false;
            }
            
            // 直接获取四个角点
            bottomLeft = GetVertex(cellX, cellZ);
            bottomRight = GetVertex(cellX + 1, cellZ);
            topRight = GetVertex(cellX + 1, cellZ + 1);
            topLeft = GetVertex(cellX, cellZ + 1);
            
            return true;
        }
        
        /// <summary>
        /// 解锁指定区域（基于角点）
        /// </summary>
        public void UnlockArea(Vector3 centerWorldPos, float radius)
        {
            // 转换为角点坐标
            int centerX = Mathf.RoundToInt(centerWorldPos.x / dataCellSize);
            int centerZ = Mathf.RoundToInt(centerWorldPos.z / dataCellSize);
            int radiusVertices = Mathf.RoundToInt(radius / dataCellSize);
            
            for (int x = centerX - radiusVertices; x <= centerX + radiusVertices; x++)
            {
                for (int z = centerZ - radiusVertices; z <= centerZ + radiusVertices; z++)
                {
                    float distance = Vector2.Distance(new Vector2(x, z), new Vector2(centerX, centerZ));
                    if (distance <= radiusVertices)
                    {
                        SetVertexUnlocked(x, z, true);
                    }
                }
            }
        }
        
        // 公共属性
        public float MapWidth => mapWidth;
        public float MapHeight => mapHeight;
        public float DataCellSize => dataCellSize;
        public int DataGridCountX => dataGridCountX;  // 格子数量
        public int DataGridCountZ => dataGridCountZ;  // 格子数量
        public int VertexCountX => vertexCountX;      // 角点数量
        public int VertexCountZ => vertexCountZ;      // 角点数量
        public TerrainDataConfig Config => config;
    }
} 