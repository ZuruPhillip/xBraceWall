---
name: plc-send-command-implementation
overview: 实现 PLC 指令单元页面的"下发指令"按钮功能：将所有 PLC 指令按 OPC UA NodeId 格式（ns=2;s=unit/MCCUnit_35.InDATA_CNC_P.LineDef[i].头字母）批量写入到 PLC 设备。
todos:
  - id: impl-send-command
    content: 修改 PlcDataViewModel.cs：注入 IOpcUaService，实现 SendAsync 批量写入逻辑（扁平化指令、生成 NodeId 字典、调用 WriteNodesAsync）
    status: completed
---

## 用户需求

在 PLC 指令单元页面，点击"下发指令"按钮时，将页面中所有 PLC 指令数据按指定 OPC UA NodeId 格式组装，一次性批量写入到 PLC 设备中。

## 核心功能

- **数据扁平化**：从 `FeatureGroups` 中取出所有分组的全部指令，按排序顺序扁平化为一个连续列表，索引 i 从 0 开始编号
- **NodeId 生成**：每条指令生成 9 个写入项（T/F/D/X0/Y0/Z0/X1/Y1/Z1），Key 格式为 `ns=2;s=unit/MCCUnit_35.InDATA_CNC_P.LineDef[i].{header}`
- **批量写入**：调用 `IOpcUaService.WriteNodesAsync` 一次性将所有键值对写入 OPC UA 服务器
- **连接检查**：写入前检查 OPC 是否已连接，未连接时给出提示
- **错误处理**：写入失败时弹出错误提示，并记录日志

## 技术方案

### 实现策略

仅需修改一个文件：`ViewModels/PlcDataViewModel.cs`。在构造函数中额外注入 `IOpcUaService`（已注册为 DI 单例），然后在 `SendAsync` 方法中：

1. 检查 `_opcUaService.IsConnected`，未连接则弹窗提示并返回
2. 遍历所有 `FeatureGroups`，将各分组的 `Instructions` 扁平化，每条指令分配从 0 开始的索引
3. 为每条指令生成 9 个键值对并放入 `Dictionary<string, object>`
4. 调用 `_opcUaService.WriteNodesAsync(dict)` 批量写入
5. 根据结果弹窗反馈，异常时记录日志

### 修改点

| 位置 | 修改内容 |
| --- | --- |
| 文件头部 using | 新增 `using CncWallStation.Services.OpcUa;` |
| 字段 | 新增 `private readonly IOpcUaService _opcUaService;` |
| 构造函数 | 新增参数 `IOpcUaService opcUaService` 并赋值 |
| SendAsync 方法 | 替换 TODO 空实现为完整写入逻辑 |


### 关键设计

- **扁平化逻辑**：`FeatureGroups.SelectMany(g => g.Instructions).ToList()` 即可按分组顺序获取所有指令
- **NodeId 格式**：使用字符串插值 `$"ns=2;s=unit/MCCUnit_35.InDATA_CNC_P.LineDef[{i}].{header}"`
- **字典类型**：`IReadOnlyDictionary<string, object>` 可直接用 `Dictionary<string, object>` 传入
- **反馈机制**：成功弹出"下发成功，共 X 条指令"提示；失败弹出错误详情

### 代码逻辑

```
SendAsync():
    if !IsConnected → MessageBox("OPC 未连接") → return
    扁平化所有分组指令 → flatList (每条分配 i)
    foreach i, inst in flatList:
        dict[$"ns=2;s=...LineDef[{i}].T"]  = inst.T
        dict[$"ns=2;s=...LineDef[{i}].F"]  = inst.F
        ...
    try:
        await _opcUaService.WriteNodesAsync(dict)
        MessageBox("下发成功，共 X 条指令")
    catch Exception ex:
        _logger.LogError(ex, ...)
        MessageBox("下发失败: {ex.Message}")
```