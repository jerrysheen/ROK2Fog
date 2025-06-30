using UnityEngine;

namespace FogSystem
{
    /// <summary>
    /// 小块类型枚举
    /// </summary>
    public enum CellBlockType
    {
        FullLocked = 0,        // 完全锁定（四个角点都未解锁）
        AdjacentUnLocked = 1,  // 相邻未锁定（与PartialUnlocked区域相邻）
        PartialUnlocked = 2,   // 部分未锁定（与FullUnlocked区域相邻）
        FullUnlocked = 3       // 完全未锁定（四个角点都已解锁）
    }

    /// <summary>
    /// 小块数据结构
    /// </summary>
    [System.Serializable]
    public struct CellBlock
    {
        public int gridX;              // 在mesh块中的本地格子坐标X
        public int gridZ;              // 在mesh块中的本地格子坐标Z
        public int globalGridX;        // 全局格子坐标X
        public int globalGridZ;        // 全局格子坐标Z
        public CellBlockType blockType; // 小块类型
        
        // 四个角点的地形数据（左下、右下、右上、左上）
        public TerrainCell bottomLeft;
        public TerrainCell bottomRight;
        public TerrainCell topRight;
        public TerrainCell topLeft;
        
        // 四个角点的解锁状态
        public bool bottomLeftUnlocked;
        public bool bottomRightUnlocked;
        public bool topRightUnlocked;
        public bool topLeftUnlocked;
    }

    /// <summary>
    /// 简化的迷雾系统 - 专注于地形可视化
    /// 根据地形数据生成带颜色标记的mesh，显示地形类型和解锁状态
    /// </summary>
    public class FogSystem : MonoBehaviour
    {
        [Header("地图配置")]
        [SerializeField] private float mapWidth = 500f;   // 地图总宽度（米）
        [SerializeField] private float mapHeight = 500f;  // 地图总高度（米）
        [SerializeField] private float cellSize = 1f;     // 显示格子的边长（米）
        [SerializeField] private float dataCellSize = 5f; // 数据格子的边长（米，可以比显示格子更大以节省内存）
        
        [Header("Mesh拆分配置")]
        [SerializeField] private float meshWidth = 50f;   // 单个mesh的宽度（米，应为cellSize的整数倍）
        [SerializeField] private float meshHeight = 50f;  // 单个mesh的高度（米，应为cellSize的整数倍）
        
        [Header("显示配置")]
        [SerializeField] private Material terrainMaterial; // 地形材质
        [SerializeField] private bool showUnlockedOverlay = true; // 是否在未解锁区域显示覆盖层
        
        [Header("视锥剔除配置")]
        [SerializeField] private Camera mainCamera; // 主相机
        [SerializeField] private bool enableFrustumCulling = true; // 是否启用视锥剔除
        [SerializeField] private float groundPlaneHeight = 0f; // 地面平面高度
        [SerializeField] private float cullingMargin = 10f; // 剔除边界扩展距离（米）
        
        [Header("调试信息")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private bool showVisibleMeshBounds = false; // 是否显示可见mesh块的边界
        
        // 核心组件
        private TerrainDataReader terrainReader;
        private GameObject[,] meshObjects; // 多个地形mesh对象
        private Mesh[,] meshes;
        
        // 计算出的实际格子数量
        private int gridCountX;
        private int gridCountZ;
        
        // 计算出的mesh块数量
        private int meshCountX;
        private int meshCountZ;
        
        // 视锥剔除相关
        private Vector3[] frustumGroundPoints = new Vector3[4]; // 视锥与地面的四个交点
        private Vector2Int minVisibleMesh = Vector2Int.zero;
        private Vector2Int maxVisibleMesh = Vector2Int.zero;
        private bool[,] meshVisibility; // 记录每个mesh块的可见性状态
        
        private bool systemInitialized = false;
        
        private void Awake()
        {
            InitializeSystem();
        }
        
        private void Start()
        {
            StartSystem();
        }
        
        private void Update()
        {
            if (systemInitialized && enableFrustumCulling && mainCamera != null)
            {
                UpdateFrustumCulling();
            }
        }
        
        /// <summary>
        /// 初始化系统
        /// </summary>
        private void InitializeSystem()
        {
            // 根据地图总尺寸和格子大小计算实际格子数量
            gridCountX = Mathf.CeilToInt(mapWidth / cellSize);
            gridCountZ = Mathf.CeilToInt(mapHeight / cellSize);
            
            // 根据mesh大小计算mesh块数量
            meshCountX = Mathf.CeilToInt(mapWidth / meshWidth);
            meshCountZ = Mathf.CeilToInt(mapHeight / meshHeight);
            
            // 初始化mesh数组
            meshObjects = new GameObject[meshCountX, meshCountZ];
            meshes = new Mesh[meshCountX, meshCountZ];
            
            // 初始化视锥剔除相关数组
            meshVisibility = new bool[meshCountX, meshCountZ];
            
            // 如果没有指定相机，尝试自动找到主相机
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    mainCamera = FindObjectOfType<Camera>();
                }
            }
            
            // 初始化地形数据读取器（使用独立的数据精度）
            terrainReader = new TerrainDataReader();
            terrainReader.Initialize(mapWidth, mapHeight, dataCellSize);
            
