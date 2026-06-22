### v1.1.0 (2026-06-22)
- 播放参数统一为 PrimeTween `TweenSettings`（组件 `m_TweenSettings`、`UIPathPlaybackOptions.tweenSettings`）；`PlayInternal` 经 `TweenSettings<float>` 调用 `Tween.Custom`，完整传递缓动、循环与 delay。
- Inspector 复用 PrimeTween `TweenSettings` PropertyDrawer，含 Custom 缓动曲线编辑；修复此前选 `Ease.Custom` 无曲线且运行时回退默认缓动的问题。
- 新增 `UIPathTween.PlaybackSettings`，便于读取当前播放配置。
- Demo 构建脚本与测试场景已同步为新序列化格式。

### v1.0.0 (2026-06-22)
- 首次发布。
