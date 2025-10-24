# Troubleshooting Guide

This guide helps you resolve common issues when running the Agent Group Chat application.

## Table of Contents

1. [Configuration Issues](#configuration-issues)
2. [Authentication Issues](#authentication-issues)
3. [Runtime Errors](#runtime-errors)
4. [Performance Issues](#performance-issues)
5. [Database Issues](#database-issues)
6. [Build Issues](#build-issues)

---

## Configuration Issues

### Error: "Azure OpenAI endpoint not configured"

**Symptoms**:
```
System.InvalidOperationException: Azure OpenAI endpoint not configured
```

**Solutions**:

1. **Check appsettings.json**:
   ```json
   {
     "AzureOpenAI": {
       "Endpoint": "https://your-resource-name.openai.azure.com/",
       "DeploymentName": "gpt-4o-mini"
     }
   }
   ```
   Make sure the endpoint URL is correct and includes `https://`.

2. **Set Environment Variables**:
   ```bash
   export AZURE_OPENAI_ENDPOINT="https://your-resource-name.openai.azure.com/"
   export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o-mini"
   ```

3. **Verify Format**:
   - Endpoint should end with `.openai.azure.com/`
   - Deployment name should match your Azure OpenAI model deployment

### Wrong Deployment Name

**Symptoms**:
- Error 404 or "Deployment not found"

**Solutions**:

1. **Check Azure Portal**:
   - Go to Azure OpenAI resource
   - Navigate to "Deployments"
   - Use the exact deployment name shown

2. **Common Names**:
   - `gpt-4o-mini`
   - `gpt-4o`
   - `gpt-4`
   - `gpt-35-turbo`

---

## Authentication Issues

### Error: "DefaultAzureCredential authentication failed"

**Symptoms**:
```
Azure.Identity.AuthenticationFailedException: DefaultAzureCredential failed to retrieve a token
```

**Solutions**:

1. **Azure CLI Login** (Recommended for development):
   ```bash
   az login
   ```
   Follow browser prompts to authenticate.

2. **Verify Login**:
   ```bash
   az account show
   ```
   Should display your account info.

3. **Check Subscription**:
   ```bash
   az account list
   az account set --subscription "Your Subscription Name"
   ```

4. **Service Principal** (For production):
   ```bash
   export AZURE_CLIENT_ID="your-client-id"
   export AZURE_CLIENT_SECRET="your-client-secret"
   export AZURE_TENANT_ID="your-tenant-id"
   ```

5. **Managed Identity** (For Azure deployments):
   - Ensure the deployed app has system or user-assigned managed identity
   - Grant "Cognitive Services OpenAI User" role to the identity

### Permission Denied

**Symptoms**:
- "Insufficient permissions" or "Access denied"

**Solutions**:

1. **Check Azure RBAC**:
   - In Azure Portal, go to your OpenAI resource
   - Navigate to "Access control (IAM)"
   - Add role assignment: "Cognitive Services OpenAI User"

2. **Verify User/Principal**:
   - Ensure your account or service principal is listed
   - Wait 5-10 minutes for permissions to propagate

---

## Runtime Errors

### Agents Not Responding

**Symptoms**:
- Messages sent but no agent replies
- Loading indicator stays indefinitely

**Solutions**:

1. **Check Logs**:
   ```bash
   dotnet run --verbosity detailed
   ```
   Look for error messages in the output.

2. **Verify API Quota**:
   - Check Azure OpenAI quota in portal
   - Ensure you haven't exceeded rate limits

3. **Test Connectivity**:
   ```bash
   curl -H "Authorization: Bearer $(az account get-access-token --resource https://cognitiveservices.azure.com --query accessToken -o tsv)" \
        https://your-resource-name.openai.azure.com/openai/deployments?api-version=2023-05-15
   ```

4. **Restart Application**:
   - Stop the app (Ctrl+C)
   - Clear bin/obj folders
   - Rebuild: `dotnet build`
   - Run: `dotnet run`

### Slow Response Times

**Symptoms**:
- Long delays before agent responds
- Partial responses

**Solutions**:

1. **Check Model Performance**:
   - GPT-4 models are slower than GPT-3.5
   - Consider using `gpt-35-turbo` for faster responses

2. **Network Issues**:
   - Test internet connection
   - Check for firewall blocking Azure endpoints

3. **Concurrent Requests**:
   - Multiple users may exceed rate limits
   - Implement request queuing

### Images Not Generating

**Symptoms**:
- No images appear in messages
- "Image generation failed" error

**Solutions**:

1. **Current Implementation**:
   - Uses placeholder image service (picsum.photos)
   - May fail if service is down

2. **Integrate Real Image Generation**:
   - Replace `ImageGenerationTool` with DALL-E integration
   - See [ARCHITECTURE.md](ARCHITECTURE.md) for details

---

## Performance Issues

### High Memory Usage

**Symptoms**:
- Application consuming excessive RAM
- System slowdown

**Solutions**:

1. **Limit Session History**:
   - Clear old sessions regularly
   - Implement session archiving

2. **Configure Limits**:
   ```csharp
   // In SessionService
   private const int MAX_MESSAGES_PER_SESSION = 1000;
   ```

3. **Garbage Collection**:
   ```bash
   dotnet run --gc-server
   ```

### Slow UI Updates

**Symptoms**:
- Laggy interface
- Delayed message appearance

**Solutions**:

1. **SignalR Configuration**:
   ```csharp
   builder.Services.AddSignalR(options =>
   {
       options.MaximumReceiveMessageSize = 102400;
   });
   ```

2. **Reduce Message Size**:
   - Compress images
   - Limit message length

3. **Browser Cache**:
   - Clear browser cache
   - Disable browser extensions

---

## Database Issues

### Database Locked Error

**Symptoms**:
```
System.IO.IOException: The process cannot access the file 'sessions.db' because it is being used by another process
```

**Solutions**:

1. **Close Other Instances**:
   - Ensure only one app instance is running
   - Kill any orphaned processes:
     ```bash
     pkill -f AgentGroupChat
     ```

2. **Reset Database**:
   ```bash
   rm -f Data/sessions.db
   dotnet run
   ```

3. **File Permissions**:
   ```bash
   chmod 666 Data/sessions.db
   ```

### Corrupted Database

**Symptoms**:
- "Invalid database format"
- Sessions not loading

**Solutions**:

1. **Backup and Reset**:
   ```bash
   mv Data/sessions.db Data/sessions.db.bak
   dotnet run
   ```

2. **Recover Data** (if needed):
   ```bash
   # Use LiteDB.Studio to export data
   # Download from: https://github.com/mbdavid/LiteDB.Studio
   ```

### Missing Data Folder

**Symptoms**:
- "Directory not found" error

**Solutions**:

The application creates this automatically, but if it fails:
```bash
mkdir -p Data
chmod 755 Data
```

---

## Build Issues

### Package Restore Failed

**Symptoms**:
```
error NU1101: Unable to find package Microsoft.Agents.AI
```

**Solutions**:

1. **Clear NuGet Cache**:
   ```bash
   dotnet nuget locals all --clear
   ```

2. **Restore Packages**:
   ```bash
   dotnet restore
   ```

3. **Check NuGet Sources**:
   ```bash
   dotnet nuget list source
   ```
   Ensure nuget.org is enabled.

### Compilation Errors

**Symptoms**:
- CS errors during build
- Type not found errors

**Solutions**:

1. **Clean and Rebuild**:
   ```bash
   dotnet clean
   dotnet build
   ```

2. **Check .NET Version**:
   ```bash
   dotnet --version
   ```
   Must be 9.0 or higher.

3. **Update SDK**:
   - Download from: https://dot.net

### Razor Compilation Errors

**Symptoms**:
- RZ errors in .razor files
- Component not rendering

**Solutions**:

1. **Check Syntax**:
   - Ensure @ symbols are properly escaped
   - Verify closing tags

2. **Clear Razor Cache**:
   ```bash
   rm -rf obj/
   dotnet build
   ```

---

## Getting Help

If issues persist:

1. **Check Logs**:
   ```bash
   dotnet run --verbosity detailed > app.log 2>&1
   ```

2. **Enable Debug Logging**:
   In `appsettings.Development.json`:
   ```json
   {
     "Logging": {
       "LogLevel": {
         "Default": "Debug",
         "Microsoft.AspNetCore": "Debug"
       }
     }
   }
   ```

3. **Report Issues**:
   - GitHub Issues: https://github.com/GreenShadeZhang/agent-framework-tutorial-code-/issues
   - Include:
     - Error messages
     - Steps to reproduce
     - Environment details (OS, .NET version)

4. **Community Resources**:
   - Microsoft Agent Framework: https://github.com/microsoft/agent-framework
   - Azure OpenAI Docs: https://learn.microsoft.com/azure/ai-services/openai/
   - Blazor Docs: https://learn.microsoft.com/aspnet/core/blazor

---

## Quick Diagnostic Checklist

Run through this checklist to diagnose most issues:

- [ ] .NET 9.0+ installed: `dotnet --version`
- [ ] Azure CLI logged in: `az account show`
- [ ] Azure OpenAI endpoint configured in appsettings.json
- [ ] Deployment name matches Azure portal
- [ ] Permissions granted in Azure IAM
- [ ] No other app instances running
- [ ] Database file not locked
- [ ] Internet connectivity working
- [ ] Packages restored: `dotnet restore`
- [ ] Build successful: `dotnet build`

If all checks pass but issues remain, see specific sections above or reach out for help.