            systemInitialized = true;
            Debug.Log($"FogSystem: 系统初始化完成，地图总尺寸: {mapWidth}x{mapHeight}米，显示精度: {cellSize}米({gridCountX}x{gridCountZ}格子)，数据精度: {dataCellSize}米({terrainReader.DataGridCountX}x{terrainReader.DataGridCountZ}格子)");
            Debug.Log($"FogSystem: Mesh拆分配置，单个mesh尺寸: {meshWidth}x{meshHeight}米，mesh块数量: {meshCountX}x{meshCountZ}");
            Debug.Log($"FogSystem: 视锥剔除配置，主相机: {(mainCamera != null ? mainCamera.name : "未找到")}, 启用状态: {enableFrustumCulling}");
            
            if (showDebugInfo)
            {
                LogTerrainStatistics();
            }
        }
        
        /// <summary>
        /// 启动系统
        /// </summary>
        private void StartSystem()
        {
            if (!systemInitialized) return;
            
            // 生成地形mesh
            GenerateTerrainMesh();
        }
        
        /// <summary>
        /// 生成所有地形mesh块
        /// </summary>
        private void GenerateTerrainMesh()
        {
            int totalVertices = 0;
            
            // 为每个mesh块生成mesh
            for (int meshX = 0; meshX < meshCountX; meshX++)
            {
                for (int meshZ = 0; meshZ < meshCountZ; meshZ++)
                {
                    GenerateMeshBlock(meshX, meshZ);
                    totalVertices += meshes[meshX, meshZ].vertexCount;
                    
                    // 设置初始可见性（如果启用了视锥剔除，则先隐藏所有mesh块）
                    if (enableFrustumCulling && mainCamera != null)
                    {
                        meshObjects[meshX, meshZ].SetActive(false);
                        meshVisibility[meshX, meshZ] = false;
                    }
                    else
                    {
                        meshObjects[meshX, meshZ].SetActive(true);
                        meshVisibility[meshX, meshZ] = true;
                    }
                }
            }
            
            Debug.Log($"FogSystem: 生成了 {meshCountX}x{meshCountZ} = {meshCountX * meshCountZ} 个mesh块，总计 {totalVertices} 个顶点");
            
            // 如果启用了视锥剔除，立即进行一次更新
            if (enableFrustumCulling && mainCamera != null)
            {
                UpdateFrustumCulling();
            }
        }
        
        /// <summary>
        /// 生成单个mesh块
        /// </summary>
        private void GenerateMeshBlock(int meshX, int meshZ)
        {
            // 计算这个mesh块的世界坐标范围
            float startWorldX = meshX * meshWidth;
            float startWorldZ = meshZ * meshHeight;
            float endWorldX = Mathf.Min(startWorldX + meshWidth, mapWidth);
            float endWorldZ = Mathf.Min(startWorldZ + meshHeight, mapHeight);
            
            // 计算格子坐标范围
            int startGridX = Mathf.FloorToInt(startWorldX / cellSize);
            int startGridZ = Mathf.FloorToInt(startWorldZ / cellSize);
            int endGridX = Mathf.FloorToInt(endWorldX / cellSize);
            int endGridZ = Mathf.FloorToInt(endWorldZ / cellSize);
            
            // 计算这个mesh块的格子数量
            int blockGridCountX = endGridX - startGridX;
            int blockGridCountZ = endGridZ - startGridZ;
            
            if (blockGridCountX <= 0 || blockGridCountZ <= 0) return;
            
            // 创建mesh对象
            if (meshObjects[meshX, meshZ] == null)
            {
                meshObjects[meshX, meshZ] = new GameObject($"TerrainMesh_{meshX}_{meshZ}");
                meshObjects[meshX, meshZ].transform.parent = transform;
                
                MeshFilter meshFilter = meshObjects[meshX, meshZ].AddComponent<MeshFilter>();
                MeshRenderer meshRenderer = meshObjects[meshX, meshZ].AddComponent<MeshRenderer>();
                
                meshes[meshX, meshZ] = new Mesh();
                meshes[meshX, meshZ].name = $"TerrainMesh_{meshX}_{meshZ}";
                meshFilter.mesh = meshes[meshX, meshZ];
                meshRenderer.material = terrainMaterial;
            }
            
            // 生成这个mesh块的数据
            BuildMeshBlockData(meshX, meshZ, startGridX, startGridZ, blockGridCountX, blockGridCountZ);
        }
        
        /// <summary>
        /// 构建单个mesh块的数据
        /// </summary>
        private void BuildMeshBlockData(int meshX, int meshZ, int startGridX, int startGridZ, int blockGridCountX, int blockGridCountZ)
        {
            // 第一步：创建并分类所有小块
            CellBlock[,] cellBlocks = CreateAndClassifyCellBlocks(startGridX, startGridZ, blockGridCountX, blockGridCountZ);
            
            // 第二步：基于分类后的小块生成mesh数据
            GenerateMeshFromCellBlocks(meshX, meshZ, cellBlocks, startGridX, startGridZ, blockGridCountX, blockGridCountZ);
        }
        
