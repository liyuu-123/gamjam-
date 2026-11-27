# 走错也算数 · 三境冒险版 v3.0

一个用 Unity 2D 从空白工程独立完成的实时动作单机小游戏。

固定地图、明亮高密度的程序化像素场景、连续移动、鼠标瞄准、环戒连射、闪避无敌帧、路线残影、发光幽灵、房间改造、Boss 战、重生留光和两颗记忆冰晶，已完整接入可玩闭环。

v3.0 接入了蓝发主角的四方向像素形象、透明头像和六首场景音乐，并提供**森林 / 冰原 / 沙漠**三张可选择地图。

---

## 一、怎么玩

### 方式 A：直接下载成品（推荐，不用装 Unity）

1. 打开仓库里的 `build/走错也算数_三境冒险版_v3.0_交付包.zip`
2. 点右上角 **Download** 下载（约 50 MB，单文件下载，不需要 clone 整个仓库）
3. 解压，进入 `Windows/` 目录，双击 **`WrongTurnCounts.exe`**

> 当前只提供 Windows 64 位版本。

### 方式 B：从源码运行

见下方「三、打开工程」。需要 Unity **2022.3.62f2c1**。

---

## 二、操作

| 按键 | 作用 |
|---|---|
| `WASD` / 方向键 | 连续移动，带加减速 |
| 鼠标 | 自由瞄准，带轻微瞄准吸附 |
| 鼠标左键 / `空格` | 按住连续投环 |
| `Shift` | 短距闪避并进入短暂无敌 |
| 按住 `E` | 边走边记录路线，松开后生成战斗残影 |
| `Q` | 放出 / 收回探路幽灵 |
| `F` | 拆墙、铺缺口、触碰冰晶 |
| `Esc` | 暂停、查看操作、重新开局 |
| `R` | 胜利、失败或暂停界面重新开局 |
| `T` | 跳过当前新手引导 |
| `1` / `2` / `3` | 标题页选择森林 / 冰原 / 沙漠地图 |

三张地图分别配置 **9 / 10 / 12** 名敌人。青色战斗残影已加宽，便于在激烈战斗中判断伤害路线。

---

## 三、打开工程

1. 用 Unity Hub 添加 `src/UnityProject` 文件夹
2. 用 Unity `2022.3.62f2c1` 打开
3. 打开 `Assets/Scenes/Main.unity`
4. 点击 Play

工程里**不含** `Library/`、`Build/`、`Logs/` 等生成目录（已由 `.gitignore` 排除），首次打开时 Unity 会自动重建，需要等几分钟。

---

## 四、目录结构

```
gamjam-/
├── README.md
├── build/                       成品包：解压即玩，队友直接下这个
│   └── 走错也算数_三境冒险版_v3.0_交付包.zip
├── docs/                        说明文档与验收记录
│   ├── 走错也算数_三境冒险版说明_v3.0.docx
│   ├── 走错也算数_三境冒险版说明_v3.0.pdf
│   ├── RT_autotest_v3.0.txt     打包与运行时自动验收输出
│   └── verification_maps_*.png  三地图与标题页验收截图
├── src/
│   └── UnityProject/            完整可打开的 Unity 工程
│       ├── Assets/
│       │   ├── Scripts/         游戏逻辑
│       │   ├── Resources/Art/   主角四方向图与透明头像
│       │   ├── Resources/Music/ 六首场景音乐
│       │   ├── Scenes/          Main.unity
│       │   └── Editor/          ProjectBuilder.cs（场景生成 / 打包 / 自动验收）
│       ├── Packages/
│       └── ProjectSettings/
└── assets/                      素材原文件（工程内已引用，这里留档）
    ├── music/                   六首音乐原始文件
    └── images/                  角色、地图、参考图
```

### 关键脚本

| 文件 | 职责 |
|---|---|
| `Assets/Scripts/ActionGameController.cs` | 实时移动、连射、闪避、敌人 AI、Boss、引导、重生与胜负逻辑 |
| `Assets/Scripts/PixelArtHD.cs` | 64 像素级明亮程序化素材生成 |
| `Assets/Scripts/ProceduralAudio.cs` | 程序化音效与环境底噪 |
| `Assets/Editor/ProjectBuilder.cs` | 场景生成、Windows 打包与自动验收 |

---

## 五、音乐与素材

| 游戏内名称 | 文件名 | 用途 |
|---|---|---|
| 雾之路 | `Exploration_Mist.mp3` | 探索 |
| 森之牢 | `Exploration_Forest.mp3` | 森林地图 |
| 冰之城 | `Exploration_Ice.mp3` | 冰原地图 |
| 黑之堡 | `Boss_Castle.mp3` | Boss 战 |
| 攻无不克 | `Boss_VictoryPath.mp3` | Boss 第二阶段 |
| 功成时刻 | `Victory_Moment.mp3` | 胜利 |

---

## 六、验收情况

已通过 Unity 打包与运行时自动验收：

- 四方向主角与透明头像加载
- 六首音乐加载与切换
- 森林 / 冰原 / 沙漠三张地图连通性
- 9 / 10 / 12 敌人数正确
- 新手引导链完整
- Boss 开门条件
- 死亡扣命与重生复位
- 前世光路继承

---

## 七、版本记录

| 版本 | 内容 |
|---|---|
| v3.0 | 三境冒险版：三张可选择地图、四方向主角、透明头像、六首音乐 |
| v2.1 | 角色与音乐版 |
| v2.0 | 实时动作版 |
| v1.x | 回合制版 |

---

## 八、团队协作约定

- **拉代码**：`git clone https://github.com/liyuu-123/gamjam-.git`
- **只要成品**：别 clone，直接在网页上下 `build/` 里的 zip。
- **改代码**：`src/UnityProject` 是唯一工程目录，不要在别处另开副本，避免 `.meta` 冲突。
- **提交前**：确认 `Library/`、`Build/` 没有被加进来（`.gitignore` 已挡）。
- **出新的成品包**：放 `build/`，文件名带版本号；旧版本保留不动，方便对比。
- **大文件注意**：GitHub 单文件上限 100 MB，超过就必须换方案（Release 附件或网盘）。
