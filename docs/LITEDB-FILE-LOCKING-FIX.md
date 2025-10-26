# LiteDB 文件锁定问题修复

## 问题描述

在运行应用时遇到以下错误：

```
System.IO.IOException: "The process cannot access the file 'C:\Users\gil\Music\github\agent-framework-tutorial-code\src\AgentGroupChat.AgentHost\bin\Debug\net9.0\Data\sessions.db' because it is being used by another process."
```

## 根本原因

1. **多个 LiteDatabase 实例**：
   - `PersistedSessionService` 在构造函数中创建了自己的 `LiteDatabase` 实例
   - `Program.cs` 中也注册了一个 `LiteDatabase` 单例
   - 旧的 `SessionService` 也在创建实例（虽然未使用）

2. **文件独占锁**：
   - LiteDB 默认使用独占文件锁（Exclusive mode）
   - 多个 `LiteDatabase` 实例无法同时访问同一个文件

## 解决方案

### 方案 1：依赖注入单例模式（已实施）✅

**优点**：
- ✅ 符合 ASP.NET Core 依赖注入最佳实践
- ✅ 确保整个应用只有一个 `LiteDatabase` 实例
- ✅ 生命周期由 DI 容器管理，避免资源泄漏
- ✅ 易于测试（可以注入 mock）

**修改内容**：

#### 1. 修改 `PersistedSessionService` 构造函数

```csharp
// 之前：自己创建 LiteDatabase 实例
public PersistedSessionService(ILogger<PersistedSessionService>? logger = null)
{
    var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
    Directory.CreateDirectory(dbPath);
    var dbFilePath = Path.Combine(dbPath, "sessions.db");
    _database = new LiteDatabase(dbFilePath);
    // ...
}

// 之后：通过依赖注入接收 LiteDatabase
public PersistedSessionService(
    LiteDatabase database, 
    ILogger<PersistedSessionService>? logger = null)
{
    _database = database ?? throw new ArgumentNullException(nameof(database));
    _ownsDatabase = false; // 不拥有数据库实例
    // ...
}
```

#### 2. 修改 Dispose 方法

```csharp
public void Dispose()
{
    _logger?.LogInformation("Disposing PersistedSessionService");
    
    // 只有当我们拥有数据库实例时才 Dispose
    // 使用 DI 的单例实例由容器管理，不应该在这里 Dispose
    if (_ownsDatabase)
    {
        _database?.Dispose();
    }
}
```

#### 3. 使用共享模式连接字符串（额外优化）

```csharp
// Program.cs
builder.Services.AddSingleton<LiteDatabase>(sp =>
{
    var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
    Directory.CreateDirectory(dbPath);
    var dbFilePath = Path.Combine(dbPath, "sessions.db");
    
    // 使用连接字符串配置 LiteDB
    // Mode=Shared: 允许多个进程/线程读取，但写入时会锁定
    // Connection=shared: 共享连接模式，提高并发性能
    var connectionString = $"Filename={dbFilePath};Mode=Shared;Connection=shared";
    
    return new LiteDatabase(connectionString);
});
```

### 方案 2：LiteDB 连接字符串优化（可选）

如果仍然需要在某些场景下创建多个实例，可以使用 LiteDB 的连接字符串参数：

```csharp
// Shared Mode - 允许多个读取者，单个写入者
var connectionString = "Filename=sessions.db;Mode=Shared;Connection=shared";
var db = new LiteDatabase(connectionString);

// 或者使用 ConnectionString 对象
var connectionString = new ConnectionString
{
    Filename = "sessions.db",
    Connection = ConnectionType.Shared,
    ReadOnly = false
};
```

**LiteDB 连接模式说明**：

| 模式 | 说明 | 使用场景 |
|------|------|----------|
| `Exclusive` | 独占模式（默认），一个进程独占文件 | 单进程应用 |
| `Shared` | 共享模式，允许多个读取者，写入时锁定 | 多线程应用 |
| `ReadOnly` | 只读模式，允许多个读取者 | 分析工具、报表 |

### 方案 3：使用 LiteDB 5.x 的内存映射（高级）

对于高并发场景，可以考虑：

```csharp
var connectionString = new ConnectionString
{
    Filename = "sessions.db",
    Connection = ConnectionType.Shared,
    InitialSize = 10 * 1024 * 1024, // 10MB 初始大小
    CacheSize = 5000 // 缓存页数
};
```

## 测试验证

修复后，应该能够：

1. ✅ 正常启动应用，不会出现文件锁定错误
2. ✅ 多个请求并发访问数据库
3. ✅ 应用重启后能正确恢复数据

## 最佳实践总结

1. **单例模式**：在 ASP.NET Core 中，`LiteDatabase` 应该注册为单例
2. **依赖注入**：所有需要数据库的服务通过构造函数注入 `LiteDatabase`
3. **资源管理**：
   - `LiteDatabase` 的 Dispose 由 DI 容器管理
   - 服务不应该 Dispose 注入的 `LiteDatabase`
4. **线程安全**：`LiteDatabase` 本身是线程安全的，可以在多线程环境中使用
5. **连接字符串**：
   - 默认情况下使用 `Shared` 模式
   - 避免创建多个 `LiteDatabase` 实例指向同一个文件

## 相关资源

- [LiteDB Documentation](https://www.litedb.org/)
- [LiteDB Connection String](https://www.litedb.org/docs/connection-string/)
- [ASP.NET Core Dependency Injection](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection)

## 清理工作（可选）

如果不再使用旧的 `SessionService`，可以删除该文件：

```
src/AgentGroupChat.AgentHost/Services/SessionService.cs
```

## 故障排除

如果仍然遇到锁定问题：

1. **检查是否有其他进程**：
   ```powershell
   # Windows
   Get-Process | Where-Object {$_.Path -like "*AgentGroupChat*"}
   ```

2. **删除锁文件**（谨慎操作）：
   ```powershell
   # 停止所有相关进程后
   Remove-Item "Data\sessions.db-*" -Force
   ```

3. **检查文件权限**：
   确保应用有读写 `Data` 目录的权限

4. **使用进程监视器**：
   使用 Process Monitor (Sysinternals) 查看哪个进程在访问文件
