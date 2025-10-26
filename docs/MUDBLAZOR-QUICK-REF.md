# MudBlazor 快速参考指南

## 项目迁移完成 ✅

AgentGroupChat.Web 已成功从 Bootstrap 迁移到 MudBlazor，使用紫罗兰色主题。

## 快速启动

### 运行项目
```powershell
cd c:\github\agent-framework-tutorial-code\src
dotnet run --project AgentGroupChat.AppHost
```

### 访问地址
- Web UI: https://localhost:7000 (或查看 AppHost 启动日志)
- API: 由 Aspire 自动发现

## 核心文件

### 主题配置
📁 `src/AgentGroupChat.Web/Theme/CustomTheme.cs`
- 修改颜色、字体、间距的唯一位置
- 支持亮色/暗色模式

### 主要组件
- `Components/App.razor` - 应用根组件
- `Components/Routes.razor` - MudBlazor 提供者配置
- `Components/Layout/MainLayout.razor` - 主布局（顶部栏）
- `Components/Pages/Home.razor` - 聊天界面

### 样式文件
📁 `wwwroot/app.css` - 最小化自定义样式，主要由 MudBlazor 主题控制

## 常用 MudBlazor 组件

### 布局组件
```razor
<MudLayout>
    <MudAppBar></MudAppBar>
    <MudMainContent></MudMainContent>
</MudLayout>
```

### 容器组件
```razor
<MudContainer MaxWidth="MaxWidth.Large">
    <MudPaper Elevation="2" Class="pa-4">
        内容
    </MudPaper>
</MudContainer>
```

### 表单组件
```razor
<MudTextField @bind-Value="value" Label="标签" />
<MudButton Color="Color.Primary">按钮</MudButton>
<MudCheckBox @bind-Checked="checked">选项</MudCheckBox>
```

### 列表组件
```razor
<MudList T="string">
    <MudListItem T="string">项目</MudListItem>
</MudList>
```

### 反馈组件
```razor
<MudAlert Severity="Severity.Info">提示</MudAlert>
<MudProgressCircular Indeterminate="true" />
<MudSnackbar @ref="snackbar" />
```

## 颜色快速参考

### 主题色
```csharp
Color.Primary   // #8B5CF6 紫罗兰
Color.Secondary // #A78BFA 浅紫罗兰
Color.Tertiary  // #7C3AED 深紫罗兰
```

### 功能色
```csharp
Color.Success   // 绿色
Color.Info      // 蓝色
Color.Warning   // 橙色
Color.Error     // 红色
```

### 中性色
```csharp
Color.Default   // 默认
Color.Dark      // 深色
Color.Transparent // 透明
```

## 常用类名

### 间距
```
pa-{0-16}  : padding all sides
ma-{0-16}  : margin all sides
px-{0-16}  : padding horizontal
py-{0-16}  : padding vertical
mx-{0-16}  : margin horizontal
my-{0-16}  : margin vertical
```

### Flexbox
```
d-flex           : display: flex
flex-column      : flex-direction: column
flex-row         : flex-direction: row
justify-center   : justify-content: center
justify-start    : justify-content: flex-start
justify-end      : justify-content: flex-end
align-center     : align-items: center
gap-{2-4}        : gap
```

### 文本
```
text-center      : text-align: center
text-left        : text-align: left
text-right       : text-align: right
font-weight-bold : font-weight: bold
```

## 图标使用

### Material Icons
```razor
<MudIcon Icon="@Icons.Material.Filled.Chat" />
<MudIcon Icon="@Icons.Material.Outlined.Person" />
<MudIcon Icon="@Icons.Material.Rounded.Star" />
```

### 常用图标
```csharp
Icons.Material.Filled.Chat
Icons.Material.Filled.Send
Icons.Material.Filled.Add
Icons.Material.Filled.Delete
Icons.Material.Filled.Edit
Icons.Material.Filled.Settings
Icons.Material.Filled.Person
Icons.Material.Filled.Close
```

## 修改主题颜色

### 1. 编辑 CustomTheme.cs
```csharp
Primary = "#YOUR_COLOR_HEX",    // 主色
Secondary = "#YOUR_COLOR_HEX",  // 次要色
```

### 2. 重新编译
```powershell
dotnet build
```

### 3. 刷新浏览器
颜色立即应用到整个应用

## 添加新页面

### 1. 创建 Razor 组件
```razor
@page "/new-page"

<PageTitle>New Page</PageTitle>

<MudContainer MaxWidth="MaxWidth.Large" Class="pa-4">
    <MudPaper Elevation="2" Class="pa-4">
        <MudText Typo="Typo.h5">页面标题</MudText>
    </MudPaper>
</MudContainer>
```

### 2. 添加导航（可选）
在 `MainLayout.razor` 的 `MudAppBar` 中添加链接

## 响应式设计

### 断点
```
xs : < 600px
sm : 600px - 960px
md : 960px - 1280px
lg : 1280px - 1920px
xl : > 1920px
```

### 条件渲染
```razor
<MudHidden Breakpoint="Breakpoint.SmAndDown">
    <!-- 只在中大屏显示 -->
</MudHidden>

<MudHidden Breakpoint="Breakpoint.MdAndUp">
    <!-- 只在小屏显示 -->
</MudHidden>
```

## 调试技巧

### 查看编译错误
```powershell
dotnet build --verbosity detailed
```

### 热重载
修改 `.razor` 或 `.cs` 文件后，应用自动重新编译（需在 VS Code 中运行）

### 查看 MudBlazor 文档
https://mudblazor.com/components/list

## 性能优化

### 虚拟化长列表
```razor
<MudVirtualize Items="@items" Context="item">
    <MudListItem>@item.Name</MudListItem>
</MudVirtualize>
```

### 延迟加载
```razor
<MudImage Src="@imageUrl" Loading="@Loading.Lazy" />
```

## 常见问题

### Q: 组件未找到？
A: 确认 `_Imports.razor` 包含 `@using MudBlazor`

### Q: 颜色不生效？
A: 检查 `Routes.razor` 是否配置了 `<MudThemeProvider Theme="@_theme" />`

### Q: 样式冲突？
A: MudBlazor 使用 scoped CSS，避免全局样式覆盖

## 资源链接

- [MudBlazor 官方文档](https://mudblazor.com/)
- [MudBlazor GitHub](https://github.com/MudBlazor/MudBlazor)
- [组件库示例](https://mudblazor.com/components/list)
- [主题生成器](https://mudblazor.com/customization/theme)

## 团队协作

### Git 分支
- `main` - 生产分支
- `copilot/implement-handoff-mode-chat` - 当前开发分支

### 提交规范
```
feat: 新功能
fix: 修复
style: UI 样式调整
docs: 文档更新
refactor: 代码重构
```

---

**最后更新**: 2025-10-24  
**版本**: MudBlazor 8.0.0  
**状态**: 生产就绪 ✅
