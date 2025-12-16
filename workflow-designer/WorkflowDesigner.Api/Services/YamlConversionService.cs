using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using WorkflowDesigner.Api.Models;
using System.Text;

namespace WorkflowDesigner.Api.Services;

/// <summary>
/// YAML 转换服务 - 在声明式工作流定义和 YAML 格式之间转换
/// 对齐 Agent Framework AdaptiveDialog YAML 格式
/// </summary>
public class YamlConversionService
{
    private readonly ISerializer _serializer;
    private readonly IDeserializer _deserializer;
    private readonly ILogger<YamlConversionService> _logger;

    public YamlConversionService(ILogger<YamlConversionService> logger)
    {
        _logger = logger;

        _serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();

        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// 将声明式工作流定义转换为 AdaptiveDialog YAML 格式
    /// </summary>
    public string ConvertToYaml(DeclarativeWorkflowDefinition workflow)
    {
        var adaptiveDialog = ConvertToAdaptiveDialog(workflow);
        var yaml = _serializer.Serialize(adaptiveDialog);
        return yaml;
    }

    /// <summary>
    /// 从 YAML 解析为声明式工作流定义
    /// 支持 Agent Framework 官方格式 (kind: Workflow) 和 AdaptiveDialog 格式
    /// </summary>
    public DeclarativeWorkflowDefinition ParseFromYaml(string yaml)
    {
        // 首先尝试检测 YAML 格式
        var genericYaml = _deserializer.Deserialize<Dictionary<string, object>>(yaml);
        
        if (genericYaml != null && genericYaml.TryGetValue("kind", out var kindObj))
        {
            var kind = kindObj?.ToString();
            if (kind == "Workflow")
            {
                // Agent Framework 官方格式
                return ParseAgentFrameworkYaml(yaml, genericYaml);
            }
        }
        
        // 回退到 AdaptiveDialog 格式
        var adaptiveDialog = _deserializer.Deserialize<AdaptiveDialogYaml>(yaml);
        return ConvertFromAdaptiveDialog(adaptiveDialog);
    }

    /// <summary>
    /// 解析 Agent Framework 官方 YAML 格式
    /// 支持两种格式：
    /// 1. trigger.actions 格式 - 简单的线性工作流
    /// 2. executors 格式 - 带有显式 edges 的复杂工作流（支持并行）
    /// </summary>
    private DeclarativeWorkflowDefinition ParseAgentFrameworkYaml(string yaml, Dictionary<string, object> root)
    {
        var workflow = new DeclarativeWorkflowDefinition
        {
            Id = Guid.NewGuid().ToString(),
            Version = "1.0.0",
            Executors = new List<ExecutorDefinition>(),
            EdgeGroups = new List<EdgeGroupDefinition>()
        };

        // 解析 metadata
        if (root.TryGetValue("metadata", out var metadataObj) && metadataObj is Dictionary<object, object> metadata)
        {
            if (metadata.TryGetValue("name", out var name))
                workflow.Name = name?.ToString() ?? "";
            if (metadata.TryGetValue("version", out var version))
                workflow.Version = version?.ToString() ?? "1.0.0";
            if (metadata.TryGetValue("description", out var description))
                workflow.Description = description?.ToString() ?? "";
        }

        // 检查是否是 executors 格式（带有显式 edges 的复杂工作流）
        if (root.TryGetValue("executors", out var executorsObj) && executorsObj is List<object> executorsList)
        {
            return ParseExecutorsFormat(workflow, executorsList);
        }

        // 回退到 trigger.actions 格式
        return ParseTriggerActionsFormat(workflow, root);
    }

    /// <summary>
    /// 解析 executors 格式 - 带有显式 edges 的复杂工作流（支持并行和条件）
    /// </summary>
    private DeclarativeWorkflowDefinition ParseExecutorsFormat(DeclarativeWorkflowDefinition workflow, List<object> executorsList)
    {
        var incomingEdges = new Dictionary<string, List<string>>(); // 记录每个节点的入边
        var outgoingEdgesWithConditions = new Dictionary<string, List<(string targetId, string? condition)>>(); // 带条件的出边
        
        // 第一遍：收集所有执行器和边信息
        foreach (var execObj in executorsList)
        {
            if (execObj is not Dictionary<object, object> execDict) continue;

            var id = execDict.TryGetValue("id", out var idObj) ? idObj?.ToString() ?? "" : "";
            if (string.IsNullOrEmpty(id)) continue;

            if (!incomingEdges.ContainsKey(id))
                incomingEdges[id] = new List<string>();
            if (!outgoingEdgesWithConditions.ContainsKey(id))
                outgoingEdgesWithConditions[id] = new List<(string, string?)>();

            // 收集边信息
            if (execDict.TryGetValue("edges", out var edgesObj) && edgesObj is List<object> edges)
            {
                foreach (var edgeObj in edges)
                {
                    string? targetId = null;
                    string? condition = null;
                    
                    if (edgeObj is Dictionary<object, object> edgeDict)
                    {
                        if (edgeDict.TryGetValue("targetId", out var targetIdObj))
                            targetId = targetIdObj?.ToString();
                        if (edgeDict.TryGetValue("condition", out var conditionObj))
                            condition = conditionObj?.ToString();
                    }
                    else if (edgeObj is string targetStr)
                    {
                        targetId = targetStr;
                    }

                    if (!string.IsNullOrEmpty(targetId))
                    {
                        outgoingEdgesWithConditions[id].Add((targetId, condition));
                        if (!incomingEdges.ContainsKey(targetId))
                            incomingEdges[targetId] = new List<string>();
                        incomingEdges[targetId].Add(id);
                    }
                }
            }
        }

        // 找到起始节点（没有入边的节点）
        var startNodes = incomingEdges.Where(kv => kv.Value.Count == 0).Select(kv => kv.Key).ToList();
        if (startNodes.Count > 0)
        {
            workflow.StartExecutorId = startNodes[0];
        }

        // 计算节点布局
        var outgoingEdgesSimple = outgoingEdgesWithConditions.ToDictionary(
            kv => kv.Key, 
            kv => kv.Value.Select(e => e.targetId).ToList());
        var levels = CalculateNodeLevels(startNodes, outgoingEdgesSimple);
        var nodesPerLevel = new Dictionary<int, int>();

        // 第二遍：创建执行器定义
        foreach (var execObj in executorsList)
        {
            if (execObj is not Dictionary<object, object> execDict) continue;

            var id = execDict.TryGetValue("id", out var idObj) ? idObj?.ToString() ?? "" : "";
            if (string.IsNullOrEmpty(id)) continue;

            var level = levels.GetValueOrDefault(id, 0);
            var nodeIndexInLevel = nodesPerLevel.GetValueOrDefault(level, 0);
            nodesPerLevel[level] = nodeIndexInLevel + 1;

            // 计算位置 - 水平布局同一层级的节点
            var xOffset = 250 + nodeIndexInLevel * 300;
            var yOffset = level * 150.0;

            var executor = ConvertExecutorDictToExecutor(execDict, xOffset, yOffset);
            if (executor != null)
            {
                workflow.Executors.Add(executor);
            }
        }

        // 第三遍：创建边组（支持条件边）
        foreach (var (sourceId, edgesWithConditions) in outgoingEdgesWithConditions)
        {
            if (edgesWithConditions.Count == 0) continue;

            // 判断边类型
            var hasConditions = edgesWithConditions.Any(e => !string.IsNullOrEmpty(e.condition));
            EdgeGroupType edgeType;
            
            if (hasConditions)
            {
                edgeType = EdgeGroupType.SwitchCase;
            }
            else if (edgesWithConditions.Count > 1)
            {
                edgeType = EdgeGroupType.FanOut;
            }
            else
            {
                edgeType = EdgeGroupType.Single;
            }

            workflow.EdgeGroups.Add(new EdgeGroupDefinition
            {
                Type = edgeType,
                SourceExecutorId = sourceId,
                Edges = edgesWithConditions.Select(e => new EdgeDefinition 
                { 
                    TargetExecutorId = e.targetId,
                    Condition = e.condition
                }).ToList()
            });
        }

        _logger.LogInformation("成功解析 executors 格式 YAML，共 {Count} 个节点, {EdgeCount} 个边组", 
            workflow.Executors.Count, workflow.EdgeGroups.Count);
        return workflow;
    }

    /// <summary>
    /// 计算节点层级（用于布局）
    /// </summary>
    private Dictionary<string, int> CalculateNodeLevels(List<string> startNodes, Dictionary<string, List<string>> outgoingEdges)
    {
        var levels = new Dictionary<string, int>();
        var queue = new Queue<(string id, int level)>();

        foreach (var start in startNodes)
        {
            queue.Enqueue((start, 0));
            levels[start] = 0;
        }

        while (queue.Count > 0)
        {
            var (id, level) = queue.Dequeue();

            if (outgoingEdges.TryGetValue(id, out var targets))
            {
                foreach (var target in targets)
                {
                    var newLevel = level + 1;
                    if (!levels.ContainsKey(target) || levels[target] < newLevel)
                    {
                        levels[target] = newLevel;
                        queue.Enqueue((target, newLevel));
                    }
                }
            }
        }

        return levels;
    }

    /// <summary>
    /// 将 executor 字典转换为执行器定义
    /// </summary>
    private ExecutorDefinition? ConvertExecutorDictToExecutor(Dictionary<object, object> execDict, double xOffset, double yOffset)
    {
        var id = execDict.TryGetValue("id", out var idObj) ? idObj?.ToString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
        var type = execDict.TryGetValue("type", out var typeObj) ? typeObj?.ToString() ?? "" : "";

        var executor = new ExecutorDefinition
        {
            Id = id,
            Name = id,
            Position = new NodePosition { X = xOffset, Y = yOffset },
            Config = new Dictionary<string, object>()
        };

        // 获取 properties
        var properties = execDict.TryGetValue("properties", out var propsObj) && propsObj is Dictionary<object, object> props
            ? props
            : new Dictionary<object, object>();

        switch (type)
        {
            case "Question":
                executor.Type = ExecutorType.Question;
                if (properties.TryGetValue("prompt", out var prompt))
                    executor.Config["prompt"] = prompt?.ToString() ?? "";
                if (properties.TryGetValue("variable", out var variable))
                    executor.Config["resultVariable"] = variable?.ToString() ?? "";
                break;

            case "InvokeAzureAgent":
                executor.Type = ExecutorType.InvokeAzureAgent;
                if (properties.TryGetValue("agentId", out var agentId))
                    executor.Config["name"] = agentId?.ToString() ?? "";
                if (properties.TryGetValue("instructions", out var instructions))
                    executor.Config["instructionsTemplate"] = instructions?.ToString() ?? "";
                if (properties.TryGetValue("message", out var message))
                    executor.Config["message"] = message?.ToString() ?? "";
                if (properties.TryGetValue("result", out var result))
                    executor.Config["resultVariable"] = result?.ToString() ?? "";
                break;

            case "SendActivity":
                executor.Type = ExecutorType.SendActivity;
                if (properties.TryGetValue("activity", out var activity))
                {
                    if (activity is Dictionary<object, object> actDict && actDict.TryGetValue("text", out var text))
                        executor.Config["message"] = text?.ToString() ?? "";
                    else
                        executor.Config["message"] = activity?.ToString() ?? "";
                }
                break;

            case "SetVariable":
                executor.Type = ExecutorType.SetVariable;
                if (properties.TryGetValue("variable", out var varName))
                    executor.Config["variableName"] = varName?.ToString() ?? "";
                if (properties.TryGetValue("value", out var varValue))
                    executor.Config["value"] = varValue?.ToString() ?? "";
                break;

            case "ConditionGroup":
                executor.Type = ExecutorType.ConditionGroup;
                // TODO: 解析嵌套条件
                break;

            case "Foreach":
                executor.Type = ExecutorType.Foreach;
                if (properties.TryGetValue("items", out var items))
                    executor.Config["itemsExpression"] = items?.ToString() ?? "";
                if (properties.TryGetValue("item", out var item))
                    executor.Config["itemVariableName"] = item?.ToString() ?? "item";
                break;

            case "EndWorkflow":
                executor.Type = ExecutorType.EndWorkflow;
                break;

            default:
                _logger.LogWarning("未知的执行器类型: {Type}", type);
                // 默认作为 SendActivity 处理
                executor.Type = ExecutorType.SendActivity;
                executor.Config["message"] = $"[{type}] 节点";
                break;
        }

        return executor;
    }

    /// <summary>
    /// 解析 trigger.actions 格式 - 简单的线性工作流
    /// </summary>
    private DeclarativeWorkflowDefinition ParseTriggerActionsFormat(DeclarativeWorkflowDefinition workflow, Dictionary<string, object> root)
    {
        // 获取 trigger
        if (!root.TryGetValue("trigger", out var triggerObj) || triggerObj == null)
        {
            _logger.LogWarning("YAML 缺少 trigger 节点");
            return workflow;
        }

        var trigger = triggerObj as Dictionary<object, object>;
        if (trigger == null)
        {
            _logger.LogWarning("trigger 节点格式不正确");
            return workflow;
        }

        // 获取工作流 ID
        if (trigger.TryGetValue("id", out var idObj))
        {
            workflow.Name = idObj?.ToString() ?? "";
        }

        // 获取 actions
        if (!trigger.TryGetValue("actions", out var actionsObj) || actionsObj == null)
        {
            _logger.LogWarning("trigger 缺少 actions 节点");
            return workflow;
        }

        var actions = actionsObj as List<object>;
        if (actions == null)
        {
            _logger.LogWarning("actions 节点格式不正确");
            return workflow;
        }

        var lastExecutorId = string.Empty;
        var yOffset = 0.0;

        foreach (var actionObj in actions)
        {
            var action = actionObj as Dictionary<object, object>;
            if (action == null) continue;

            var executor = ConvertAgentFrameworkActionToExecutor(action, yOffset);
            if (executor != null)
            {
                workflow.Executors.Add(executor);

                // 创建边连接
                if (!string.IsNullOrEmpty(lastExecutorId))
                {
                    workflow.EdgeGroups.Add(new EdgeGroupDefinition
                    {
                        Type = EdgeGroupType.Single,
                        SourceExecutorId = lastExecutorId,
                        Edges = new List<EdgeDefinition>
                        {
                            new EdgeDefinition { TargetExecutorId = executor.Id }
                        }
                    });
                }
                else
                {
                    workflow.StartExecutorId = executor.Id;
                }

                lastExecutorId = executor.Id;
                yOffset += 120;
            }
        }

        _logger.LogInformation("成功解析 trigger.actions 格式 YAML，共 {Count} 个节点", workflow.Executors.Count);
        return workflow;
    }

    /// <summary>
    /// 将 Agent Framework action 转换为执行器
    /// </summary>
    private ExecutorDefinition? ConvertAgentFrameworkActionToExecutor(Dictionary<object, object> action, double yOffset)
    {
        if (!action.TryGetValue("kind", out var kindObj))
            return null;

        var kind = kindObj?.ToString() ?? "";
        var id = action.TryGetValue("id", out var idObj) ? idObj?.ToString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();

        var executor = new ExecutorDefinition
        {
            Id = id,
            Name = id,
            Position = new NodePosition { X = 250, Y = yOffset },
            Config = new Dictionary<string, object>()
        };

        switch (kind)
        {
            case "SetVariable":
            case "SetTextVariable":
                executor.Type = ExecutorType.SetVariable;
                if (action.TryGetValue("variable", out var varName))
                    executor.Config["variableName"] = varName?.ToString() ?? "";
                if (action.TryGetValue("value", out var varValue))
                    executor.Config["value"] = varValue?.ToString() ?? "";
                break;

            case "SendActivity":
                executor.Type = ExecutorType.SendActivity;
                if (action.TryGetValue("activity", out var activity))
                    executor.Config["message"] = activity?.ToString() ?? "";
                break;

            case "Question":
                executor.Type = ExecutorType.Question;
                if (action.TryGetValue("prompt", out var prompt))
                    executor.Config["prompt"] = prompt?.ToString() ?? "";
                if (action.TryGetValue("variable", out var resultVar))
                    executor.Config["resultVariable"] = resultVar?.ToString() ?? "";
                break;

            case "InvokeAzureAgent":
                executor.Type = ExecutorType.InvokeAzureAgent;
                if (action.TryGetValue("agent", out var agentObj) && agentObj is Dictionary<object, object> agent)
                {
                    if (agent.TryGetValue("name", out var agentName))
                        executor.Config["name"] = agentName?.ToString() ?? "";
                    if (agent.TryGetValue("instructions", out var instructions))
                        executor.Config["instructionsTemplate"] = instructions?.ToString() ?? "";
                }
                if (action.TryGetValue("conversationId", out var convId))
                    executor.Config["conversationId"] = convId?.ToString() ?? "";
                break;

            case "ConditionGroup":
                executor.Type = ExecutorType.ConditionGroup;
                // TODO: 解析嵌套条件
                break;

            case "GotoAction":
                executor.Type = ExecutorType.GotoAction;
                if (action.TryGetValue("actionId", out var targetId))
                    executor.Config["targetExecutorId"] = targetId?.ToString() ?? "";
                break;

            case "Foreach":
                executor.Type = ExecutorType.Foreach;
                if (action.TryGetValue("items", out var items))
                    executor.Config["itemsExpression"] = items?.ToString() ?? "";
                if (action.TryGetValue("item", out var item))
                    executor.Config["itemVariableName"] = item?.ToString() ?? "item";
                break;

            case "EndWorkflow":
                executor.Type = ExecutorType.EndWorkflow;
                break;

            case "EndConversation":
                executor.Type = ExecutorType.EndConversation;
                break;

            case "CreateConversation":
                executor.Type = ExecutorType.CreateConversation;
                break;

            default:
                _logger.LogWarning("未知的 Action 类型: {Kind}", kind);
                return null;
        }

        return executor;
    }

    /// <summary>
    /// 转换为 AdaptiveDialog 格式
    /// </summary>
    private AdaptiveDialogYaml ConvertToAdaptiveDialog(DeclarativeWorkflowDefinition workflow)
    {
        var dialog = new AdaptiveDialogYaml
        {
            Kind = "AdaptiveDialog",
            Id = new IdProperty { Value = workflow.Id },
            DialogVersion = workflow.Version,
            Actions = new List<ActionYaml>()
        };

        // 按照拓扑排序执行器
        var sortedExecutors = TopologicalSort(workflow);

        foreach (var executor in sortedExecutors)
        {
            var action = ConvertExecutorToAction(executor, workflow);
            if (action != null)
            {
                dialog.Actions.Add(action);
            }
        }

        return dialog;
    }

    /// <summary>
    /// 从 AdaptiveDialog 格式转换
    /// </summary>
    private DeclarativeWorkflowDefinition ConvertFromAdaptiveDialog(AdaptiveDialogYaml dialog)
    {
        var workflow = new DeclarativeWorkflowDefinition
        {
            Id = dialog.Id?.Value ?? Guid.NewGuid().ToString(),
            Version = dialog.DialogVersion ?? "1.0.0",
            Executors = new List<ExecutorDefinition>(),
            EdgeGroups = new List<EdgeGroupDefinition>()
        };

        var lastExecutorId = string.Empty;
        var yOffset = 0.0;

        foreach (var action in dialog.Actions ?? new List<ActionYaml>())
        {
            var executor = ConvertActionToExecutor(action, yOffset);
            if (executor != null)
            {
                workflow.Executors.Add(executor);

                // 创建边连接
                if (!string.IsNullOrEmpty(lastExecutorId))
                {
                    workflow.EdgeGroups.Add(new EdgeGroupDefinition
                    {
                        Type = EdgeGroupType.Single,
                        SourceExecutorId = lastExecutorId,
                        Edges = new List<EdgeDefinition>
                        {
                            new EdgeDefinition { TargetExecutorId = executor.Id }
                        }
                    });
                }
                else
                {
                    workflow.StartExecutorId = executor.Id;
                }

                lastExecutorId = executor.Id;
                yOffset += 120;
            }
        }

        return workflow;
    }

    /// <summary>
    /// 将执行器转换为 YAML Action
    /// </summary>
    private ActionYaml? ConvertExecutorToAction(ExecutorDefinition executor, DeclarativeWorkflowDefinition workflow)
    {
        return executor.Type switch
        {
            ExecutorType.ChatAgent or ExecutorType.FunctionAgent or ExecutorType.ToolAgent =>
                ConvertAgentExecutorToAction(executor),

            ExecutorType.Condition =>
                ConvertConditionToAction(executor),

            ExecutorType.ConditionGroup =>
                ConvertConditionGroupToAction(executor),

            ExecutorType.Foreach =>
                ConvertForeachToAction(executor, workflow),

            ExecutorType.SetVariable or ExecutorType.SetMultipleVariables =>
                ConvertSetVariableToAction(executor),

            ExecutorType.SendActivity =>
                ConvertSendActivityToAction(executor),

            ExecutorType.Question =>
                ConvertQuestionToAction(executor),

            ExecutorType.EndWorkflow =>
                ConvertEndWorkflowToAction(executor),

            ExecutorType.EndConversation =>
                ConvertEndConversationToAction(executor),

            ExecutorType.AzureAgent =>
                ConvertAzureAgentToAction(executor),

            ExecutorType.SubWorkflow =>
                ConvertSubWorkflowToAction(executor),

            ExecutorType.ParallelExecution or ExecutorType.FanOut =>
                ConvertParallelToAction(executor, workflow),

            _ => ConvertDefaultExecutorToAction(executor)
        };
    }

    #region Executor to Action Converters

    private ActionYaml ConvertAgentExecutorToAction(ExecutorDefinition executor)
    {
        var agentConfig = GetConfigValue<AgentExecutorConfig>(executor.Config, "agentConfig");

        return new ActionYaml
        {
            Kind = "InvokeAzureAgent",
            Id = new IdProperty { Value = executor.Id },
            AgentName = agentConfig?.Name ?? executor.Name,
            Instructions = agentConfig?.InstructionsTemplate,
            Model = agentConfig?.ModelConfig?.Model ?? "gpt-4o",
            Tools = agentConfig?.Tools?.Select(t => new ToolYaml
            {
                Kind = MapToolTypeToKind(t.Type),
                Name = t.Name,
                Config = t.Config
            }).ToList()
        };
    }

    private ActionYaml ConvertConditionToAction(ExecutorDefinition executor)
    {
        var conditionConfig = GetConfigValue<ConditionConfig>(executor.Config, "conditionConfig");

        return new ActionYaml
        {
            Kind = "ConditionGroup",
            Id = new IdProperty { Value = executor.Id },
            Conditions = new List<ConditionItemYaml>
            {
                new ConditionItemYaml
                {
                    Expression = conditionConfig?.Expression ?? "true",
                    Actions = new List<ActionYaml>
                    {
                        new ActionYaml
                        {
                            Kind = "GotoAction",
                            ActionId = conditionConfig?.TrueBranchTarget
                        }
                    }
                }
            },
            ElseActions = conditionConfig?.FalseBranchTarget != null
                ? new List<ActionYaml>
                {
                    new ActionYaml
                    {
                        Kind = "GotoAction",
                        ActionId = conditionConfig.FalseBranchTarget
                    }
                }
                : null
        };
    }

    private ActionYaml ConvertConditionGroupToAction(ExecutorDefinition executor)
    {
        var groupConfig = GetConfigValue<ConditionGroupConfig>(executor.Config, "conditionGroupConfig");

        return new ActionYaml
        {
            Kind = "ConditionGroup",
            Id = new IdProperty { Value = executor.Id },
            Conditions = groupConfig?.Conditions?.Select(c => new ConditionItemYaml
            {
                Expression = c.Expression,
                Actions = new List<ActionYaml>
                {
                    new ActionYaml
                    {
                        Kind = "GotoAction",
                        ActionId = c.TargetExecutorId
                    }
                }
            }).ToList(),
            ElseActions = groupConfig?.DefaultTarget != null
                ? new List<ActionYaml>
                {
                    new ActionYaml
                    {
                        Kind = "GotoAction",
                        ActionId = groupConfig.DefaultTarget
                    }
                }
                : null
        };
    }

    private ActionYaml ConvertForeachToAction(ExecutorDefinition executor, DeclarativeWorkflowDefinition workflow)
    {
        var foreachConfig = GetConfigValue<ForeachConfig>(executor.Config, "foreachConfig");

        return new ActionYaml
        {
            Kind = "Foreach",
            Id = new IdProperty { Value = executor.Id },
            ItemsProperty = foreachConfig?.ItemsExpression ?? "[]",
            Value = foreachConfig?.ItemVariableName ?? "item",
            Index = foreachConfig?.IndexVariableName ?? "index",
            Actions = GetActionsForBody(foreachConfig?.BodyStartExecutorId, workflow)
        };
    }

    private ActionYaml ConvertSetVariableToAction(ExecutorDefinition executor)
    {
        var variableName = GetConfigValue<string>(executor.Config, "variableName") ?? "";
        var value = GetConfigValue<string>(executor.Config, "value") ?? "";

        return new ActionYaml
        {
            Kind = executor.Type == ExecutorType.SetVariable ? "SetVariable" : "SetMultipleVariables",
            Id = new IdProperty { Value = executor.Id },
            Property = variableName,
            Value = value
        };
    }

    private ActionYaml ConvertSendActivityToAction(ExecutorDefinition executor)
    {
        var message = GetConfigValue<string>(executor.Config, "message") ?? "";

        return new ActionYaml
        {
            Kind = "SendActivity",
            Id = new IdProperty { Value = executor.Id },
            Activity = new ActivityYaml
            {
                Type = "message",
                Text = message
            }
        };
    }

    private ActionYaml ConvertQuestionToAction(ExecutorDefinition executor)
    {
        var prompt = GetConfigValue<string>(executor.Config, "prompt") ?? "";
        var variableName = GetConfigValue<string>(executor.Config, "resultVariable") ?? "user_response";

        return new ActionYaml
        {
            Kind = "Question",
            Id = new IdProperty { Value = executor.Id },
            Prompt = prompt,
            Property = variableName
        };
    }

    private ActionYaml ConvertEndWorkflowToAction(ExecutorDefinition executor)
    {
        return new ActionYaml
        {
            Kind = "EndWorkflow",
            Id = new IdProperty { Value = executor.Id }
        };
    }

    private ActionYaml ConvertEndConversationToAction(ExecutorDefinition executor)
    {
        return new ActionYaml
        {
            Kind = "EndConversation",
            Id = new IdProperty { Value = executor.Id }
        };
    }

    private ActionYaml ConvertAzureAgentToAction(ExecutorDefinition executor)
    {
        var agentName = GetConfigValue<string>(executor.Config, "agentName") ?? executor.Name;
        var connectionName = GetConfigValue<string>(executor.Config, "connectionName") ?? "";

        return new ActionYaml
        {
            Kind = "InvokeAzureAgent",
            Id = new IdProperty { Value = executor.Id },
            AgentName = agentName,
            ConnectionName = connectionName
        };
    }

    private ActionYaml ConvertSubWorkflowToAction(ExecutorDefinition executor)
    {
        var workflowId = GetConfigValue<string>(executor.Config, "workflowId") ?? "";

        return new ActionYaml
        {
            Kind = "BeginDialog",
            Id = new IdProperty { Value = executor.Id },
            Dialog = workflowId
        };
    }

    private ActionYaml ConvertParallelToAction(ExecutorDefinition executor, DeclarativeWorkflowDefinition workflow)
    {
        var parallelTargets = GetConfigValue<List<string>>(executor.Config, "targets") ?? new List<string>();

        return new ActionYaml
        {
            Kind = "ActionScope",
            Id = new IdProperty { Value = executor.Id },
            Actions = parallelTargets.Select(targetId =>
            {
                var targetExecutor = workflow.Executors.FirstOrDefault(e => e.Id == targetId);
                return targetExecutor != null
                    ? ConvertExecutorToAction(targetExecutor, workflow)
                    : null;
            }).Where(a => a != null).Cast<ActionYaml>().ToList()
        };
    }

    private ActionYaml ConvertDefaultExecutorToAction(ExecutorDefinition executor)
    {
        return new ActionYaml
        {
            Kind = executor.Type.ToString(),
            Id = new IdProperty { Value = executor.Id },
            Config = executor.Config
        };
    }

    #endregion

    #region Action to Executor Converters

    private ExecutorDefinition? ConvertActionToExecutor(ActionYaml action, double yOffset)
    {
        var executor = new ExecutorDefinition
        {
            Id = action.Id?.Value ?? Guid.NewGuid().ToString(),
            Name = action.AgentName ?? action.Kind ?? "Unknown",
            Position = new NodePosition { X = 250, Y = yOffset }
        };

        switch (action.Kind?.ToLower())
        {
            case "invokeazureagent":
                executor.Type = ExecutorType.AzureAgent;
                executor.Config = new Dictionary<string, object>
                {
                    ["agentName"] = action.AgentName ?? "",
                    ["connectionName"] = action.ConnectionName ?? "",
                    ["instructions"] = action.Instructions ?? "",
                    ["model"] = action.Model ?? "gpt-4o"
                };
                break;

            case "conditiongroup":
                executor.Type = ExecutorType.ConditionGroup;
                executor.Config = new Dictionary<string, object>
                {
                    ["conditions"] = action.Conditions ?? new List<ConditionItemYaml>()
                };
                break;

            case "foreach":
                executor.Type = ExecutorType.Foreach;
                executor.Config = new Dictionary<string, object>
                {
                    ["items"] = action.ItemsProperty ?? "",
                    ["itemVariable"] = action.Value ?? "item",
                    ["indexVariable"] = action.Index ?? "index"
                };
                break;

            case "setvariable":
                executor.Type = ExecutorType.SetVariable;
                executor.Config = new Dictionary<string, object>
                {
                    ["variableName"] = action.Property ?? "",
                    ["value"] = action.Value ?? ""
                };
                break;

            case "sendactivity":
                executor.Type = ExecutorType.SendActivity;
                executor.Config = new Dictionary<string, object>
                {
                    ["message"] = action.Activity?.Text ?? ""
                };
                break;

            case "question":
                executor.Type = ExecutorType.Question;
                executor.Config = new Dictionary<string, object>
                {
                    ["prompt"] = action.Prompt ?? "",
                    ["resultVariable"] = action.Property ?? "user_response"
                };
                break;

            case "endworkflow":
                executor.Type = ExecutorType.EndWorkflow;
                break;

            case "endconversation":
                executor.Type = ExecutorType.EndConversation;
                break;

            case "begindialog":
                executor.Type = ExecutorType.SubWorkflow;
                executor.Config = new Dictionary<string, object>
                {
                    ["workflowId"] = action.Dialog ?? ""
                };
                break;

            default:
                _logger.LogWarning("Unknown action kind: {Kind}", action.Kind);
                executor.Type = ExecutorType.ChatAgent;
                executor.Config = action.Config ?? new Dictionary<string, object>();
                break;
        }

        return executor;
    }

    #endregion

    #region Helper Methods

    private T? GetConfigValue<T>(Dictionary<string, object> config, string key)
    {
        if (config.TryGetValue(key, out var value))
        {
            if (value is T typedValue)
            {
                return typedValue;
            }
            // 尝试 JSON 反序列化
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(value);
                return System.Text.Json.JsonSerializer.Deserialize<T>(json);
            }
            catch
            {
                return default;
            }
        }
        return default;
    }

