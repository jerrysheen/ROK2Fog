# 迷雾系统设计文档 v2.0

## 概述
这是一个用于Unity的增强版迷雾系统，支持多种地形类型、解锁状态管理和外部配置文件。用于创建复杂的战争迷雾效果。

## 主要特性 v2.0
- **多种地形类型**：支持平原、丘陵、山脉、湖泊、森林五种地形
- **解锁状态管理**：支持区域解锁/未解锁状态跟踪
- **外部配置系统**：通过JSON文件配置地形参数，无需修改代码
- **动态网格生成**：根据解锁状态动态切换迷雾/已解锁网格
- **高度差异化**：不同地形类型有明显的高度差异
- **性能优化的区块加载**：只渲染玩家周围的网格
- **调试信息**：提供详细的地形统计信息

## 架构 v2.0
该系统分为三个主要部分：

### 1. 核心组件
- `TerrainDataReader` - 处理地形数据读取、解锁状态管理
- `FogSystem` - 处理迷雾渲染、网格管理和解锁逻辑

### 2. 数据结构
- `TerrainType` - 地形类型枚举（平原、丘陵、山脉、湖泊、森林）
- `TerrainCell` - 地形格子数据结构（高度、类型、解锁状态、噪声值）
- `TerrainDataConfig` - 配置数据结构（种子、高度、权重等）

### 3. 配置系统
- `StreamingAssets/terrain_config.json` - 主配置文件
- 支持运行时读取外部配置
- 多种预设配置示例

## 地形生成算法
1. **基础噪声生成**：使用Perlin噪声生成基础高度
2. **地形类型确定**：根据权重分布和额外噪声确定地形类型
3. **高度计算**：根据地形类型和噪声变化计算最终高度
4. **解锁状态初始化**：离地图中心近的区域初始解锁

## 解锁系统
- **初始解锁**：地图中心10%范围初始解锁
- **玩家解锁**：玩家移动时自动解锁周围区域
- **手动解锁**：支持代码调用解锁指定区域
- **网格更新**：解锁状态变化时自动更新网格渲染

## 配置参数
### 基础设置
- `seed`: 随机种子
- `noiseScale`: 噪声缩放（推荐0.005-0.02）

### 高度设置
- `mountainHeight`: 山脉高度（推荐15-30米）
- `hillHeight`: 丘陵高度（推荐5-15米）
- `plainHeight`: 平原高度（推荐1-5米）
- `lakeDepth`: 湖泊深度（负值，推荐-1到-5米）
- `forestHeight`: 森林高度（推荐3-10米）

### 分布权重
- `plainWeight`: 平原权重（推荐0.3-0.5）
- `hillWeight`: 丘陵权重（推荐0.2-0.3）
- `mountainWeight`: 山脉权重（推荐0.1-0.3）
- `lakeWeight`: 湖泊权重（推荐0.05-0.2）
- `forestWeight`: 森林权重（推荐0.05-0.2）

## 使用方法
1. 将 `FogSystem` 脚本附加到场景中的游戏对象上
2. 配置材质：设置 `fogMaterial`（迷雾材质）和 `unlockedMaterial`（已解锁区域材质）
3. 设置玩家引用：指定 `playerTransform`
4. 调整参数：配置地图大小、网格大小、解锁半径等
5. 修改配置文件：编辑 `StreamingAssets/terrain_config.json` 调整地形特性

## API接口
### FogSystem公共方法
- `UnlockArea(Vector3 centerWorldPos, float radius)` - 解锁指定区域
- `TerrainReader` - 获取地形数据读取器

### TerrainDataReader公共方法
- `GetHeight(int x, int z)` - 获取指定坐标高度
- `GetTerrainCell(int x, int z)` - 获取地形数据
- `IsUnlocked(int x, int z)` - 检查是否已解锁
- `SetUnlocked(int x, int z, bool unlocked)` - 设置解锁状态
- `UnlockArea(Vector3 centerWorldPos, float radius)` - 解锁区域

## 性能优化
- **区块加载**：只渲染玩家周围3x3范围的网格
- **动态管理**：根据距离动态创建/销毁网格
- **状态缓存**：缓存地形和解锁状态，避免重复计算
- **批量处理**：批量更新网格状态

## 扩展性
- **模块化设计**：地形数据和渲染系统分离
- **配置驱动**：通过外部文件配置，易于调整
- **接口丰富**：提供多种API供外部调用
- **类型扩展**：可以轻松添加新的地形类型

## 调试功能
- **统计信息**：显示各种地形类型的分布统计
- **解锁进度**：显示当前解锁区域百分比
- **颜色编码**：网格顶点根据地形类型着色
- **调试开关**：可控制是否显示调试信息

## 配置文件示例

### 默认配置 (terrain_config.json)
```json
{
    "seed": 12345,
    "noiseScale": 0.01,
    "mountainHeight": 15.0,
    "hillHeight": 8.0,
    "plainHeight": 2.0,
    "lakeDepth": -2.0,
    "forestHeight": 5.0,
    "plainWeight": 0.4,
    "hillWeight": 0.25,
    "mountainWeight": 0.15,
    "lakeWeight": 0.1,
    "forestWeight": 0.1
}
```

### 山地配置示例
```json
{
    "seed": 54321,
    "noiseScale": 0.008,
    "mountainHeight": 25.0,
    "hillHeight": 12.0,
    "plainHeight": 1.5,
    "lakeDepth": -4.0,
    "forestHeight": 7.0,
    "plainWeight": 0.2,
    "hillWeight": 0.2,
    "mountainWeight": 0.3,
    "lakeWeight": 0.2,
    "forestWeight": 0.1
}
```

## 代码示例

### 基础设置
```csharp
// 创建FogSystem
GameObject fogSystemObj = new GameObject("FogSystem");
FogSystem fogSystem = fogSystemObj.AddComponent<FogSystem>();

// 配置基本参数
fogSystem.mapWidth = 500;
fogSystem.mapHeight = 500;
fogSystem.cellSize = 1f;
fogSystem.meshSize = 50;
fogSystem.playerTransform = playerTransform;
fogSystem.unlockRadius = 10f;
```

### 解锁区域
```csharp
// 解锁玩家周围区域
fogSystem.UnlockArea(playerPosition, 20f);

// 检查解锁状态
bool isUnlocked = fogSystem.TerrainReader.IsUnlocked(x, z);

// 获取地形信息
TerrainCell cell = fogSystem.TerrainReader.GetTerrainCell(x, z);
Debug.Log($"地形类型: {cell.terrainType}, 高度: {cell.height}");
```

### 获取统计信息
```csharp
// 在FogSystem中添加的调试功能会自动记录统计信息
// 可以通过Debug.Log查看地形分布和解锁进度
```

## 未来改进方向
1. **LOD系统**：根据距离使用不同精度的网格
2. **材质混合**：支持地形类型间的平滑过渡
3. **动画效果**：解锁时的动画效果
4. **保存系统**：持久化解锁状态
5. **网络同步**：多人游戏中的状态同步
6. **地形编辑器**：可视化地形编辑工具
7. **更多地形类型**：支持更多样化的地形（沙漠、雪地等）
8. **高度图导入**：支持从外部高度图导入地形数据

## 注意事项
- 所有权重的和应该为1.0
- 高度值可以为负数（用于湖泊等）
- noiseScale建议在0.005到0.02之间
- 修改配置后需要重新生成地形才能看到效果
- StreamingAssets文件夹中的文件会被打包到最终构建中
- 建议根据目标平台调整网格大小和细节级别
