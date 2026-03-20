namespace Civil3DAIAddon.Models.AI;

public static class AIPlanJsonSchema
{
    public static object GetSchema()
    {
        return new
        {
            type = "object",
            required = new[] { "intent", "assumptions", "target_objects", "ordered_steps", "required_tools", "safety_level", "confirmation_required", "validation_rules", "expected_result" },
            properties = new
            {
                intent = new { type = "string", description = "What the user wants to achieve" },
                assumptions = new { type = "array", items = new { type = "string" } },
                target_objects = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            handle = new { type = new[] { "string", "null" } },
                            type = new { type = "string" },
                            layer = new { type = new[] { "string", "null" } },
                            description = new { type = new[] { "string", "null" } }
                        }
                    }
                },
                ordered_steps = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        required = new[] { "step_number", "tool_name", "parameters", "description" },
                        properties = new
                        {
                            step_number = new { type = "integer" },
                            tool_name = new { type = "string" },
                            parameters = new { type = "object" },
                            description = new { type = "string" },
                            depends_on = new { type = "array", items = new { type = "integer" } },
                            rollback_tool = new { type = new[] { "string", "null" } }
                        }
                    }
                },
                required_tools = new { type = "array", items = new { type = "string" } },
                safety_level = new { type = "string", @enum = new[] { "Safe", "Moderate", "Destructive", "Critical" } },
                confirmation_required = new { type = "boolean" },
                validation_rules = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            type = new { type = "string" },
                            description = new { type = "string" },
                            check_tool = new { type = new[] { "string", "null" } }
                        }
                    }
                },
                expected_result = new { type = "string" },
                clarification_needed = new { type = new[] { "string", "null" } }
            }
        };
    }
}
