# 地形配置系统说明

## 概述
这个文件夹包含了地形生成系统的配置文件。通过修改这些JSON文件，你可以在不修改代码的情况下调整地形特性。

## 主配置文件
- `terrain_config.json` - 默认配置文件，系统会自动加载这个文件
- `terrain_config_example2.json` - 示例配置文件，展示不同的地形设置

## 配置参数说明

### 基础设置
- `seed`: 随机种子，相同的种子会生成相同的地形
- `noiseScale`: 噪声缩放，值越小地形变化越平缓，值越大变化越剧烈

### 高度设置
- `mountainHeight`: 山脉的基础高度（米）
- `hillHeight`: 丘陵的基础高度（米）
- `plainHeight`: 平原的基础高度（米）
- `lakeDepth`: 湖泊的深度（负值）（米）
- `forestHeight`: 森林的基础高度（米）

### 地形分布权重
这些权重决定了各种地形类型的出现概率，所有权重的和应该为1.0：
- `plainWeight`: 平原权重（推荐0.3-0.5）
- `hillWeight`: 丘陵权重（推荐0.2-0.3）
- `mountainWeight`: 山脉权重（推荐0.1-0.3）
- `lakeWeight`: 湖泊权重（推荐0.05-0.2）
- `forestWeight`: 森林权重（推荐0.05-0.2）

## 使用方法

### 修改配置
1. 编辑 `terrain_config.json` 文件
2. 保存文件
3. 在Unity中重新启动场景或重新初始化FogSystem

### 创建新配置
1. 复制现有的配置文件
2. 修改参数值
3. 重命名为 `terrain_config.json` 来替换默认配置
4. 或者修改代码中的配置文件路径来加载不同的配置

## 配置示例

### 平原为主的配置
```json
{
    "seed": 12345,
    "noiseScale": 0.01,
    "mountainHeight": 10.0,
    "hillHeight": 5.0,
    "plainHeight": 2.0,
    "lakeDepth": -1.0,
    "forestHeight": 4.0,
    "plainWeight": 0.6,
    "hillWeight": 0.2,
    "mountainWeight": 0.05,
    "lakeWeight": 0.1,
    "forestWeight": 0.05
}
```

### 山地为主的配置
```json
{
    "seed": 54321,
    "noiseScale": 0.008,
    "mountainHeight": 25.0,
    "hillHeight": 15.0,
    "plainHeight": 1.0,
    "lakeDepth": -3.0,
    "forestHeight": 8.0,
    "plainWeight": 0.1,
    "hillWeight": 0.2,
    "mountainWeight": 0.5,
    "lakeWeight": 0.1,
    "forestWeight": 0.1
}
```

## 注意事项
- 所有权重的和应该为1.0
- 高度值可以为负数（用于湖泊等）
- noiseScale建议在0.005到0.02之间
- 修改配置后需要重新生成地形才能看到效果 