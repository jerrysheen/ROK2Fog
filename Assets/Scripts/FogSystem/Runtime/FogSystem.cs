using UnityEngine;

namespace FogSystem
{
    /// <summary>
    /// 小块类型枚举
    /// </summary>
    public enum CellBlockType
    {
        FullLocked = 0,        // 完全锁定（四个角点都未解锁）
        AdjacentUnLocked = 1,  // 相邻未锁定（预留给后续扩展使用）
        PartialUnlocked = 2,   // 部分解锁（1-3个角点解锁）
        FullUnlocked = 3       // 完全解锁（四个角点都已解锁）
    }

    /// <summary>
    /// 小块数据结构（简化版本，用于调试）
    /// </summary>
    [System.Serializable]
    public struct CellBlock
    {
        public int gridX;              // 在mesh块中的本地格子坐标X
        public int gridZ;              // 在mesh块中的本地格子坐标Z
        public int globalGridX;        // 全局格子坐标X
        public int globalGridZ;        // 全局格子坐标Z
        public CellBlockType blockType; // 小块类型
        
        // 四个角点的高度
        public float bottomLeftHeight;
        public float bottomRightHeight;
        public float topRightHeight;
        public float topLeftHeight;
        
        // 四个角点的解锁状态
        public bool bottomLeftUnlocked;
        public bool bottomRightUnlocked;
        public bool topRightUnlocked;
        public bool topLeftUnlocked;
    }

    /// <summary>
    /// 简化的迷雾系统 - 专注于Mesh管理
    /// 负责mesh块的创建、显示/隐藏管理、视锥剔除等
    /// </summary>
    public class FogSystem : MonoBehaviour
    {
        [Header("地图配置")]
        [SerializeField] private float mapWidth = 1200f;   // 地图总宽度（米）
        [SerializeField] private float mapHeight = 1200f;  // 地图总高度（米）
        [SerializeField] private float cellSize = 1f;     // 显示格子的边长（米）
        [SerializeField] private float dataCellSize = 5f; // 数据格子的边长（米，可以比显示格子更大以节省内存）
        
        [Header("Mesh拆分配置")]
        [SerializeField] private float meshWidth = 360;   // 单个mesh的宽度（米，应为cellSize的整数倍）
        [SerializeField] private float meshHeight = 180f;  // 单个mesh的高度（米，应为cellSize的整数倍）
        
        [Header("显示配置")]
        [SerializeField] private Material fogBaseMaterial; // 地形材质
        [SerializeField] private Material fogMaterial; // 地形材质
        [SerializeField] private bool showUnlockedOverlay = true; // 是否在未解锁区域显示覆盖层
        
        [Header("视锥剔除配置")]
        [SerializeField] private Camera mainCamera; // 主相机
        [SerializeField] private bool enableFrustumCulling = true; // 是否启用视锥剔除
        [SerializeField] private float groundPlaneHeight = 0f; // 地面平面高度
        [SerializeField] private float cullingMargin = 10f; // 剔除边界扩展距离（米）
        
        // 核心组件
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
        
#if UNITY_EDITOR
        [Header("调试信息")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private bool showVisibleMeshBounds = false; // 是否显示可见mesh块的边界
        [SerializeField] private bool showCellBlockGizmos = false; // 是否显示CellBlock类型的Gizmo
        
        // CellBlock数据存储（用于Gizmo显示）
        private CellBlock[,] allCellBlocks; // 存储所有CellBlock数据
#endif
        
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
            
#if UNITY_EDITOR
            // 初始化CellBlock数据存储（仅在编辑器中用于Gizmo显示）
            allCellBlocks = new CellBlock[gridCountX, gridCountZ];
#endif
            
            // 如果没有指定相机，尝试自动找到主相机
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    mainCamera = FindObjectOfType<Camera>();
                }
            }
            
            // 初始化地形数据读取器（使用静态方法）
            TerrainDataReader.Initialize(mapWidth, mapHeight, dataCellSize);
            
