# SubZ 插件说明（中文）

## 一、Emby 插件封装要求

根据 Emby 官方构建文档，服务端插件最终以 DLL 形式放到 Emby 的 `plugins` 目录运行。

- 最低要求：一个主插件 DLL。
- 若引入第三方依赖，可能会附带额外 DLL。
- 本项目当前目标是尽量保持为单主 DLL（`SubZ.Plugin.dll`）发布。

官方参考：
- https://github.com/MediaBrowser/MediaBrowser/wiki/How-to-build-a-Server-Plugin

## 二、当前方案

- 仅使用 API 翻译（不使用本地模型）
- 输出格式可选 `ass` / `srt`
- 支持“手动目标模式”
- 支持 `default_profile + profiles` 多配置切换
- 支持“批次尾部自愈重试”（只重试未翻译尾段）
- 支持翻译前后标签保护（HTML/ASS 标签与 `\\N`）
- 支持字幕轨评分选择（文本轨优先、非 forced 优先、非 HI 优先）

## 三、配置项说明

配置类文件：`src/SubZ.Plugin/Configuration/PluginOptions.cs`

1. `Enabled`
- 插件总开关

2. `ManualTargetOnlyMode`
- `true`：默认入库时不自动执行翻译
- `false`：允许自动入库流程执行

3. `TargetLanguage`
- 目标语言，如 `zh-CN`

4. `OutputFormat`
- `ass` 或 `srt`

5. `AssFontName` / `AssFontSize`
- ASS 输出字体样式基础参数

6. `ApiProvider` / `ApiBaseUrl` / `ApiKey` / `Model`
- API 接入参数（例如 LLM + `gpt-5.5`）

## 四、手动执行接口

接口文件：`src/SubZ.Plugin/Api/ManualTranslationService.cs`

- `POST /SubZ/Translate/Run`

请求体（二选一）：

```json
{
  "targetFolderPath": "D:/Media/Movies",
  "targetFilePath": null
}
```

```json
{
  "targetFolderPath": null,
  "targetFilePath": "D:/Media/Movies/A.Movie.2025.mkv"
}
```

校验规则：
- 必须且只能提供一个目标字段
- 目标路径必须存在

额外接口：
- `GET /SubZ/Translate/Queue`：查看当前排队目标（当前为内存队列骨架）

## 五、自动执行策略

策略文件：`src/SubZ.Plugin/Services/TranslationExecutionPolicy.cs`

- `Enabled=false`：不执行
- `ManualTargetOnlyMode=true`：入库自动执行关闭
- `ManualTargetOnlyMode=false` 且 `Enabled=true`：允许自动流程

## 六、当前实现状态

已实现：
- Emby 插件工程骨架（`SubZ.Plugin.csproj`）
- Simple UI 配置类
- 手动目标模式策略
- 手动触发 API 与参数校验
- 打包脚本（单主 DLL 导出）

未实现（下一步）：
- 真正的字幕提取/翻译/ASS 生成执行链路
- 入库事件与自动任务接线
- 持久化任务队列与重试机制

## 七、构建与打包

执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-plugin.ps1
```

输出：
- `artifacts/plugin/SubZ.Plugin.dll`
- `artifacts/SubZ.Plugin-Release.zip`

说明：
- 你当前环境未安装 .NET SDK，需先安装后再构建。

## 八、Logo 文件

- 当前最终选定 Logo：`assets/subz-logo.svg`
- 该 Logo 已作为嵌入资源写入插件程序集（`SubZ.Plugin.dll`）。

