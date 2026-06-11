---
name: plc-wall-dimension-input
overview: 在 PlcDataPage 墙体信息栏中增加实际尺寸（长/宽/高）输入框，输入后自动同步更新「墙定义」分组中 PLC 指令的 X0/Y0/Z0 值。
todos:
  - id: add-viewmodel-properties
    content: 在 PlcDataViewModel.cs 中新增 WallActualLength、WallActualWidth、WallActualHeight 可观察属性，并实现 OnXxxChanged 钩子调用 SyncWallDimensions 方法
    status: completed
  - id: add-sync-method
    content: 实现 SyncWallDimensions 和 ReadWallDimensionsFromInstructions 方法，在 LoadFeatureGroups 和 LoadInstructionsFromEntities 及 ClearPanel 中集成调用
    status: completed
    dependencies:
      - add-viewmodel-properties
  - id: add-xaml-inputs
    content: 在 PlcDataPage.xaml 的 Row1 WrapPanel 中新增实际长/宽/高三个带标签的 TextBox 输入框
    status: completed
    dependencies:
      - add-viewmodel-properties
  - id: add-localization-keys
    content: 在 Strings.resx 和 Strings.en.resx 中新增 Label_ActualLength、Label_ActualWidth、Label_ActualHeight 本地化键值
    status: completed
---

## 用户需求

在 PlcDataPage 墙体信息栏中增加墙体实际尺寸（长/宽/高）的输入框，用户输入后自动同步写入「墙定义」(WallHandler) 分组中所有 PLC 指令的 X0/Y0/Z0 字段。

## 核心功能

- 在墙体信息条（Row1）中新增三个可编辑输入框：实际长度、实际宽度、实际高度
- 加载墙体时，自动从 WallHandler 分组指令中读取 X0/Y0/Z0 作为初始值回显
- 用户修改任意尺寸后，自动更新 WallHandler 分组中所有指令的 X0/Y0/Z0 值
- 清空面板时一并重置尺寸输入
- 支持中英文双语界面标签

## 技术栈

- WPF (.NET 8) + CommunityToolkit.Mvvm
- 数据绑定：`[ObservableProperty]` 属性 + `partial void OnXxxChanged` 方法
- 本地化：`.resx` 资源文件 + `l:Loc` 标记扩展

## 实现方案

### 核心思路

在 ViewModel 中新增三个 `float` 类型的可观察属性（`WallActualLength`、`WallActualWidth`、`WallActualHeight`），利用 CommunityToolkit 的 `partial void OnXxxChanged` 钩子，在值变更时查找 `FeatureGroups` 中 `HandlerName == "WallHandler"` 的分组，批量更新该分组下所有 `PlcInstructionDto` 的 X0/Y0/Z0 属性。

### 数据流

```
用户输入长/宽/高
  → ObservableProperty 变更通知
  → OnXxxChanged 钩子
  → SyncWallDimensions()
  → 查找 WallHandler 分组
  → 遍历更新 PlcInstructionDto.X0/Y0/Z0
  → DataGrid 自动刷新
  → 用户保存草稿 → 持久化到 PlcInstructionEntity 表
```

### 初始值加载

```
墙体搜索完成 → LoadFeatureGroups / LoadInstructionsFromEntities
  → ReadWallDimensionsFromInstructions()
  → 从 WallHandler 分组第一条指令读取 X0(=旧实际长)/Y0(=旧实际宽)/Z0(=旧实际高)
  → 赋给 WallActualLength/WallActualWidth/WallActualHeight
```

### 关键决策

1. **属性放在 ViewModel 而非 WallInfoDto**：因为 WallInfoDto 仅作为展示 DTO 从 AppService 获取，不包含实际尺寸字段；且实际尺寸已在 WallHandler 生成的指令中存在，直接从指令回读是天然的数据源。
2. **使用 partial OnXxxChanged 而非统一命令**：三个属性各自独立变更，每个属性变化都触发同步，确保用户输入一个字就实时更新。WallHandler 分组通常只有 1 条指令，性能无影响。
3. **不在此阶段持久化到 WallEntity**：当前需求明确仅修改 PLC 指令中的 X0/Y0/Z0，后续可通过 `SaveDraftCommand` 将更新后的指令统一保存到 PlcInstructionEntity 表。

### 复发控制

- 只修改 WallHandler 分组的指令，不影响其他特征分组
- 只在 `IsWallLoaded == true` 时同步（未加载墙体时不会有 WallHandler 分组）
- 清空面板时重置三个属性为 0，避免脏数据残留