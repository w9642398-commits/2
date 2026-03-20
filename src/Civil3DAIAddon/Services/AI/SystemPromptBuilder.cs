using Civil3DAIAddon.Models.AI;
using Newtonsoft.Json;

namespace Civil3DAIAddon.Services.AI;

public static class SystemPromptBuilder
{
    private static readonly List<string> SafetyRules = new()
    {
        "Never delete or erase objects without explicit user confirmation.",
        "Never modify objects in external references (XREF) unless explicitly handled.",
        "Never modify locked layers without user consent.",
        "Never fabricate or guess entity handles - only use handles from the drawing context.",
        "If the prompt is ambiguous, set clarification_needed with a precise question.",
        "Group all changes into a single undo scope.",
        "After execution, validate geometric and logical results.",
        "Do not hallucinate results - if an operation is not supported, say so clearly.",
        "Do not generate free-form text instructions for CAD. Return structured tool calls only.",
        "Always specify exact parameter values. Never use placeholder values."
    };

    public static string BuildSystemPrompt(DrawingSnapshot snapshot, IReadOnlyList<ToolDefinition> tools)
    {
        var toolDescriptions = tools.Select(t => new
        {
            t.Name,
            t.Description,
            t.Category,
            t.SafetyLevel,
            Parameters = t.ParameterSchema
        });

        return $"""
You are a Civil 3D / AutoCAD AI assistant operating inside the Autodesk Civil 3D 2026 application.
You receive a user prompt and a snapshot of the current drawing state.
Your role is to analyze the prompt and produce a structured execution plan as JSON.

CRITICAL: You must ONLY return a valid JSON object matching the schema below. No markdown, no prose, no code blocks.

## Drawing Context
{JsonConvert.SerializeObject(snapshot, Formatting.Indented)}

## Available Tools
{JsonConvert.SerializeObject(toolDescriptions, Formatting.Indented)}

## Safety Rules
{string.Join("\n", SafetyRules.Select((r, i) => $"{i + 1}. {r}"))}

## Response Schema
Return EXACTLY this JSON structure:
{{
  "intent": "string - what the user wants to achieve",
  "assumptions": ["string - assumptions you are making"],
  "target_objects": [{{ "handle": "string|null", "type": "string", "layer": "string|null", "description": "string|null" }}],
  "ordered_steps": [
    {{
      "step_number": 1,
      "tool_name": "string - must be one of the available tools",
      "parameters": {{ }},
      "description": "string",
      "depends_on": [],
      "rollback_tool": "string|null"
    }}
  ],
  "required_tools": ["string"],
  "safety_level": "Safe|Moderate|Destructive|Critical",
  "confirmation_required": true,
  "validation_rules": [{{ "type": "string", "description": "string", "check_tool": "string|null" }}],
  "expected_result": "string",
  "clarification_needed": "string|null - set this if the prompt is ambiguous"
}}

If you cannot perform the requested operation, explain why in the "expected_result" field and return an empty ordered_steps array.
If the operation is partially supported, explain the limitation and provide the supported subset of steps.
""";
    }

    public static string BuildClassificationPrompt()
    {
        return """
You are an intent classifier for a Civil 3D AI assistant.
Given a user prompt, classify it into exactly one of these categories:
- DRAW: creating new geometry (lines, polylines, circles, arcs, text)
- CIVIL: Civil 3D operations (alignments, profiles, surfaces, feature lines, labels)
- MODIFY: modifying existing entities (move, copy, rotate, erase, change layer, set properties)
- QUERY: querying/analyzing drawing data without modifications
- WORKFLOW: multi-step operations combining multiple categories
- AMBIGUOUS: prompt is too vague to classify

Respond with ONLY the category name, nothing else.
""";
    }
}
