using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestQuadNode : MonoBehaviour
{
    [Header("QuadTree 配置")]
    [SerializeField] private Vector2Int mapSize = new Vector2Int(400, 400);

    [Header("编辑操作（以格子坐标为单位）")]
    [SerializeField] public Vector2Int minXY = new Vector2Int(15, 15);
    [SerializeField] public Vector2Int maxXY = new Vector2Int(45, 45);
    [SerializeField] public FogNodeType editNodeType = FogNodeType.FullyUnlock;

    private FogQuadNode rootNode;

    public FogQuadNode Root => rootNode;

    void Start()
    {
        EnsureRoot();
    }

    private void EnsureRoot()
    {
        if (rootNode == null)
        {
            rootNode = new FogQuadNode(Vector2Int.zero, mapSize);
        }
    }

    public void RecreateRoot()
    {
        rootNode = new FogQuadNode(Vector2Int.zero, mapSize);
    }

    public void InsertCurrentArea()
    {
        EnsureRoot();
        var aabb = new FogQuadNode.BoundsAABB(minXY, maxXY);
        rootNode.Insert(aabb, editNodeType);
    }

    public void RemoveCurrentArea()
    {
    }

    public void ClearAll()
    {
        if (rootNode != null)
        {
            rootNode.Delete();
            rootNode = null;
        }
    }

    void Update()
    {
        
    }

    // 统计信息结构
    public struct NodeStatistics
    {
        public int totalNodes;
        public int leafNodes;
        public int internalNodes;

        public int fullyUnlockNodes;
        public int fullyLockedNodes;
        public int partiallyUnlockedNodes;
    }

    // 计算整棵树的节点统计
    public NodeStatistics ComputeNodeStatistics()
    {
        NodeStatistics stats = new NodeStatistics();
        if (rootNode == null)
        {
            return stats;
        }

        rootNode.ForEachNode(node =>
        {
            stats.totalNodes++;
            if (node.IsLeafNode) stats.leafNodes++; else stats.internalNodes++;

            switch (node.NodeType)
            {
                case FogNodeType.FullyUnlock:
                    stats.fullyUnlockNodes++;
                    break;
                case FogNodeType.FullyLocked:
                    stats.fullyLockedNodes++;
                    break;
                case FogNodeType.PartiallyUnlocked:
                    stats.partiallyUnlockedNodes++;
                    break;
            }
        });

        return stats;
    }
}
