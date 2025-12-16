using Scriban;
using Scriban.Runtime;

namespace WorkflowDesigner.Api.Services;

/// <summary>
/// 模板渲染服务
/// </summary>
public interface ITemplateService
{
    /// <summary>
    /// 渲染模板
    /// </summary>
    Task<string> RenderAsync(string template, Dictionary<string, object> parameters);

    /// <summary>
    /// 验证模板语法
    /// </summary>
    Task<(bool IsValid, string? Error)> ValidateAsync(string template);
}

/// <summary>
/// Scriban 模板渲染服务实现
/// </summary>
public class ScribanTemplateService : ITemplateService
{
    private readonly ILogger<ScribanTemplateService> _logger;

    public ScribanTemplateService(ILogger<ScribanTemplateService> logger)
    {
        _logger = logger;
    }

    public async Task<string> RenderAsync(string template, Dictionary<string, object> parameters)
    {
        try
        {
            var scribanTemplate = Template.Parse(template);
            
            if (scribanTemplate.HasErrors)
            {
                var errors = string.Join(", ", scribanTemplate.Messages.Select(m => m.Message));
                throw new InvalidOperationException($"Template parsing failed: {errors}");
            }

            var scriptObject = new ScriptObject();
            foreach (var param in parameters)
            {
                scriptObject.Add(param.Key, param.Value);
            }

            var context = new TemplateContext();
            context.PushGlobal(scriptObject);

            var result = await scribanTemplate.RenderAsync(context);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering template");
            throw;
        }
    }

    public async Task<(bool IsValid, string? Error)> ValidateAsync(string template)
    {
        try
        {
            var scribanTemplate = Template.Parse(template);
            
            if (scribanTemplate.HasErrors)
            {
                var errors = string.Join(", ", scribanTemplate.Messages.Select(m => m.Message));
                return (false, errors);
            }

            return await Task.FromResult<(bool, string?)>((true, null));
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
