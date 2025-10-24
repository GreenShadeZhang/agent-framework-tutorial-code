# MudBlazor UI 设计说明

## 颜色方案 - 紫罗兰主题

### 主色调
```
Primary:   #8B5CF6  (Violet-500) - 主要按钮、AppBar、强调元素
Secondary: #A78BFA  (Violet-400) - 次要元素、辅助按钮
Tertiary:  #7C3AED  (Violet-600) - 悬停状态、深色强调
```

### 功能色
```
Info:    #6366F1  (Indigo-500)  - 信息提示
Success: #10B981  (Emerald-500) - 成功状态、用户头像
Warning: #F59E0B  (Amber-500)   - 警告消息
Error:   #EF4444  (Red-500)     - 错误提示
```

### 中性色
```
Text Primary:   #1F2937  (Gray-800)
Text Secondary: #6B7280  (Gray-500)
Background:     #F9FAFB  (Gray-50)
Surface:        #FFFFFF
Divider:        #E5E7EB  (Gray-200)
```

## 组件样式指南

### 1. 顶部导航栏 (MudAppBar)
```razor
<MudAppBar Elevation="2" Color="Color.Primary">
    <MudIconButton Icon="@Icons.Material.Filled.Chat" />
    <MudText Typo="Typo.h6">Agent Group Chat Platform</MudText>
    <MudSpacer />
    <MudIconButton Icon="@Icons.Material.Filled.Help" />
</MudAppBar>
```

