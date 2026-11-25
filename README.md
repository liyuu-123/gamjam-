# 走错也算数 · 网格回合制版

一个使用 Unity 2022.3.62f2c1 制作的 2D 像素网格回合制游戏。

玩家回合选择一个行动，随后进入世界回合。玩家、近战敌人、远程敌人和 Boss 都按格移动。主角拥有八向方向攻击、四向视野、影子路线、灵魂出窍、可搬墙、伤害陷阱和记忆冰晶。

## 当前玩法

| 按键 | 作用 |
|---|---|
| `WASD` / 方向键 | 玩家或虚影向上下左右移动一格 |
| 鼠标 | 量化为八方向瞄准 |
| 鼠标左键 | 朝当前八向选择方向发动方向斩 |
| `F` | 拆墙、补缺口、触发冰晶 |
| `Q` | 灵魂出窍 / 收回虚影 |
| `E` | 开始或结束路线记录 |
| `空格` | 原地等待一个世界回合 |
| `Esc` | 暂停、返回 |
| `R` | 死亡后重生或重新开始 |
| `M` | 结算后返回选图 |
| `1` / `2` / `3` | 标题页选择森林 / 冰原 / 沙漠 |

### 回合规则

- 玩家回合只执行一个主行动，瞄准本身不消耗回合。
- 世界回合处理敌人预警、远程蓄力、弹体移动、方向招式和残影伤害。
- 玩家方向斩会立即命中该方向三格内最近的敌人，墙体阻挡攻击。
- 近战敌人在玩家四格内会标记玩家当前地板；玩家移动离开后不会受伤。
- 远程敌人先在第一个世界回合准备，第二个世界回合才发射弹体。弹体在黑暗中仍然可见。
- 敌人在玩家两格半径内即可被发现，即使所在格尚未被照亮。
- 近战预警会把整个目标格染成高亮红色，并持续闪烁。
- 所有小怪都有头顶血条，Boss 使用屏幕上方的大血条。
- Boss 在生命值降到 50% 时切换为红色二阶段立绘，暂不新增攻击方式。
- 绿色伤害陷阱被踩中时扣除 1 点生命。
- 玩家死亡后固定回到地图出生格。Boss 当前血量会保留，但不会立即重新索敌，必须重新进入 Boss 区域才会再次激活。

### 视野与虚影

- 身体只显示自身格和上下左右四个相邻格。
- 当前可见格为灰色，已探索格保留暗灰轮廓，未探索格接近纯黑。
- 敌人、冰晶、微光和出口不在当前视野内时隐藏。
- `Q` 让蓝色半透明虚影离体。虚影可以穿墙、缺口和敌人，但不能离开地图边界。
- 虚影移动时身体留在原地，仍然会被敌人方向攻击和远程弹体命中。
- 虚影每前进一格消耗 1 格影子，并触发一个世界回合。
- 已走过的路线和虚影照过的格会永久保留为已探索区域。

### 影子与残影

- 影子资源使用整数格数，上限 12 格。
- 路线记录每移动一格消耗 1 格影子，结束后生成战斗残影。
- 战斗残影会在敌人移动或停留到旧路线时造成伤害。
- 虚影探路和战斗残影不能同时启用。

## 打开与运行

1. 用 Unity Hub 添加 `src/UnityProject`。
2. 使用 Unity `2022.3.62f2c1` 打开。
3. 打开 `Assets/Scenes/Main.unity`。
4. 点击 Play，或直接运行 `src/UnityProject/Build/Windows/WrongTurnCounts.exe`。

当前场景中的格子视图、UGUI、生成点和角色 Prefab 都是编辑模式下生成并保存的对象，不依赖运行后临时生成整套地图或界面。

## 地图与素材

三张现有地图保持 28×20。新地图支持通过纯色 PNG 导入：

- 黑：墙
- 白 / 浅灰：地板
- 黄：可搬墙
- 红：缺口
- 绿：伤害陷阱

使用 Unity 菜单：

`Wrong Turn > Turn-Based > Import Color Map...`

导入器按每格中心像素采样并映射到 Tilemap。可选透明物件层支持：