    private string MapToolTypeToKind(ToolType type)
    {
        return type switch
        {
            ToolType.Function => "function",
            ToolType.Mcp => "mcp",
            ToolType.OpenApi => "openapi",
            ToolType.CodeInterpreter => "code_interpreter",
            ToolType.FileSearch => "file_search",
            ToolType.WebSearch => "web_search",
            _ => "custom"
        };
    }

    private List<ActionYaml> GetActionsForBody(string? startExecutorId, DeclarativeWorkflowDefinition workflow)
    {
        if (string.IsNullOrEmpty(startExecutorId))
        {
            return new List<ActionYaml>();
        }

        // 获取循环体内的所有执行器
        var bodyExecutors = new List<ExecutorDefinition>();
        var visited = new HashSet<string>();
        var currentId = startExecutorId;

        while (!string.IsNullOrEmpty(currentId) && !visited.Contains(currentId))
        {
            visited.Add(currentId);
            var executor = workflow.Executors.FirstOrDefault(e => e.Id == currentId);
            if (executor != null)
            {
                bodyExecutors.Add(executor);
                // 查找下一个执行器
                var edge = workflow.EdgeGroups
                    .SelectMany(g => g.Edges)
                    .FirstOrDefault(e => 
                        workflow.EdgeGroups.Any(g => 
                            g.SourceExecutorId == currentId && g.Edges.Contains(e)));
                currentId = edge?.TargetExecutorId;
            }
            else
            {
                break;
            }
        }

        return bodyExecutors
            .Select(e => ConvertExecutorToAction(e, workflow))
            .Where(a => a != null)
            .Cast<ActionYaml>()
            .ToList();
    }