        /// <summary>
        /// 创建并分类所有小块
        /// </summary>
        private CellBlock[,] CreateAndClassifyCellBlocks(int startGridX, int startGridZ, int blockGridCountX, int blockGridCountZ)
        {
            CellBlock[,] cellBlocks = new CellBlock[blockGridCountX, blockGridCountZ];
            
            // 第一遍：创建所有小块并初始化基础数据
            for (int localZ = 0; localZ < blockGridCountZ; localZ++)
            {
                for (int localX = 0; localX < blockGridCountX; localX++)
                {
                    cellBlocks[localX, localZ] = CreateCellBlock(localX, localZ, startGridX, startGridZ);
                }
            }
            
            // 第二遍：根据角点判断FullLocked和FullUnlocked
            for (int localZ = 0; localZ < blockGridCountZ; localZ++)
            {
                for (int localX = 0; localX < blockGridCountX; localX++)
                {
                    cellBlocks[localX, localZ] = ClassifyBasicCellBlock(cellBlocks[localX, localZ]);
                }
            }
            
            // 第三遍：标记PartialUnlocked（FullUnlocked相邻的八个块）
            MarkPartialUnlockedBlocks(cellBlocks, blockGridCountX, blockGridCountZ, startGridX, startGridZ);
            
            // 第四遍：标记AdjacentUnLocked（与PartialUnlocked相邻的块）
            MarkAdjacentUnLockedBlocks(cellBlocks, blockGridCountX, blockGridCountZ, startGridX, startGridZ);
            
            return cellBlocks;
        }
        
        /// <summary>
        /// 创建单个小块
        /// </summary>
        private CellBlock CreateCellBlock(int localX, int localZ, int startGridX, int startGridZ)
        {
            CellBlock block = new CellBlock();
            
            // 设置坐标
            block.gridX = localX;
            block.gridZ = localZ;
            block.globalGridX = startGridX + localX;
            block.globalGridZ = startGridZ + localZ;
            
            // 获取四个角点的地形数据（左下、右下、右上、左上）
            int globalX = block.globalGridX;
            int globalZ = block.globalGridZ;
            
            // 左下角点
            int queryX = Mathf.Clamp(globalX, 0, gridCountX - 1);
            int queryZ = Mathf.Clamp(globalZ, 0, gridCountZ - 1);
            block.bottomLeft = terrainReader.GetTerrainCellAtGrid(queryX, queryZ, cellSize);
            block.bottomLeftUnlocked = block.bottomLeft.isUnlocked || block.bottomLeft.terrainType == TerrainType.Unlocked;
            
            // 右下角点
            queryX = Mathf.Clamp(globalX + 1, 0, gridCountX - 1);
            queryZ = Mathf.Clamp(globalZ, 0, gridCountZ - 1);
            block.bottomRight = terrainReader.GetTerrainCellAtGrid(queryX, queryZ, cellSize);
            block.bottomRightUnlocked = block.bottomRight.isUnlocked || block.bottomRight.terrainType == TerrainType.Unlocked;
            
            // 右上角点
            queryX = Mathf.Clamp(globalX + 1, 0, gridCountX - 1);
            queryZ = Mathf.Clamp(globalZ + 1, 0, gridCountZ - 1);
            block.topRight = terrainReader.GetTerrainCellAtGrid(queryX, queryZ, cellSize);
            block.topRightUnlocked = block.topRight.isUnlocked || block.topRight.terrainType == TerrainType.Unlocked;
            
            // 左上角点
            queryX = Mathf.Clamp(globalX, 0, gridCountX - 1);
            queryZ = Mathf.Clamp(globalZ + 1, 0, gridCountZ - 1);
            block.topLeft = terrainReader.GetTerrainCellAtGrid(queryX, queryZ, cellSize);
            block.topLeftUnlocked = block.topLeft.isUnlocked || block.topLeft.terrainType == TerrainType.Unlocked;
            
            return block;
        }
        
        /// <summary>
        /// 根据角点分类基础小块类型（FullLocked或FullUnlocked）
        /// </summary>
        private CellBlock ClassifyBasicCellBlock(CellBlock block)
        {
            // 统计解锁的角点数量
            int unlockedCorners = 0;
            if (block.bottomLeftUnlocked) unlockedCorners++;
            if (block.bottomRightUnlocked) unlockedCorners++;
            if (block.topRightUnlocked) unlockedCorners++;
            if (block.topLeftUnlocked) unlockedCorners++;
            
            // 根据解锁角点数量分类
            if (unlockedCorners == 4)
            {
                block.blockType = CellBlockType.FullUnlocked;
            }
            else if (unlockedCorners == 0)
            {
                block.blockType = CellBlockType.FullLocked;
            }
            else
            {
                // 部分解锁的情况，暂时标记为FullLocked，后续会被重新分类
                block.blockType = CellBlockType.FullLocked;
            }
            
            return block;
        }
        