- 品红：玩家出生点
- 橙：近战敌人
- 青：远程敌人
- 紫：Boss
- 水蓝：记忆冰晶
- 亮黄：微光
- 白：出口

角色素材来源：

- `assets/images/地头龙.png`：近战敌人
- `assets/images/困倦法师.png`：远程敌人
- `assets/images/New Piskel (2).png`：Boss 5×5 帧图，最后三帧为二阶段
- `assets/images/1795444240784.png`：UGUI 面板、按钮、条形资源和主角头像
- `assets/images/地牢图.png`：地板、墙体、缺口、可搬墙、陷阱和出口门的地牢视觉来源

## 代码结构

| 文件 | 职责 |
|---|---|
| `Assets/Scripts/TurnBased/GridTurnSession.cs` | 回合规则、移动、方向攻击、敌人 AI、Boss、影子、视野和陷阱 |
| `Assets/Scripts/TurnBased/GridTurnController.cs` | 场景输入、摄像机、逐格表现、音频和运行时自检 |
| `Assets/Scripts/TurnBased/GridTurnHud.cs` | UGUI 标题、HUD、暂停、记忆选择和结算界面 |
| `Assets/Scripts/TurnBased/GridMapView.cs` | 格子地图可见性渲染 |
| `Assets/Scripts/TurnBased/GridTileView.cs` | 单格地形、迷雾、陷阱、路线和危险格表现 |
| `Assets/Editor/TurnBasedSceneBuilder.cs` | 素材切分、场景重建、UGUI 构建和美术验证 |
| `Assets/Editor/MapImageImporter.cs` | 纯色地图 PNG 转 Tilemap 与场景生成点 |
| `Assets/Editor/TurnBasedRuleTests.cs` | 回合规则自动测试 |

旧的 `ActionGameController.cs`、`GameController.cs` 和 `GameSession.cs` 保留作历史参考，但不是当前 `Main.unity` 的运行入口。

## 自动验证

Unity 命令行验证：

```powershell
& 'D:\Unity\2022.3.62f2\Editor\Unity.exe' `
  -batchmode -nographics -quit `
  -projectPath 'D:\Unity\PROGRAM\GJ Project\src\UnityProject' `
  -executeMethod WrongTurn.EditorTools.TurnBasedSceneBuilder.BatchVerify `
  -logFile 'D:\Unity\PROGRAM\GJ Project\src\UnityProject\Logs\turnbased-verify.log'
```

构建 Windows 播放器：

```powershell
& 'D:\Unity\2022.3.62f2\Editor\Unity.exe' `
  -batchmode -nographics -quit `
  -projectPath 'D:\Unity\PROGRAM\GJ Project\src\UnityProject' `
  -executeMethod WrongTurn.EditorTools.ProjectBuilder.BuildWindowsPlayer `
  -logFile 'D:\Unity\PROGRAM\GJ Project\src\UnityProject\Logs\turnbased-player-build.log'
```

运行时自检：

```powershell
& 'D:\Unity\PROGRAM\GJ Project\src\UnityProject\Build\Windows\WrongTurnCounts.exe' `
  -batchmode -nographics `
  -gridtest 'D:\Unity\PROGRAM\GJ Project\src\UnityProject\Logs\turnbased-runtime-test.txt' `
  -logFile 'D:\Unity\PROGRAM\GJ Project\src\UnityProject\Logs\turnbased-runtime.log'
```

当前自动测试覆盖四向视野、两格敌人感知、陷阱、方向攻击与墙体阻挡、近战红格躲避、远程蓄力与弹体、虚影穿墙、出窍时本体受伤、影子格消耗、Boss 二阶段、地图连通性和 UGUI/素材完整性。

## 目录

```text
src/UnityProject/
├── Assets/
│   ├── Editor/
│   ├── Resources/Art/
│   ├── Resources/UI/
│   ├── Scenes/Main.unity
│   └── Scripts/TurnBased/
├── Build/Windows/            本地 Windows 构建输出
├── Logs/                     验证日志与截图
├── Packages/
└── ProjectSettings/

assets/images/                原始角色、地图与 UI 素材
build/                        旧版 v3.0 成品压缩包
docs/                         旧版说明与验收记录
```
