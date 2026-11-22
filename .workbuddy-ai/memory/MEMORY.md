# 项目长期约定

- 当前项目路径：`D:/Fork/ShouNaXiaoZhan`。
- 业务场景为 `Assets/Scenes/StorageStation.unity`；不要修改 MediaPipe Samples 示例场景。
- 用户要求场景中的物品和视觉对象必须真实保存为 GameObject，可在 Scene 中拖拽、在 Inspector 中编辑；不要只依赖运行时 `new GameObject`  procedural 生成。
- 修改布局应直接编辑场景对象。`Storage Station > Bake Editable Scene Objects` 会确认后重建并覆盖 World/Targets/Items/Systems/HUD 的人工调整，仅用于重置布局。
- 几何 Sprite 持久资源位于 `Assets/Resources/StorageStation/Shapes`。