        /// <summary>
        /// 标记PartialUnlocked块（FullUnlocked相邻的四个块）
        /// </summary>
        private void MarkPartialUnlockedBlocks(CellBlock[,] cellBlocks, int blockGridCountX, int blockGridCountZ, int startGridX, int startGridZ)
        {
            // 四个方向的偏移量（左右上下）
            int[] dx = { -1, 1, 0, 0};
            int[] dz = { 0, 0, 1, -1};
            
            for (int localZ = 0; localZ < blockGridCountZ; localZ++)
            {
                for (int localX = 0; localX < blockGridCountX; localX++)
                {
                    // 跳过已经是FullUnlocked的块
                    if (cellBlocks[localX, localZ].blockType == CellBlockType.FullUnlocked)
                        continue;
                    
                    // 检查四个相邻位置是否有FullUnlocked块
                    bool hasFullUnlockedNeighbor = false;
                    for (int i = 0; i < 4; i++)
                    {
                        int neighborX = localX + dx[i];
                        int neighborZ = localZ + dz[i];
                        
                        CellBlockType neighborType = GetNeighborBlockType(cellBlocks, neighborX, neighborZ, blockGridCountX, blockGridCountZ, startGridX, startGridZ);
                        
                        if (neighborType == CellBlockType.FullUnlocked)
                        {
                            hasFullUnlockedNeighbor = true;
                            break;
                        }
                    }
                    
                    // 如果有FullUnlocked邻居，标记为PartialUnlocked
                    if (hasFullUnlockedNeighbor)
                    {
                        CellBlock block = cellBlocks[localX, localZ];
                        block.blockType = CellBlockType.PartialUnlocked;
                        cellBlocks[localX, localZ] = block;
                    }
                }
            }
        }
        
        /// <summary>
        /// 标记AdjacentUnLocked块（与PartialUnlocked相邻的块）
        /// </summary>
        private void MarkAdjacentUnLockedBlocks(CellBlock[,] cellBlocks, int blockGridCountX, int blockGridCountZ, int startGridX, int startGridZ)
        {
            // 四个方向的偏移量（左右上下）
            int[] dx = { -1, 1, 0, 0};
            int[] dz = { 0, 0, 1, -1};
            
            for (int localZ = 0; localZ < blockGridCountZ; localZ++)
            {
                for (int localX = 0; localX < blockGridCountX; localX++)
                {
                    // 只处理仍然是FullLocked的块
                    if (cellBlocks[localX, localZ].blockType != CellBlockType.FullLocked)
                        continue;
                    
                    // 检查四个相邻位置是否有PartialUnlocked块
                    bool hasPartialUnlockedNeighbor = false;
                    for (int i = 0; i < 4; i++)
                    {
                        int neighborX = localX + dx[i];
                        int neighborZ = localZ + dz[i];
                        
                        CellBlockType neighborType = GetNeighborBlockType(cellBlocks, neighborX, neighborZ, blockGridCountX, blockGridCountZ, startGridX, startGridZ);
                        
                        if (neighborType == CellBlockType.PartialUnlocked)
                        {
                            hasPartialUnlockedNeighbor = true;
                            break;
                        }
                    }
                    
                    // 如果有PartialUnlocked邻居，标记为AdjacentUnLocked
                    if (hasPartialUnlockedNeighbor)
                    {
                        CellBlock block = cellBlocks[localX, localZ];
                        block.blockType = CellBlockType.AdjacentUnLocked;
                        cellBlocks[localX, localZ] = block;
                    }
                }
            }
        }
        
        /// <summary>
        /// 获取邻居小块的类型（支持跨MeshBlock查询）
        /// </summary>
        private CellBlockType GetNeighborBlockType(CellBlock[,] cellBlocks, int neighborX, int neighborZ, int blockGridCountX, int blockGridCountZ, int startGridX, int startGridZ)
        {
            // 如果邻居在当前MeshBlock内，直接返回其类型
            if (neighborX >= 0 && neighborX < blockGridCountX && 
                neighborZ >= 0 && neighborZ < blockGridCountZ)
            {
                return cellBlocks[neighborX, neighborZ].blockType;
            }
            
            // 邻居超出当前MeshBlock范围，需要通过全局坐标查询地形数据
            int globalNeighborX = startGridX + neighborX;
            int globalNeighborZ = startGridZ + neighborZ;
            
            // 检查全局坐标是否有效
            if (globalNeighborX < 0 || globalNeighborX >= gridCountX || 
                globalNeighborZ < 0 || globalNeighborZ >= gridCountZ)
            {
                return CellBlockType.FullLocked; // 超出地图边界，视为完全锁定
            }
            
            // 获取邻居小块四个角点的解锁状态
            bool bottomLeftUnlocked = IsCornerUnlocked(globalNeighborX, globalNeighborZ);
            bool bottomRightUnlocked = IsCornerUnlocked(globalNeighborX + 1, globalNeighborZ);
            bool topRightUnlocked = IsCornerUnlocked(globalNeighborX + 1, globalNeighborZ + 1);
            bool topLeftUnlocked = IsCornerUnlocked(globalNeighborX, globalNeighborZ + 1);
            
            // 统计解锁的角点数量
            int unlockedCorners = 0;
            if (bottomLeftUnlocked) unlockedCorners++;
            if (bottomRightUnlocked) unlockedCorners++;
            if (topRightUnlocked) unlockedCorners++;
            if (topLeftUnlocked) unlockedCorners++;
            
            // 根据解锁角点数量返回基础类型
            if (unlockedCorners == 4)
            {
                return CellBlockType.FullUnlocked;
            }
            else
            {
                // 对于跨MeshBlock的邻居，我们无法准确判断其PartialUnlocked或AdjacentUnLocked状态
                // 简化处理：非完全解锁的都视为FullLocked
                return CellBlockType.FullLocked;
            }
        }
        