            systemInitialized = true;
            Debug.Log($"FogSystem: 系统初始化完成，地图总尺寸: {mapWidth}x{mapHeight}米，显示精度: {cellSize}米({gridCountX}x{gridCountZ}格子)，数据精度: {dataCellSize}米({TerrainDataReader.DataGridCountX}x{TerrainDataReader.DataGridCountZ}格子)");
            Debug.Log($"FogSystem: Mesh拆分配置，单个mesh尺寸: {meshWidth}x{meshHeight}米，mesh块数量: {meshCountX}x{meshCountZ}");
            Debug.Log($"FogSystem: 视锥剔除配置，主相机: {(mainCamera != null ? mainCamera.name : "未找到")}, 启用状态: {enableFrustumCulling}");
            
#if UNITY_EDITOR
            if (showDebugInfo)
            {
                LogTerrainStatistics();
            }
#endif
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
        /// 生成单个mesh块（调用SingleFogMeshGenerator）
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
                meshRenderer.materials = new []{fogBaseMaterial, fogMaterial };
            }
            
            // 调用SingleFogMeshGenerator生成mesh数据（使用简化的接口）
            SingleFogMeshGenerator.GenerateMeshBlock(meshes[meshX, meshZ], startGridX, startGridZ, 
                blockGridCountX, blockGridCountZ, cellSize);
            
#if UNITY_EDITOR
            // 存储CellBlock数据用于Gizmo显示
            StoreCellBlocksToGlobalArray(startGridX, startGridZ, blockGridCountX, blockGridCountZ);
#endif
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
        
        /// <summary>
        /// 重新生成指定mesh块（用于动态更新）
        /// </summary>
        public void RegenerateMeshBlock(int meshX, int meshZ)
        {
            if (meshX < 0 || meshX >= meshCountX || meshZ < 0 || meshZ >= meshCountZ)
                return;
                
            GenerateMeshBlock(meshX, meshZ);
        }
        
        /// <summary>
        /// 重新生成所有mesh块（用于全局更新）
        /// </summary>
        public void RegenerateAllMeshBlocks()
        {
            for (int meshX = 0; meshX < meshCountX; meshX++)
            {
                for (int meshZ = 0; meshZ < meshCountZ; meshZ++)
                {
                    GenerateMeshBlock(meshX, meshZ);
                }
            }
        }

        // ==================== 编辑器调试功能 ====================
