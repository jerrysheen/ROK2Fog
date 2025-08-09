using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FogQuadNode
{
    public struct BoundsAABB
    {
        public Vector2Int MinXY, MaxXY;

        public BoundsAABB(Vector2Int min, Vector2Int max)
        {
            MinXY = min;
            MaxXY = max;
        }
    }

    // 所有节点规避掉地图上的缩放， 比如400 x 400，就是单纯的格子。
    // 如果不与服务器同步数据时，格子只会被标记为脏，每次重新登录的时候，根据服务器下发的数据可以合并大mesh。
    // 只对 25x25的最小格子进行致密重建，
    // 需要标记是否修改，以便于重建。
    private FogQuadNode _leftBottomNode;
    private FogQuadNode _leftTopNode;
    private FogQuadNode _rightBottomNode;
    private FogQuadNode _rightTopNode;
    private Vector2Int _size;
    private Vector2Int _startPos;
    private Vector2Int _endPos;
    // 当endPos - startPos < minNodesize，确认为最小叶子节点，不再递归，目前以25,25作为底格。
    private Vector2Int _minNodeSize = new Vector2Int(25,25);
    private bool isLeafNode = false;
    private bool isMaxDepth = false;
    private FogNodeType _nodeType;
    
    public FogQuadNode(Vector2Int startPos, Vector2Int endPos)
    {
        _startPos = startPos;
        _endPos = endPos;
        _nodeType = FogNodeType.UnInitialized;
        Vector2Int _size = _endPos - _startPos;
        if (_size.x <= _minNodeSize.x && _size.y <= _minNodeSize.y)
        {
            isMaxDepth = true;
        }
        isLeafNode = true;
    }

    // 公开只读访问器，供Editor可视化
    public Vector2Int StartPos => _startPos;
    public Vector2Int EndPos => _endPos;
    public FogNodeType NodeType => _nodeType;
    public bool IsLeafNode => isLeafNode;
    public bool HasChildren => _leftBottomNode != null || _leftTopNode != null || _rightBottomNode != null || _rightTopNode != null;

    // 整个区域全解锁或者不解锁。
    public void UpdateArea()
    {
        
    }

    // 插入一个节点后，进行格子划分，会直接将25x25的区域标记为脏
    // 比如400 x 400的网格， 解锁4，4点， 那么会生成多个3个200 x 200， 3个100x100，3个50x50， 3个25x25
    public void Insert(BoundsAABB boundsAABB, FogNodeType nodeType)
    {
        if (!IsInBound(boundsAABB))
        {
            Debug.Log($"Not in the bounds of {_startPos}, {_endPos}");
            return;
        }

        if (IsOverlapping(boundsAABB))
        {
            Debug.Log($"Is overlapping with {_startPos}, {_endPos}");
            _nodeType = nodeType;
            isLeafNode = true;
            if (_leftBottomNode != null)
            {
                _leftBottomNode.Delete();
                _leftBottomNode = null;
            }

            if (_leftTopNode != null)
            {
                _leftTopNode.Delete();
                _leftTopNode = null;
            }
            
            if (_rightBottomNode != null)
            {
                _rightBottomNode.Delete();
                _rightBottomNode = null;
            } 
            
            if (_rightTopNode != null)
            {
                _rightTopNode.Delete();
                _rightTopNode = null;
            }
            return;
        }

        // 如果已经到了最大深度，并且没有overlap 
        if (isMaxDepth)
        { 
            if(_nodeType != nodeType) _nodeType = FogNodeType.PartiallyUnlocked;
            return;
        }
        
        // 判断是否为当前的LeafNode，Leafnode接收到一个和状态一样的解锁消息，不需要细分。
        if (isLeafNode)
        {
            if (_nodeType == nodeType) return;
        }

        SubDivide();
        isLeafNode = false;
        _leftBottomNode.Insert(boundsAABB, nodeType);
        _rightBottomNode.Insert(boundsAABB, nodeType);
        _leftTopNode.Insert(boundsAABB, nodeType);
        _rightTopNode.Insert(boundsAABB, nodeType);
        
    }

    public void Delete()
    {
        if (_leftBottomNode != null)
        {
            _leftBottomNode.Delete();
            _leftBottomNode = null;
        }

        if (_leftTopNode != null)
        {
            _leftTopNode.Delete();
            _leftTopNode = null;
        }
            
        if (_rightBottomNode != null)
        {
            _rightBottomNode.Delete();
            _rightBottomNode = null;
        } 
            
        if (_rightTopNode != null)
        {
            _rightTopNode.Delete();
            _rightTopNode = null;
        }

        
    }



    public void SubDivide()
    {
        // 如果已经细分过，直接返回，避免重建子树导致历史数据丢失
        if (_leftBottomNode != null || _leftTopNode != null || _rightBottomNode != null || _rightTopNode != null)
        {
            return;
        }

        Vector2Int mid = (_endPos + _startPos) / 2;
        _leftBottomNode = new FogQuadNode(_startPos, mid);
        _leftTopNode = new FogQuadNode(new Vector2Int(_startPos.x, mid.y), new Vector2Int(mid.x, _endPos.y));
        _rightTopNode = new FogQuadNode(mid, _endPos);
        _rightBottomNode = new FogQuadNode(new Vector2Int(mid.x, _startPos.y), new Vector2Int(_endPos.x , mid.y));
        _leftBottomNode._nodeType = _nodeType;
        _leftTopNode._nodeType = _nodeType;
        _rightTopNode._nodeType = _nodeType;
        _rightBottomNode._nodeType = _nodeType;
    }

    public bool IsInBound(BoundsAABB boundsAABB)
    {
        return boundsAABB.MinXY.x < _endPos.x &&
               boundsAABB.MaxXY.x > _startPos.x &&
               boundsAABB.MinXY.y < _endPos.y &&
               boundsAABB.MaxXY.y > _startPos.y;
    }

    // 如果传入的aabb完全覆盖当前的grid，就回传true
    public bool IsOverlapping(BoundsAABB boundsAABB)
    {
        return boundsAABB.MinXY.x <= _startPos.x && boundsAABB.MaxXY.x >= _endPos.x &&
               boundsAABB.MinXY.y <= _startPos.y && boundsAABB.MaxXY.y >= _endPos.y; 
    }

    // 遍历当前节点及其子节点（前序）
    public void ForEachNode(System.Action<FogQuadNode> visitor)
    {
        if (visitor == null) return;
        visitor(this);
        _leftBottomNode?.ForEachNode(visitor);
        _rightBottomNode?.ForEachNode(visitor);
        _leftTopNode?.ForEachNode(visitor);
        _rightTopNode?.ForEachNode(visitor);
    }
}

public enum FogNodeType
{
    UnInitialized = 0,
    FullyUnlock = 1,
    FullyLocked = 2,
    PartiallyUnlocked = 3,
}