        /// <summary>
        /// 检查指定全局格子坐标的角点是否解锁
        /// </summary>
        private bool IsCornerUnlocked(int globalX, int globalZ)
        {
            // 边界处理
            int queryX = Mathf.Clamp(globalX, 0, gridCountX - 1);
            int queryZ = Mathf.Clamp(globalZ, 0, gridCountZ - 1);
            
            TerrainCell terrainCell = terrainReader.GetTerrainCellAtGrid(queryX, queryZ, cellSize);
            return terrainCell.isUnlocked || terrainCell.terrainType == TerrainType.Unlocked;
        }
        
        /// <summary>
        /// 基于分类后的小块生成mesh数据
        /// </summary>
        private void GenerateMeshFromCellBlocks(int meshX, int meshZ, CellBlock[,] cellBlocks, int startGridX, int startGridZ, int blockGridCountX, int blockGridCountZ)
        {
            // 计算顶点数量：每个格子需要4个顶点，但相邻格子共享顶点
            int vertexCountX = blockGridCountX + 1;
            int vertexCountZ = blockGridCountZ + 1;
            int totalVertices = vertexCountX * vertexCountZ;
            
            // 创建数组
            Vector3[] vertices = new Vector3[totalVertices];
            Vector2[] uvs = new Vector2[totalVertices];
            Color[] colors = new Color[totalVertices];
            
            // 生成顶点数据
            int vertexIndex = 0;
            for (int localZ = 0; localZ <= blockGridCountZ; localZ++)
            {
                for (int localX = 0; localX <= blockGridCountX; localX++)
                {
                    // 全局格子坐标
                    int globalX = startGridX + localX;
                    int globalZ = startGridZ + localZ;
                    
                    // 世界坐标
                    float worldX = globalX * cellSize;
                    float worldZ = globalZ * cellSize;
                    
                    // 获取地形数据（使用任意精度查询，支持不同的数据和显示精度）
                    int queryX = Mathf.Clamp(globalX, 0, gridCountX - 1);
                    int queryZ = Mathf.Clamp(globalZ, 0, gridCountZ - 1);
                    TerrainCell terrainCell = terrainReader.GetTerrainCellAtGrid(queryX, queryZ, cellSize);
                    
                    // 根据小块类型获取顶点颜色
                    Color vertexColor = GetVertexColorWithBlockType(terrainCell, localX, localZ, cellBlocks, blockGridCountX, blockGridCountZ);
                    
                    vertices[vertexIndex] = new Vector3(worldX, terrainCell.height, worldZ);
                    uvs[vertexIndex] = new Vector2((float)localX / blockGridCountX, (float)localZ / blockGridCountZ);
                    colors[vertexIndex] = vertexColor;
                    
                    vertexIndex++;
                }
            }
            
            // 生成三角形（每个格子2个三角形）
            int triangleCount = blockGridCountX * blockGridCountZ * 6;
            int[] triangles = new int[triangleCount];
            
            int triangleIndex = 0;
            for (int localZ = 0; localZ < blockGridCountZ; localZ++)
            {
                for (int localX = 0; localX < blockGridCountX; localX++)
                {
                    // 当前格子的4个顶点索引（基于本地mesh块坐标）
                    int bottomLeft = localZ * vertexCountX + localX;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = (localZ + 1) * vertexCountX + localX;
                    int topRight = topLeft + 1;
                    
                    // 第一个三角形（逆时针：左下-左上-右下）
                    triangles[triangleIndex] = bottomLeft;
                    triangles[triangleIndex + 1] = topLeft;
                    triangles[triangleIndex + 2] = bottomRight;
                    
                    // 第二个三角形（逆时针：左上-右上-右下）
                    triangles[triangleIndex + 3] = topLeft;
                    triangles[triangleIndex + 4] = topRight;
                    triangles[triangleIndex + 5] = bottomRight;
                    
                    triangleIndex += 6;
                }
            }
            
            // 应用到mesh
            Mesh currentMesh = meshes[meshX, meshZ];
            currentMesh.Clear();
            currentMesh.vertices = vertices;
            currentMesh.triangles = triangles;
            currentMesh.uv = uvs;
            currentMesh.colors = colors;
            currentMesh.RecalculateNormals();
            currentMesh.RecalculateBounds();
        }
        
        /// <summary>
        /// 根据小块类型和地形数据获取顶点颜色
        /// </summary>
        private Color GetVertexColorWithBlockType(TerrainCell terrainCell, int localX, int localZ, CellBlock[,] cellBlocks, int blockGridCountX, int blockGridCountZ)
        {
            // 获取基础地形颜色
            Color baseColor = GetVertexColor(terrainCell);
            
            // 找到影响这个顶点的小块类型（一个顶点可能被多个小块共享）
            CellBlockType dominantBlockType = GetDominantBlockTypeForVertex(localX, localZ, cellBlocks, blockGridCountX, blockGridCountZ);
            
            // 根据小块类型调整颜色
            return ApplyBlockTypeToColor(baseColor, dominantBlockType);
        }
        
