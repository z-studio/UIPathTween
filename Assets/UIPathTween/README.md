# UI Path Tween

所见即所得的 **UGUI 路径动画**插件。在 Scene 视图拖动路径点与 Bezier 切线手柄，青色预览曲线即为运行时轨迹；由 [PrimeTween](https://github.com/KyryloKuzyk/PrimeTween) 驱动，支持循环、朝向与可选的 `async/await`。

---

## 功能

| 类别 | 能力 |
|------|------|
| 编辑 | Scene 实时预览；Inspector 内 Scrub / Preview（Edit Mode，非破坏性） |
| 曲线 | `Linear` / `CatmullRom` / `Bezier`（独立 in/out 切线手柄） |
| 播放 | 弧长匀速采样；PrimeTween `TweenSettings`（缓动含 Custom 曲线、循环、delay、`useUnscaledTime`）、`orient` |
| 程序化 | 静态 `Play()` 无需场景 Waypoint；支持 anchored / 世界坐标 / 自定义 Bezier 切线 |
| 异步 | 可选 UniTask `PlayAsync`（软依赖，未安装时自动跳过编译） |

---

## 环境要求

| 依赖 | Package | 必需 |
|------|---------|------|
| Unity 6 | — | ✅ 6000.0 或更高 |
| Unity UGUI | `com.unity.ugui` | ✅ |
| PrimeTween | `com.kyrylokuzyk.primetween` | ✅ |
| UniTask | `com.cysharp.unitask` | ⭕ 仅 `PlayAsync` 需要 |

PrimeTween 无法通过 UPM 解析时，请先从 Asset Store 或 Git 安装到项目。

---

## 安装

### Git UPM（推荐）

在目标项目 `Packages/manifest.json` 中添加：

```json
{
  "dependencies": {
    "com.zstudio.uipathtween": "https://github.com/z-studio/UIPathTween.git?path=Assets/UIPathTween"
  }
}
```

### 本地 UPM

将本包文件夹放入 `Packages/com.zstudio.uipathtween/`（文件夹名与 `package.json` 的 `name` 一致）。

### 拷贝到 Assets

将整个 `UIPathTween` 文件夹放入 `Assets/` 下即可，Unity 会按 `asmdef` 自动编译。

---

## 快速开始

### 1. 场景编辑模式

在 Canvas 下搭建如下层级：

```
Canvas
└── FlyPath                 ← 挂 UIPathTween
    ├── Waypoint_0
    ├── Waypoint_1
    ├── Waypoint_2
    └── Coin                  ← Target（要移动的 UI）
```

**规则：** `Target` 与所有 `Waypoint` 必须是 `FlyPath` 的**直接子节点**，共用同一 anchored 坐标系。

添加组件：**Component → ZStudio → UI → UI Path Tween**

| Inspector 字段 | 说明 |
|----------------|------|
| Target / Waypoints | 动画目标与路径点（按顺序） |
| Curve Mode | 推荐 **Bezier**（可调 S 形） |
| Tween Settings | 时长、缓动（`Ease.Custom` 可编辑 `AnimationCurve`）、循环（`-1` = 无限）、delay、`useUnscaledTime` 等；路径匀速建议 **Linear** |
| Orient | 让 Target 朝向前进方向 |
| Snap To Start On Play | 播放前移到起点 |

Waypoint 上可挂 **`UIPathWaypoint`** 控制 Bezier 切线（蓝色 Out / 橙色 In）。勾选 **Auto Tangents** 时切线由相邻点自动推算。

Inspector 工具：

- **Scrub Path** — Edit Mode 沿路径试看（非破坏性，不写脏、不进 Undo）
- **Preview In Scene** — Edit Mode 实时播放，停止后自动还原位姿
- **Reset Target To Original** — 手动还原 Scrub/预览前的位姿

Scene 编辑：拖 Waypoint 改经过点；拖蓝/橙手柄改弯曲方向；青色实线为运行时轨迹。

### 2. 组件播放

```csharp
using UnityEngine;
using ZStudio.UIPathTween;

public class FlyCoin : MonoBehaviour {
    [SerializeField] 
    private UIPathTween m_Path;

    public void Play() => m_Path.Play();   // 返回 PrimeTween.Tween
    public void Stop() => m_Path.Stop();
}
```

装了 UniTask 时：

```csharp
await m_Path.PlayAsync(cancellationToken);
// 取消时就地 Stop，不触发 onComplete
```

### 3. 程序化播放

无需在 Hierarchy 摆 Waypoint，直接对任意 `RectTransform` 传点即可。

#### 3a. Anchored 坐标

`points` 直接赋给 `target.anchoredPosition`，必须是 **target 父级**的 anchored 坐标：

```csharp
using PrimeTween;
using ZStudio.UIPathTween;

var points = new List<Vector2> {
    from, (from + to) * 0.5f + new Vector2(0f, 160f), to
};

var options = UIPathPlaybackOptions.Default;
options.curveMode = EUIPathCurveMode.CatmullRom;
options.tweenSettings.duration = 0.6f;
options.tweenSettings.ease = Ease.OutQuad;
options.orient = true;
options.onUpdate = t => { /* t ∈ [0,1] */ };
options.onComplete = () => Debug.Log("done");

UIPathTween.Play(icon, points, options);
```

Custom 缓动曲线：

```csharp
options.tweenSettings = new TweenSettings(0.6f, myAnimationCurve);
// 或
options.tweenSettings.ease = Ease.Custom;
options.tweenSettings.customEase = myAnimationCurve;
```

#### 3b. 世界坐标（跨父级飞行）

从背包格子飞到 HUD 等场景，源/终点往往在不同父级，用世界坐标重载：

```csharp
var worldPoints = new List<Vector3> {
    fromRect.position,
    (fromRect.position + toRect.position) * 0.5f + new Vector3(0f, 1.5f, 0f),
    toRect.position,
};

// Overlay 画布 cam = null；Camera/World Space 画布传 render camera
UIPathTween.Play(icon, worldPoints, options, cam: null);
```

单点转换：`UIPathTween.WorldToAnchored(icon, worldPos, cam)`

#### 3c. 自定义 Bezier 切线

只传 `Vector2` 时 Bezier 用自动切线。需要逐点控制弯曲时传 `UIPathNode`：

```csharp
var nodes = new List<UIPathNode> {
    new(new Vector2(-400f, 0f), Vector2.zero, new Vector2(140f, 120f)),
    new(new Vector2(0f, 0f), new Vector2(-130f, 90f), new Vector2(130f, -90f)),
    UIPathNode.Auto(new Vector2(400f, 0f)),
};

options.curveMode = EUIPathCurveMode.Bezier;
UIPathTween.Play(icon, nodes, options);
```

---

## 曲线模式

| 模式 | 适用 |
|------|------|
| **Bezier** | 精确 S 形、非对称弯度（**推荐默认**） |
| CatmullRom | 快速平滑过点，切线由邻居自动决定 |
| Linear | 折线连接 |

Catmull-Rom 切线由 `(P下一個 - P上一個)` 决定，无法单独控制「先左弯再右弯」。Bezier 每点有独立 In/Out 切线，控形自由度更高。

---

## API 参考

### UIPathTween

| 成员 | 说明 |
|------|------|
| `Play()` | 实例播放，返回 `Tween`；无效配置时警告并返回 `default` |
| `Stop()` | 停止当前动画 |
| `IsPlaying` | 是否正在播放 |
| `Duration` / `PlaybackSettings` | 当前播放时长 / 完整 `TweenSettings` 配置 |
| `BuildOptions()` | 导出 Inspector 配置为 `UIPathPlaybackOptions` |
| `Play(target, points, options)` | 程序化：anchored 坐标 |
| `Play(target, nodes, options)` | 程序化：自定义 Bezier 切线 |
| `Play(target, worldPoints, options, cam)` | 程序化：世界坐标，自动转换 |
| `WorldToAnchored(target, worldPos, cam)` | 世界坐标 → target anchored 坐标 |
| `Evaluate(t)` / `EvaluateWorld(t)` | 按弧长求位置（每次重建采样，有 GC） |
| `GetSampledPath()` / `GetPathNodes()` | 采样路径 / 含切线节点 |
| `IsValid(out reason)` | 校验 Hierarchy 配置 |

### UIPathPlaybackOptions

| 字段 | 说明 |
|------|------|
| `curveMode` / `samplesPerSegment` | 曲线模式与每段采样数 |
| `tweenSettings` | PrimeTween 播放参数：`duration`、`ease`（含 `customEase`）、`cycles`、`cycleMode`、`startDelay` / `endDelay`、`useUnscaledTime` |
| `snapToStart` | 播放前移到起点 |
| `orient` / `orientAngleOffset` | 朝向前进方向 + 角度偏移 |
| `onUpdate(float t)` | 每帧回调，t 为缓动后进度 [0,1] |
| `onComplete()` | 全部循环结束（无限循环不触发） |
| `Default` | 推荐默认值 |

### UIPathNode

| 构造 | 说明 |
|------|------|
| `new UIPathNode(pos, tangentIn, tangentOut)` | 显式切线（anchored 偏移） |
| `UIPathNode.Auto(pos)` | 自动切线 |

### UIPathTweenAsync（需 UniTask）

| 成员 | 说明 |
|------|------|
| `path.PlayAsync(ct)` | 实例异步播放 |
| `PlayAsync(target, points, options, ct)` | 程序化 anchored |
| `PlayAsync(target, nodes, options, ct)` | 程序化自定义切线 |
| `PlayAsync(target, worldPoints, options, cam, ct)` | 程序化世界坐标 |

## 示例

通过 Package Manager → **Import samples → Demo**，或菜单 **ZStudio → UIPathTween → Build Test Scene** 生成测试场景。

场景路径：`Samples/Demo/Scenes/UIPathTweenTestScene.unity`

示例含 Bezier S 形路径与 Play / Reset / Loop 控件。不需要示例时，不导入即可，不影响核心功能。

---

## 常见问题

**Scene 里看不到切线手柄？**  
确认 `Curve Mode = Bezier`，且 Waypoint 上 `Auto Tangents = false`。

**预览曲线和运行时不一致？**  
确认 Target / Waypoint 都是 Path 物体的直接子节点，共用 anchored 坐标系。

**Yoyo 和 Rewind 看起来一样？**  
Tween Settings 里 `Ease = Linear` 时两者视觉相同；换非 Linear 缓动才能看出区别（缓动作用在路径参数 t 上）。

**动画速度不均匀？**  
路径已按弧长采样。若仍觉快慢不一，检查 Tween Settings 里的 `Ease` 是否为非 Linear。

**没装 UniTask 能用吗？**  
能。核心 `Play()` 只依赖 PrimeTween；`UIPathTween.Async` 带 `defineConstraints`，无 UniTask 时自动跳过编译。

**Evaluate 每帧调用会卡？**  
`Evaluate` / `EvaluateWorld` 每次重建采样路径（有 GC）。高频场景用 `GetSampledPath()` + `UIPathSampler.BuildCumulativeLengths()` 自行缓存，或直接用 `Play`。