    private List<ExecutorDefinition> TopologicalSort(DeclarativeWorkflowDefinition workflow)
    {
        var sorted = new List<ExecutorDefinition>();
        var visited = new HashSet<string>();

        void Visit(string executorId)
        {
            if (visited.Contains(executorId)) return;
            visited.Add(executorId);

            var executor = workflow.Executors.FirstOrDefault(e => e.Id == executorId);
            if (executor != null)
            {
                sorted.Add(executor);

                // 查找后续执行器
                var outgoingEdges = workflow.EdgeGroups
                    .Where(g => g.SourceExecutorId == executorId)
                    .SelectMany(g => g.Edges);

                foreach (var edge in outgoingEdges)
                {
                    Visit(edge.TargetExecutorId);
                }
            }
        }

        // 从入口开始
        if (!string.IsNullOrEmpty(workflow.StartExecutorId))
        {
            Visit(workflow.StartExecutorId);
        }

        // 添加未访问的执行器
        foreach (var executor in workflow.Executors)
        {
            if (!visited.Contains(executor.Id))
            {
                sorted.Add(executor);
            }
        }

        return sorted;
    }

    #endregion
}

#region YAML Model Classes

/// <summary>
/// AdaptiveDialog YAML 模型
/// </summary>
public class AdaptiveDialogYaml
{
    [YamlMember(Alias = "$kind")]
    public string Kind { get; set; } = "AdaptiveDialog";

