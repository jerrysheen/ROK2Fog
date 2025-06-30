using UnityEngine;

namespace FogSystem
{
    /// <summary>
    /// 迷雾系统配置
    /// 包含所有迷雾相关的可配置参数
    /// </summary>
    [CreateAssetMenu(fileName = "FogConfig", menuName = "FogSystem/FogConfig")]
    public class FogConfig : ScriptableObject
    {
        [Header("网格生成配置")]
        [Tooltip("网格单元格大小(米)")]
        public float cellSize = 18f;
        
        [Tooltip("最大面数")]
        public int maxTriangles = 2800;
        
        [Tooltip("目标面数")]
        public int targetTriangles = 2000;
        
        [Tooltip("边缘网格密度倍数")]
        [Range(1f, 5f)]
        public float edgeDensityMultiplier = 2f;
        
        [Header("区域管理配置")]
        [Tooltip("区域更新距离阈值")]
        public float updateDistance = 50f;
        
        [Tooltip("最大缓存距离")]
        public float maxCacheDistance = 200f;
        
        [Tooltip("缓存大小")]
        public int cacheSize = 16;
        
        [Header("高度调整配置")]
        [Tooltip("山地区域下压深度")]
        public float mountainDepthOffset = -2f;
        
        [Tooltip("河流区域下压深度")]
        public float riverDepthOffset = -1.5f;
        
        [Tooltip("高度平滑程度")]
        [Range(0f, 1f)]
        public float heightSmoothness = 0.3f;
        
        [Header("渲染配置")]
        [Tooltip("迷雾材质")]
        public Material fogMaterial;
        
        [Tooltip("遮蔽材质")]
        public Material occlusionMaterial;
        
        [Tooltip("迷雾高度")]
        public float fogHeight = 5f;
        
        [Tooltip("迷雾透明度")]
        [Range(0f, 1f)]
        public float fogAlpha = 0.8f;
        
        [Header("LOD配置")]
        [Tooltip("LOD距离阈值")]
        public float[] lodDistances = { 50f, 100f, 200f };
        
        [Tooltip("LOD细节级别")]
        public float[] lodDetailLevels = { 1f, 0.7f, 0.4f, 0.2f };
        
        [Header("调试配置")]
        [Tooltip("显示网格线框")]
        public bool showWireframe = false;
        
        [Tooltip("显示区域边界")]
        public bool showRegionBounds = false;
        
        [Tooltip("显示性能统计")]
        public bool showPerformanceStats = false;
        
        /// <summary>
        /// 验证配置参数
        /// </summary>
        private void OnValidate()
        {
            cellSize = Mathf.Max(1f, cellSize);
            maxTriangles = Mathf.Max(100, maxTriangles);
            targetTriangles = Mathf.Clamp(targetTriangles, 100, maxTriangles);
            updateDistance = Mathf.Max(1f, updateDistance);
            maxCacheDistance = Mathf.Max(updateDistance, maxCacheDistance);
            cacheSize = Mathf.Max(1, cacheSize);
            fogHeight = Mathf.Max(0.1f, fogHeight);
        }
        
        /// <summary>
        /// 根据距离获取LOD等级
        /// </summary>
        public int GetLODLevel(float distance)
        {
            for (int i = 0; i < lodDistances.Length; i++)
            {
                if (distance <= lodDistances[i])
                    return i;
            }
            return lodDistances.Length;
        }
        
        /// <summary>
        /// 获取LOD细节级别
        /// </summary>
        public float GetLODDetailLevel(int lodLevel)
        {
            if (lodLevel >= 0 && lodLevel < lodDetailLevels.Length)
                return lodDetailLevels[lodLevel];
            return lodDetailLevels[lodDetailLevels.Length - 1];
        }
    }
} 