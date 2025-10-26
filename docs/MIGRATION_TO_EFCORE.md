# Migration from LiteDB to EF Core

## Overview
This document describes the migration from LiteDB to Entity Framework Core (EF Core) for the AgentGroupChat project.

## What Changed

### Dependencies
**Removed:**
- LiteDB (5.0.21)

**Added:**
- Microsoft.EntityFrameworkCore (9.0.0)
- Microsoft.EntityFrameworkCore.Sqlite (9.0.0)
- Npgsql.EntityFrameworkCore.PostgreSQL (9.0.2)
- Microsoft.EntityFrameworkCore.Design (9.0.0)

### Architecture Changes

#### 1. Interface-Based Design
Following best practices, the implementation now uses interfaces for dependency injection:

- `ISessionService`: Contract for session persistence operations
- `IMessageCollection`: Contract for message collection access

#### 2. New Components
- `AgentDbContext`: EF Core DbContext for database operations
- `EfCoreSessionService`: EF Core implementation of `ISessionService`
- `EfCoreMessageCollection`: EF Core implementation of `IMessageCollection`
- `EfCoreChatMessageStore`: EF Core implementation of `ChatMessageStore`

#### 3. Service Registration
Services are now registered using dependency injection:
```csharp
builder.Services.AddScoped<ISessionService, EfCoreSessionService>();
builder.Services.AddScoped<AgentChatService>();
```

## Database Providers

### SQLite (Default)
SQLite is the default database provider and requires no additional setup.

**Configuration:**
```json
{
  "DatabaseProvider": "Sqlite"
}
```

The database file will be created at: `{AppDirectory}/Data/agentchat.db`

### PostgreSQL
To use PostgreSQL, update your `appsettings.json`:

**Configuration:**
```json
{
  "DatabaseProvider": "PostgreSQL",
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Database=agentchat;Username=postgres;Password=yourpassword"
  }
}
```

**Or use environment variable:**
```bash
export POSTGRESQL_CONNECTION_STRING="Host=localhost;Database=agentchat;Username=postgres;Password=yourpassword"
```

## Migration Guide

### For Existing Projects

1. **Backup your data**: The old LiteDB database files are in the `Data` directory
2. **Update configuration**: Add database provider settings to `appsettings.json`
3. **Run the application**: The database will be created automatically on first run
4. **Migrate data** (if needed): You can manually migrate data from LiteDB to EF Core if you have existing data

### Database Schema
The EF Core implementation uses the same data models:
- `PersistedChatSession`: Stores chat session metadata
- `PersistedChatMessage`: Stores chat messages

Both tables are created automatically with proper indexes for optimal performance.

## Features

### Performance Optimizations
1. **Indexed Queries**: All frequently queried fields are indexed
2. **In-Memory Caching**: Hot sessions are cached to reduce database calls
3. **Batch Operations**: Messages are upserted efficiently

### Provider-Agnostic Code
The implementation works seamlessly with both SQLite and PostgreSQL without code changes.

### Backward Compatibility
Old LiteDB implementation files are preserved as `.old` backups:
- `SessionService.cs.old`
- `PersistedSessionService.cs.old`
- `LiteDbChatMessageStore.cs.old`

## API Impact

No API changes were made. All existing endpoints work exactly as before:
- `GET /api/sessions` - Get all sessions
- `POST /api/sessions` - Create new session
- `GET /api/sessions/{id}` - Get specific session
- `POST /api/chat` - Send chat message
- `DELETE /api/sessions/{id}` - Delete session
- `POST /api/sessions/{id}/clear` - Clear conversation
- `GET /api/sessions/{id}/messages` - Get conversation history

## Troubleshooting

### Database Not Created
Ensure the `Data` directory has write permissions.

### PostgreSQL Connection Issues
- Verify PostgreSQL is running
- Check connection string format
- Ensure database exists (EF Core will create tables but not the database itself)

### Service Lifetime Errors
The `AgentChatService` must be registered as `Scoped` because it depends on `ISessionService` which is also scoped (due to DbContext lifetime).

## Development Notes

### Adding Migrations (for future schema changes)
```bash
dotnet ef migrations add MigrationName --project src/AgentGroupChat.AgentHost
dotnet ef database update --project src/AgentGroupChat.AgentHost
```

### Testing Different Providers
Simply change the `DatabaseProvider` setting and restart the application. Each provider maintains its own database.

## Security Considerations

1. **Connection Strings**: Never commit connection strings with real credentials to version control
2. **Use Environment Variables**: For production, use environment variables for sensitive data
3. **SQL Injection**: EF Core automatically protects against SQL injection
4. **Dependency Security**: All EF Core packages are from Microsoft and actively maintained

## References

- [EF Core Documentation](https://docs.microsoft.com/en-us/ef/core/)
- [Npgsql EF Core Provider](https://www.npgsql.org/efcore/)
- [SQLite EF Core Provider](https://docs.microsoft.com/en-us/ef/core/providers/sqlite/)