#if UNITY_EDITOR
        
        /// <summary>
        /// 创建调试用的CellBlock数据
        /// </summary>
        private CellBlock CreateDebugCellBlock(int localX, int localZ, int startGridX, int startGridZ)
        {
            CellBlock block = new CellBlock();
            
            // 设置坐标
            block.gridX = localX;
            block.gridZ = localZ;
            block.globalGridX = startGridX + localX;
            block.globalGridZ = startGridZ + localZ;
            
            // 获取四个角点的高度和解锁状态
            int globalX = block.globalGridX;
            int globalZ = block.globalGridZ;
            
            float bottomLeftHeight, bottomRightHeight, topRightHeight, topLeftHeight;
            if (TerrainDataReader.GetCellCornerHeights(globalX, globalZ, out bottomLeftHeight, out bottomRightHeight, out topRightHeight, out topLeftHeight))
            {
                block.bottomLeftHeight = bottomLeftHeight;
                block.bottomRightHeight = bottomRightHeight;
                block.topRightHeight = topRightHeight;
                block.topLeftHeight = topLeftHeight;
                
                // 判断解锁状态：高度为0表示解锁
                block.bottomLeftUnlocked = Mathf.Approximately(bottomLeftHeight, 0f);
                block.bottomRightUnlocked = Mathf.Approximately(bottomRightHeight, 0f);
                block.topRightUnlocked = Mathf.Approximately(topRightHeight, 0f);
                block.topLeftUnlocked = Mathf.Approximately(topLeftHeight, 0f);
                
                // 分类CellBlock类型
                int unlockedCorners = 0;
                if (block.bottomLeftUnlocked) unlockedCorners++;
                if (block.bottomRightUnlocked) unlockedCorners++;
                if (block.topRightUnlocked) unlockedCorners++;
                if (block.topLeftUnlocked) unlockedCorners++;
                
                if (unlockedCorners == 4)
                    block.blockType = CellBlockType.FullUnlocked;
                else if (unlockedCorners == 0)
                    block.blockType = CellBlockType.FullLocked;
                else
                    block.blockType = CellBlockType.PartialUnlocked;
            }
            else
            {
                // 默认值
                block.bottomLeftHeight = block.bottomRightHeight = block.topRightHeight = block.topLeftHeight = 12f;
                block.bottomLeftUnlocked = block.bottomRightUnlocked = block.topRightUnlocked = block.topLeftUnlocked = false;
                block.blockType = CellBlockType.FullLocked;
            }
            
            return block;
        }
        
        /// <summary>
        /// 将CellBlock数据存储到全局数组中（仅在编辑器中用于Gizmo显示）
        /// </summary>
        private void StoreCellBlocksToGlobalArray(int startGridX, int startGridZ, int blockGridCountX, int blockGridCountZ)
        {
            for (int localZ = 0; localZ < blockGridCountZ; localZ++)
            {
                for (int localX = 0; localX < blockGridCountX; localX++)
                {
                    int globalX = startGridX + localX;
                    int globalZ = startGridZ + localZ;
                    
                    // 确保全局坐标在有效范围内
                    if (globalX >= 0 && globalX < gridCountX && globalZ >= 0 && globalZ < gridCountZ)
                    {
                        allCellBlocks[globalX, globalZ] = CreateDebugCellBlock(localX, localZ, startGridX, startGridZ);
                    }
                }
            }
        }
        
        /// <summary>
        /// 记录地形统计信息
        /// </summary>
        private void LogTerrainStatistics()
        {
            int unlockedCount = 0;
            int totalDataCells = TerrainDataReader.DataGridCountX * TerrainDataReader.DataGridCountZ;
            
            // 统计解锁的格子数量
            for (int x = 0; x < TerrainDataReader.DataGridCountX; x++)
            {
                for (int z = 0; z < TerrainDataReader.DataGridCountZ; z++)
                {
                    float bl, br, tr, tl;
                    if (TerrainDataReader.GetCellCornerHeights(x, z, out bl, out br, out tr, out tl))
                    {
                        // 如果四个角点都是0高度，认为是解锁区域
                        if (Mathf.Approximately(bl, 0f) && Mathf.Approximately(br, 0f) && 
                            Mathf.Approximately(tr, 0f) && Mathf.Approximately(tl, 0f))
                        {
                            unlockedCount++;
                        }
                    }
                }
            }
            
            Debug.Log($"解锁区域: {unlockedCount}/{totalDataCells} ({(float)unlockedCount / totalDataCells * 100:F1}%)");
            Debug.Log($"显示精度: {cellSize}m，数据精度: {TerrainDataReader.DataCellSize}m");
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !systemInitialized) return;
            
            // 绘制CellBlock类型的Gizmo
            if (showCellBlockGizmos)
            {
                DrawCellBlockGizmos();
            }
            
            // 绘制可见mesh块的边界
            if (showVisibleMeshBounds)
            {
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
        /// 绘制CellBlock类型的Gizmo
        /// </summary>
        private void DrawCellBlockGizmos()
        {
            if (allCellBlocks == null) return;
            
            for (int x = 0; x < gridCountX; x++)
            {
                for (int z = 0; z < gridCountZ; z++)
                {
                    CellBlock cellBlock = allCellBlocks[x, z];
                    
                    // 根据CellBlockType设置颜色
                    Color gizmoColor = GetCellBlockGizmoColor(cellBlock.blockType);
                    Gizmos.color = gizmoColor;
                    
                    // 计算世界坐标位置（格子中心点，y=0）
                    float worldX = x * cellSize + cellSize * 0.5f;
                    float worldZ = z * cellSize + cellSize * 0.5f;
                    Vector3 position = new Vector3(worldX, 0f, worldZ);
                    
                    // 绘制立方体
                    Vector3 size = new Vector3(cellSize * 0.9f, 0.1f, cellSize * 0.9f); // 稍微小一点以便区分
                    Gizmos.DrawCube(position, size);
                }
            }
        }
        
        /// <summary>
        /// 获取CellBlockType对应的Gizmo颜色
        /// </summary>
        private Color GetCellBlockGizmoColor(CellBlockType blockType)
        {
            switch (blockType)
            {
                case CellBlockType.FullUnlocked:
                    return Color.white; // 完全解锁 - 白色
                    
                case CellBlockType.PartialUnlocked:
                    return new Color(1.0f, 0.0f, 0.0f, 1f); // 部分解锁 - 红色
                    
                case CellBlockType.AdjacentUnLocked:
                    return new Color(0.0f, 1.0f, 0.0f, 1f); // 相邻未锁定 - 绿色
                    
                case CellBlockType.FullLocked:
                default:
                    return new Color(0.0f, 0.0f, 0.0f, 1f); // 完全锁定 - 黑色
            }
        }
        
#endif
    }
} 