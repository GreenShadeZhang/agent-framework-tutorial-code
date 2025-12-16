using Microsoft.AspNetCore.Mvc;
using WorkflowDesigner.Api.Models;
using WorkflowDesigner.Api.Services;

namespace WorkflowDesigner.Api.Controllers;

/// <summary>
/// 智能体管理控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly IAgentService _agentService;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(IAgentService agentService, ILogger<AgentsController> logger)
    {
        _agentService = agentService;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有智能体
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AgentDefinition>>> GetAll()
    {
        try
        {
            var agents = await _agentService.GetAllAgentsAsync();
            return Ok(agents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all agents");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 根据ID获取智能体
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<AgentDefinition>> GetById(string id)
    {
        try
        {
            var agent = await _agentService.GetAgentByIdAsync(id);
            if (agent == null)
            {
                return NotFound();
            }
            return Ok(agent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting agent {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 创建智能体
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AgentDefinition>> Create([FromBody] AgentDefinition agent)
    {
        try
        {
            var created = await _agentService.CreateAgentAsync(agent);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating agent");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 更新智能体
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<AgentDefinition>> Update(string id, [FromBody] AgentDefinition agent)
    {
        try
        {
            var updated = await _agentService.UpdateAgentAsync(id, agent);
            if (updated == null)
            {
                return NotFound();
            }
            return Ok(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating agent {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 删除智能体
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id)
    {
        try
        {
            var result = await _agentService.DeleteAgentAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting agent {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }
}