        /// <summary>
        /// 获取影响顶点的主导小块类型
        /// </summary>
        private CellBlockType GetDominantBlockTypeForVertex(int localX, int localZ, CellBlock[,] cellBlocks, int blockGridCountX, int blockGridCountZ)
        {
            // 一个顶点最多被4个小块共享，我们需要找到优先级最高的类型
            CellBlockType dominantType = CellBlockType.FullLocked;
            
            // 检查左下小块
            if (localX > 0 && localZ > 0)
            {
                dominantType = GetHigherPriorityType(dominantType, cellBlocks[localX - 1, localZ - 1].blockType);
            }
            
            // 检查右下小块
            if (localX < blockGridCountX && localZ > 0)
            {
                dominantType = GetHigherPriorityType(dominantType, cellBlocks[localX, localZ - 1].blockType);
            }
            
            // 检查右上小块
            if (localX < blockGridCountX && localZ < blockGridCountZ)
            {
                dominantType = GetHigherPriorityType(dominantType, cellBlocks[localX, localZ].blockType);
            }
            
            // 检查左上小块
            if (localX > 0 && localZ < blockGridCountZ)
            {
                dominantType = GetHigherPriorityType(dominantType, cellBlocks[localX - 1, localZ].blockType);
            }
            
            return dominantType;
        }
        
        /// <summary>
        /// 获取更高优先级的小块类型（FullUnlocked > PartialUnlocked > AdjacentUnLocked > FullLocked）
        /// </summary>
        private CellBlockType GetHigherPriorityType(CellBlockType type1, CellBlockType type2)
        {
            return (CellBlockType)Mathf.Max((int)type1, (int)type2);
        }
        
        /// <summary>
        /// 根据小块类型调整颜色
        /// </summary>
        private Color ApplyBlockTypeToColor(Color baseColor, CellBlockType blockType)
        {
            switch (blockType)
            {
                case CellBlockType.FullUnlocked:
                    return baseColor; // 完全解锁，使用原始颜色
                    
                case CellBlockType.PartialUnlocked:
                    return Color.Lerp(baseColor, Color.white, 0.3f); // 稍微提亮
                    
                case CellBlockType.AdjacentUnLocked:
                    return Color.Lerp(baseColor, Color.gray, 0.5f); // 灰色调
                    
                case CellBlockType.FullLocked:
                default:
                    return Color.Lerp(baseColor, Color.black, 0.7f) * 0.3f; // 很暗的颜色
            }
        }
        
        
        /// <summary>
        /// 根据地形数据获取顶点颜色
        /// </summary>
        private Color GetVertexColor(TerrainCell terrainCell)
        {
            // 直接基于地形类型获取颜色
            Color baseColor = GetTerrainTypeColor(terrainCell.terrainType);
            
            // 如果是传统的未解锁区域且需要显示迷雾覆盖
            if (terrainCell.terrainType != TerrainType.Unlocked && !terrainCell.isUnlocked && showUnlockedOverlay)
            {
                // 未解锁区域：使用灰色调的地形颜色，透明度降低
                return Color.Lerp(baseColor, Color.black, 0.7f) * 0.3f;
            }
            
            return baseColor;
        }
        
        /// <summary>
        /// 根据地形类型获取颜色
        /// </summary>
        private Color GetTerrainTypeColor(TerrainType terrainType)
        {
            switch (terrainType)
            {
                case TerrainType.Plain:
                    return new Color(0.4f, 0.8f, 0.2f, 1f); // 浅绿色
                case TerrainType.Hill:
                    return new Color(0.8f, 0.7f, 0.3f, 1f); // 黄褐色
                case TerrainType.Mountain:
                    return new Color(0.6f, 0.6f, 0.6f, 1f); // 灰色
                case TerrainType.Lake:
                    return new Color(0.2f, 0.5f, 0.9f, 1f); // 蓝色
                case TerrainType.Forest:
                    return new Color(0.2f, 0.6f, 0.2f, 1f); // 深绿色
                case TerrainType.Unlocked:
                    return new Color(0.9f, 0.9f, 0.7f, 1f); // 淡黄色（已探索区域）
                default:
                    return Color.white;
            }
        }
        
        /// <summary>
        /// 世界坐标转网格坐标
        /// </summary>
        private Vector2Int WorldToGridPos(Vector3 worldPos)
        {
            int gridX = Mathf.FloorToInt(worldPos.x / cellSize);
            int gridZ = Mathf.FloorToInt(worldPos.z / cellSize);
            return new Vector2Int(gridX, gridZ);
        }
        
