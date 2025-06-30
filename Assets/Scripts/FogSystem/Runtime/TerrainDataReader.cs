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
    /// 地形数据结构
    /// </summary>
    [System.Serializable]
    public struct TerrainCell
    {
        public float height;
        public TerrainType terrainType;
        public bool isUnlocked;
        public float noiseValue;  // 用于额外的随机性
    }

    /// <summary>
    /// 假数据配置
    /// </summary>
    [System.Serializable]
    public class TerrainDataConfig
    {
        public int seed = 12345;
        public float noiseScale = 0.01f;
        public float mountainHeight = 15f;
        public float hillHeight = 8f;
        public float plainHeight = 2f;
        public float lakeDepth = -2f;
        public float forestHeight = 5f;
        
        // 地形类型分布权重
        public float plainWeight = 0.4f;
        public float hillWeight = 0.25f;
        public float mountainWeight = 0.15f;
        public float lakeWeight = 0.1f;
        public float forestWeight = 0.1f;
    }

    /// <summary>
    /// 地形数据读取器 - 独立数据精度版本
    /// 支持与显示系统不同的数据粒度，通过插值提供任意精度的查询
    /// </summary>
    public class TerrainDataReader
    {
        private TerrainCell[,] terrainData;
        private float mapWidth;      // 地图总宽度（米）
        private float mapHeight;     // 地图总高度（米）
        private float dataCellSize;  // 数据格子大小（米）
        private int dataGridCountX;  // 数据格子数量X
        private int dataGridCountZ;  // 数据格子数量Z
        private TerrainDataConfig config;
        
        /// <summary>
        /// 初始化并生成所有地形数据
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
            
            // 加载配置数据
            LoadConfigData();
            
            // 初始化地形数据数组
            terrainData = new TerrainCell[dataGridCountX, dataGridCountZ];
            
            // 生成地形数据
            GenerateAllTerrainData();
            
            Debug.Log($"TerrainDataReader: 地图尺寸 {mapWidth}x{mapHeight}m，数据精度 {dataCellSize}m，生成了 {dataGridCountX}x{dataGridCountZ} 个数据格子");
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
        /// 生成所有地形数据
        /// </summary>
        private void GenerateAllTerrainData()
        {
            Random.InitState(config.seed);
            
            for (int x = 0; x < dataGridCountX; x++)
            {
                for (int z = 0; z < dataGridCountZ; z++)
                {
                    terrainData[x, z] = GenerateTerrainCell(x, z);
                }
            }
        }
        
        /// <summary>
        /// 生成单个地形格子数据
        /// </summary>
        private TerrainCell GenerateTerrainCell(int x, int z)
        {
            TerrainCell cell = new TerrainCell();
            
            // 生成基础噪声值
            float noiseX = x * config.noiseScale;
            float noiseZ = z * config.noiseScale;
            cell.noiseValue = Mathf.PerlinNoise(noiseX, noiseZ);
            
            // 首先判断是否为解锁区域
            float unlockNoise = Mathf.PerlinNoise(x * 0.02f + 1000f, z * 0.02f + 1000f);
            bool isUnlockedArea = unlockNoise > 0.6f; // 约40%的区域解锁
            
            if (isUnlockedArea)
            {
                // 解锁区域直接设置为Unlocked类型
                cell.terrainType = TerrainType.Unlocked;
                cell.isUnlocked = true;
            }
            else
            {
                // 未解锁区域确定地形类型
                cell.terrainType = DetermineTerrainType(x, z, cell.noiseValue);
                cell.isUnlocked = false;
            }
            
            // 根据地形类型确定高度
            cell.height = CalculateHeight(cell.terrainType, cell.noiseValue, x, z);
            
            return cell;
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
            float baseHeight = 0f;
            
            switch (terrainType)
            {
                case TerrainType.Plain:
                    baseHeight = config.plainHeight; // 低地形
                    break;
                case TerrainType.Hill:
                    baseHeight = config.hillHeight; // 中高地形
                    break;
                case TerrainType.Mountain:
                    baseHeight = config.mountainHeight; // 高地形
                    break;
                case TerrainType.Lake:
                    baseHeight = config.lakeDepth; // 低地形（负值）
                    break;
                case TerrainType.Forest:
                    baseHeight = config.forestHeight; // 中高地形
                    break;
                case TerrainType.Unlocked:
                    return 0f; // 解锁区域固定高度为0，不添加噪声变化
            }
            
            // 添加噪声变化，让同类型地形也有高度差异
            float heightVariation = (noiseValue - 0.5f) * 2f; // -1 to 1
            baseHeight += heightVariation * (Mathf.Abs(baseHeight) * 0.3f); // 30%的高度变化
            
            return baseHeight;
        }
        
        /// <summary>
        /// 获取指定数据格子坐标的高度
        /// </summary>
        public float GetHeight(int x, int z)
        {
            if (x < 0 || x >= dataGridCountX || z < 0 || z >= dataGridCountZ)
                return 0f;
                
            return terrainData[x, z].height;
        }
        
        /// <summary>
        /// 获取指定数据格子坐标的地形数据
        /// </summary>
        public TerrainCell GetTerrainCell(int x, int z)
        {
            if (x < 0 || x >= dataGridCountX || z < 0 || z >= dataGridCountZ)
                return new TerrainCell { height = 0f, terrainType = TerrainType.Plain, isUnlocked = false };
                
            return terrainData[x, z];
        }
        
        /// <summary>
        /// 设置指定数据格子坐标的解锁状态
        /// </summary>
        public void SetUnlocked(int x, int z, bool unlocked)
        {
            if (x < 0 || x >= dataGridCountX || z < 0 || z >= dataGridCountZ)
                return;
                
            terrainData[x, z].isUnlocked = unlocked;
        }
        
        /// <summary>
        /// 检查指定数据格子坐标是否已解锁
        /// </summary>
        public bool IsUnlocked(int x, int z)
        {
            if (x < 0 || x >= dataGridCountX || z < 0 || z >= dataGridCountZ)
                return false;
                
            return terrainData[x, z].isUnlocked;
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
        /// 解锁指定区域
        /// </summary>
        public void UnlockArea(Vector3 centerWorldPos, float radius)
        {
            int centerX = Mathf.RoundToInt(centerWorldPos.x / dataCellSize);
            int centerZ = Mathf.RoundToInt(centerWorldPos.z / dataCellSize);
            int radiusCells = Mathf.RoundToInt(radius / dataCellSize);
            
            for (int x = centerX - radiusCells; x <= centerX + radiusCells; x++)
            {
                for (int z = centerZ - radiusCells; z <= centerZ + radiusCells; z++)
                {
                    float distance = Vector2.Distance(new Vector2(x, z), new Vector2(centerX, centerZ));
                    if (distance <= radiusCells)
                    {
                        SetUnlocked(x, z, true);
                    }
                }
            }
        }
        
        public float MapWidth => mapWidth;
        public float MapHeight => mapHeight;
        public float DataCellSize => dataCellSize;
        public int DataGridCountX => dataGridCountX;
        public int DataGridCountZ => dataGridCountZ;
        public TerrainDataConfig Config => config;
    }
} 