**特点**:
- 紫罗兰色背景 (#8B5CF6)
- 白色文字和图标
- Elevation=2 提供轻微阴影
- 固定在顶部

### 2. 侧边栏 (会话列表)
```razor
<MudPaper Elevation="0" Style="width: 320px; border-right: 1px solid var(--mud-palette-divider);">
    <MudButton Variant="Variant.Filled" Color="Color.Primary" FullWidth="true">
        New Chat
    </MudButton>
    <MudList T="string">
        <MudListItem T="string">
            <!-- 会话项 -->
        </MudListItem>
    </MudList>
</MudPaper>
```

**特点**:
- 320px 固定宽度
- 白色背景，右侧灰色分隔线
- 列表项悬停效果：浅紫罗兰背景 (rgba(139, 92, 246, 0.08))
- 激活项：紫罗兰渐变背景 + 左侧3px紫色边框

### 3. 消息气泡

#### 代理消息
```razor
<div class="d-flex justify-start">
    <MudAvatar Color="Color.Primary" Size="Size.Medium">
        🤖
    </MudAvatar>
    <MudPaper Elevation="2" Class="pa-3">
        <MudText Typo="Typo.body2">消息内容</MudText>
        <MudText Typo="Typo.caption">时间戳</MudText>
    </MudPaper>
</div>
```

**样式**:
- 左对齐
- 紫罗兰头像
- 白色背景气泡
- Elevation=2 阴影
- padding: 12px

#### 用户消息
```razor
<div class="d-flex justify-end">
    <MudPaper Elevation="2" 
              Style="background: linear-gradient(135deg, #8B5CF6 0%, #A78BFA 100%); color: white;">
        <MudText Typo="Typo.body2" Style="color: white;">
            消息内容
        </MudText>
    </MudPaper>
    <MudAvatar Color="Color.Success">
        <MudIcon Icon="@Icons.Material.Filled.Person" />
    </MudAvatar>
</div>
```

**样式**:
- 右对齐
- 紫罗兰渐变背景
- 白色文字
- 绿色用户头像
- 镜像布局

### 4. 输入区域
```razor
<MudPaper Elevation="3" Class="pa-4">
    <MudAlert Severity="Severity.Info" Variant="Variant.Outlined" Dense="true">
        💡 使用提示
    </MudAlert>
    
    <div class="d-flex gap-3">
        <MudTextField @bind-Value="_inputMessage" 
                      Variant="Variant.Outlined" 
                      Lines="3" />
        <MudButton Variant="Variant.Filled" 
                   Color="Color.Primary" 
                   EndIcon="@Icons.Material.Filled.Send">
            Send
        </MudButton>
    </div>
</MudPaper>
```

**特点**:
- 顶部阴影 (Elevation=3)
- 信息提示条 (蓝色轮廓)
- 多行文本框 (Outlined 样式)
- 紫罗兰发送按钮

### 5. 代理标签 (MudChip)
```razor
<MudChip Color="Color.Primary" 
         Size="Size.Small" 
         Variant="Variant.Outlined"
         T="string">
    🌞 Sunny
</MudChip>
```

**样式**:
- 紫罗兰边框
- 透明背景
- 小号尺寸
- 圆角矩形

## 动画效果

### 消息淡入
```css
@keyframes fadeInUp {
    from {
        opacity: 0;
        transform: translateY(20px);
    }
    to {
        opacity: 1;
        transform: translateY(0);
    }
}

.mb-4 {
    animation: fadeInUp 0.3s ease-out;
}
```

### 加载指示器
```razor
<MudProgressCircular Size="Size.Small" 
                     Indeterminate="true" 
                     Color="Color.Primary" />
```

## 响应式布局

### 桌面 (> 1024px)
- 侧边栏: 320px
- 主区域: 自适应
- 双栏布局

### 平板 (768px - 1024px)
- 侧边栏: 280px
- 字体略小
- 保持双栏

### 移动端 (< 768px)
- 侧边栏: 240px 或折叠
- 单栏布局
- 消息气泡宽度 80%

### 小屏 (< 640px)
- 侧边栏: 顶部 200px 高度
- 竖向堆叠
- 输入框全宽
- padding 减少至 12px

## 间距系统 (MudBlazor Classes)

```
pa-0  : padding: 0
pa-2  : padding: 8px
pa-3  : padding: 12px
pa-4  : padding: 16px

ma-2  : margin: 8px
ma-3  : margin: 12px
mb-2  : margin-bottom: 8px
mb-3  : margin-bottom: 12px
mb-4  : margin-bottom: 16px

ml-2  : margin-left: 8px
ml-3  : margin-left: 12px
mr-2  : margin-right: 8px
mr-3  : margin-right: 12px

gap-2 : gap: 8px
gap-3 : gap: 12px
```

## 排版系统

```
Typo.h5      : 大标题 (1.5rem)
Typo.h6      : 小标题 (1.25rem)
Typo.body1   : 正文 (0.875rem)
Typo.body2   : 次要文本 (0.8125rem)
Typo.caption : 说明文字 (0.75rem)
```

## 阴影层级

```
Elevation="0" : 无阴影 (侧边栏)
Elevation="1" : 轻微阴影 (头部)
Elevation="2" : 中等阴影 (消息气泡)
Elevation="3" : 明显阴影 (输入区域)
```

## 图标使用

### Material Icons
```razor
@Icons.Material.Filled.Chat           // 聊天
@Icons.Material.Filled.Forum          // 对话
@Icons.Material.Filled.SmartToy       // 机器人/代理
@Icons.Material.Filled.Person         // 用户
@Icons.Material.Filled.Send           // 发送
@Icons.Material.Filled.Add            // 添加
@Icons.Material.Filled.Help           // 帮助
@Icons.Material.Filled.Close          // 关闭
@Icons.Material.Filled.HourglassBottom // 加载
```

## 可访问性

1. **颜色对比度**: 所有文字与背景对比度 ≥ 4.5:1
2. **键盘导航**: 所有交互元素可通过 Tab 访问
3. **屏幕阅读器**: MudBlazor 组件内置 ARIA 标签
4. **触摸目标**: 按钮最小尺寸 44x44px

## 设计原则

1. **简洁专业**: 避免过多装饰，保持商务风格
2. **一致性**: 统一使用 MudBlazor 组件
3. **响应式**: 适配各种屏幕尺寸
4. **可维护**: 主题集中管理，易于修改
5. **性能**: 使用 MudBlazor 优化的组件

---

**设计师提示**: 所有颜色、间距、字体均在 `Theme/CustomTheme.cs` 中定义，修改该文件即可全局更新主题。