        /// <summary>
        /// 记录地形统计信息
        /// </summary>
        private void LogTerrainStatistics()
        {
            int[] terrainCounts = new int[6]; // 增加到6种地形类型
            int unlockedCount = 0;
            int totalDataCells = terrainReader.DataGridCountX * terrainReader.DataGridCountZ;
            
            // 统计实际数据格子，而不是显示格子
            for (int x = 0; x < terrainReader.DataGridCountX; x++)
            {
                for (int z = 0; z < terrainReader.DataGridCountZ; z++)
                {
                    TerrainCell cell = terrainReader.GetTerrainCell(x, z);
                    terrainCounts[(int)cell.terrainType]++;
                    if (cell.isUnlocked) unlockedCount++;
                }
            }
            
            Debug.Log($"地形统计 - 平原: {terrainCounts[0]}, 丘陵: {terrainCounts[1]}, 山脉: {terrainCounts[2]}, 湖泊: {terrainCounts[3]}, 森林: {terrainCounts[4]}, 已解锁: {terrainCounts[5]}");
            Debug.Log($"解锁区域: {unlockedCount}/{totalDataCells} ({(float)unlockedCount / totalDataCells * 100:F1}%)");
            Debug.Log($"显示精度: {cellSize}m，数据精度: {terrainReader.DataCellSize}m");
        }

        private void UpdateFrustumCulling()
        {
            // 计算视锥与地面的交点
            if (CalculateFrustumGroundIntersection())
            {
                // 根据交点计算需要显示的mesh块范围
                CalculateVisibleMeshRange();
                
                // 更新mesh块的可见性
                UpdateMeshVisibility();
            }
        }
        