    public IdProperty? Id { get; set; }

    public string? DialogVersion { get; set; }

    public List<ActionYaml>? Actions { get; set; }
}

/// <summary>
/// ID 属性
/// </summary>
public class IdProperty
{
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Action YAML 模型
/// </summary>
public class ActionYaml
{
    [YamlMember(Alias = "$kind")]
    public string? Kind { get; set; }

    public IdProperty? Id { get; set; }

    // Agent相关
    public string? AgentName { get; set; }
    public string? Instructions { get; set; }
    public string? Model { get; set; }
    public List<ToolYaml>? Tools { get; set; }
    public string? ConnectionName { get; set; }

    // 条件相关
    public List<ConditionItemYaml>? Conditions { get; set; }
    public List<ActionYaml>? ElseActions { get; set; }

    // 循环相关
    public string? ItemsProperty { get; set; }
    public string? Value { get; set; }
    public string? Index { get; set; }
    public List<ActionYaml>? Actions { get; set; }

    // 变量相关
    public string? Property { get; set; }

    // 发送活动
    public ActivityYaml? Activity { get; set; }

    // 问题
    public string? Prompt { get; set; }

    // 跳转
    public string? ActionId { get; set; }

    // 子工作流
    public string? Dialog { get; set; }

    // 通用配置
    public Dictionary<string, object>? Config { get; set; }
}

/// <summary>
/// 条件项 YAML 模型
/// </summary>
public class ConditionItemYaml
{
    public string Expression { get; set; } = string.Empty;
    public List<ActionYaml>? Actions { get; set; }
}

/// <summary>
/// 工具 YAML 模型
/// </summary>
public class ToolYaml
{
    [YamlMember(Alias = "$kind")]
    public string? Kind { get; set; }

    public string? Name { get; set; }
    public Dictionary<string, object>? Config { get; set; }
}

/// <summary>
/// Activity YAML 模型
/// </summary>
public class ActivityYaml
{
    public string? Type { get; set; }
    public string? Text { get; set; }
}

#endregion