        /// <summary>
        /// 计算相机视锥与地面平面的交点
        /// </summary>
        private bool CalculateFrustumGroundIntersection()
        {
            if (mainCamera == null) return false;
            
            // 获取相机的视锥四个角的射线（远平面）
            Ray[] frustumRays = new Ray[4];
            
            // 计算视锥四个角的方向（NDC坐标系）
            Vector3[] frustumCorners = new Vector3[4]
            {
                new Vector3(-1, -1, 1), // 左下
                new Vector3(1, -1, 1),  // 右下
                new Vector3(1, 1, 1),   // 右上
                new Vector3(-1, 1, 1)   // 左上
            };
            
            // 将NDC坐标转换为世界空间射线
            for (int i = 0; i < 4; i++)
            {
                Vector3 worldPos = mainCamera.ViewportToWorldPoint(new Vector3(
                    (frustumCorners[i].x + 1) * 0.5f,
                    (frustumCorners[i].y + 1) * 0.5f,
                    mainCamera.farClipPlane));
                
                frustumRays[i] = new Ray(mainCamera.transform.position, (worldPos - mainCamera.transform.position).normalized);
            }
            
            // 计算射线与地面平面的交点
            Plane groundPlane = new Plane(Vector3.up, groundPlaneHeight);
            
            for (int i = 0; i < 4; i++)
            {
                if (groundPlane.Raycast(frustumRays[i], out float distance))
                {
                    frustumGroundPoints[i] = frustumRays[i].GetPoint(distance);
                }
                else
                {
                    // 如果射线与地面不相交（相机朝上看），使用一个很远的点
                    frustumGroundPoints[i] = frustumRays[i].GetPoint(mainCamera.farClipPlane);
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// 根据视锥交点计算可见的mesh块范围
        /// </summary>
        private void CalculateVisibleMeshRange()
        {
            // 找到所有交点的边界
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minZ = float.MaxValue;
            float maxZ = float.MinValue;
            
            for (int i = 0; i < 4; i++)
            {
                minX = Mathf.Min(minX, frustumGroundPoints[i].x);
                maxX = Mathf.Max(maxX, frustumGroundPoints[i].x);
                minZ = Mathf.Min(minZ, frustumGroundPoints[i].z);
                maxZ = Mathf.Max(maxZ, frustumGroundPoints[i].z);
            }
            
            // 扩展边界
            minX -= cullingMargin;
            maxX += cullingMargin;
            minZ -= cullingMargin;
            maxZ += cullingMargin;
            
            // 转换为mesh块坐标
            int meshMinX = Mathf.FloorToInt(minX / meshWidth);
            int meshMaxX = Mathf.FloorToInt(maxX / meshWidth);
            int meshMinZ = Mathf.FloorToInt(minZ / meshHeight);
            int meshMaxZ = Mathf.FloorToInt(maxZ / meshHeight);
            
            // 限制在有效范围内
            meshMinX = Mathf.Clamp(meshMinX, 0, meshCountX - 1);
            meshMaxX = Mathf.Clamp(meshMaxX, 0, meshCountX - 1);
            meshMinZ = Mathf.Clamp(meshMinZ, 0, meshCountZ - 1);
            meshMaxZ = Mathf.Clamp(meshMaxZ, 0, meshCountZ - 1);
            
            minVisibleMesh = new Vector2Int(meshMinX, meshMinZ);
            maxVisibleMesh = new Vector2Int(meshMaxX, meshMaxZ);
        }
        
        /// <summary>
        /// 更新mesh块的可见性
        /// </summary>
        private void UpdateMeshVisibility()
        {
            int visibleCount = 0;
            int hiddenCount = 0;
            
            for (int x = 0; x < meshCountX; x++)
            {
                for (int z = 0; z < meshCountZ; z++)
                {
                    bool shouldBeVisible = (x >= minVisibleMesh.x && x <= maxVisibleMesh.x &&
                                          z >= minVisibleMesh.y && z <= maxVisibleMesh.y);
                    
                    // 只在状态改变时更新GameObject
                    if (meshVisibility[x, z] != shouldBeVisible)
                    {
                        meshVisibility[x, z] = shouldBeVisible;
                        
                        if (meshObjects[x, z] != null)
                        {
                            meshObjects[x, z].SetActive(shouldBeVisible);
                        }
                    }
                    
                    if (shouldBeVisible) visibleCount++;
                    else hiddenCount++;
                }
            }
            
        }
        
        /// <summary>
        /// 世界坐标转mesh块坐标
        /// </summary>
        private Vector2Int WorldToMeshBlockPos(Vector3 worldPos)
        {
            int meshX = Mathf.FloorToInt(worldPos.x / meshWidth);
            int meshZ = Mathf.FloorToInt(worldPos.z / meshHeight);
            return new Vector2Int(meshX, meshZ);
        }
        
        /// <summary>
        /// mesh块坐标转世界坐标中心点
        /// </summary>
        private Vector3 MeshBlockToWorldCenter(Vector2Int meshPos)
        {
            float worldX = meshPos.x * meshWidth + meshWidth * 0.5f;
            float worldZ = meshPos.y * meshHeight + meshHeight * 0.5f;
            return new Vector3(worldX, groundPlaneHeight, worldZ);
        }
        
        /// <summary>
        /// 获取指定mesh块的世界边界
        /// </summary>
        private Bounds GetMeshBlockBounds(int meshX, int meshZ)
        {
            Vector3 center = MeshBlockToWorldCenter(new Vector2Int(meshX, meshZ));
            Vector3 size = new Vector3(meshWidth, 0, meshHeight);
            return new Bounds(center, size);
        }
        
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !systemInitialized || !showVisibleMeshBounds) return;
            
            // 绘制可见mesh块的边界
            Gizmos.color = Color.green;
            for (int x = minVisibleMesh.x; x <= maxVisibleMesh.x; x++)
            {
                for (int z = minVisibleMesh.y; z <= maxVisibleMesh.y; z++)
                {
                    if (x >= 0 && x < meshCountX && z >= 0 && z < meshCountZ)
                    {
                        Bounds bounds = GetMeshBlockBounds(x, z);
                        Gizmos.DrawWireCube(bounds.center, bounds.size);
                    }
                }
            }
            
            // 绘制视锥与地面的交点
            if (enableFrustumCulling && mainCamera != null)
            {
                Gizmos.color = Color.red;
                for (int i = 0; i < 4; i++)
                {
                    Gizmos.DrawSphere(frustumGroundPoints[i], 2f);
                    
                    // 绘制视锥边界线
                    int nextIndex = (i + 1) % 4;
                    Gizmos.DrawLine(frustumGroundPoints[i], frustumGroundPoints[nextIndex]);
                }
            }
        }
        
        /// <summary>
        /// 手动强制更新视锥剔除（用于性能优化，比如当相机移动较大距离时）
        /// </summary>
        public void ForceUpdateFrustumCulling()
        {
            if (systemInitialized && enableFrustumCulling && mainCamera != null)
            {
                UpdateFrustumCulling();
            }
        }
        
        /// <summary>
        /// 设置视锥剔除启用状态
        /// </summary>
        public void SetFrustumCullingEnabled(bool enabled)
        {
            enableFrustumCulling = enabled;
            
            if (!enabled)
            {
                // 如果禁用视锥剔除，显示所有mesh块
                ShowAllMeshBlocks();
            }
            else if (mainCamera != null)
            {
                // 如果启用视锥剔除，立即更新
                UpdateFrustumCulling();
            }
        }
        
        /// <summary>
        /// 显示所有mesh块
        /// </summary>
        private void ShowAllMeshBlocks()
        {
            for (int x = 0; x < meshCountX; x++)
            {
                for (int z = 0; z < meshCountZ; z++)
                {
                    if (meshObjects[x, z] != null)
                    {
                        meshObjects[x, z].SetActive(true);
                        meshVisibility[x, z] = true;
                    }
                }
            }
        }
        
        /// <summary>
        /// 设置主相机
        /// </summary>
        public void SetMainCamera(Camera camera)
        {
            mainCamera = camera;
            if (enableFrustumCulling && systemInitialized)
            {
                UpdateFrustumCulling();
            }
        }
        
        /// <summary>
        /// 获取当前可见的mesh块数量
        /// </summary>
        public int GetVisibleMeshBlockCount()
        {
            int count = 0;
            for (int x = 0; x < meshCountX; x++)
            {
                for (int z = 0; z < meshCountZ; z++)
                {
                    if (meshVisibility[x, z]) count++;
                }
            }
            return count;
        }
        
        /// <summary>
        /// 获取mesh块的可见性信息
        /// </summary>
        public bool IsMeshBlockVisible(int meshX, int meshZ)
        {
            if (meshX < 0 || meshX >= meshCountX || meshZ < 0 || meshZ >= meshCountZ)
                return false;
                
            return meshVisibility[meshX, meshZ];
        }
    }
} 