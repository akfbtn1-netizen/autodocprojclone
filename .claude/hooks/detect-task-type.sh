#!/bin/bash
# UserPromptSubmit: Detect task type and suggest MCP prompts
# Input: $CLAUDE_USER_PROMPT

PROMPT="$CLAUDE_USER_PROMPT"
PROMPT_LOWER=$(echo "$PROMPT" | tr '[:upper:]' '[:lower:]')

SUGGESTIONS=()

# Detect frontend work
if echo "$PROMPT_LOWER" | grep -qE "react|component|frontend|ui|css|tailwind|zustand|vite"; then
    SUGGESTIONS+=("frontend-specialist")
fi

# Detect API/integration work
if echo "$PROMPT_LOWER" | grep -qE "api|endpoint|controller|rest|fetch|axios|signalr|wiring"; then
    SUGGESTIONS+=("api-integration-specialist")
fi

# Detect testing work
if echo "$PROMPT_LOWER" | grep -qE "test|e2e|playwright|cypress|xunit|coverage"; then
    SUGGESTIONS+=("e2e-testing-specialist")
fi

# Detect agent/architecture work
if echo "$PROMPT_LOWER" | grep -qE "agent|mcp|orchestrat|workflow|saga|multi-agent"; then
    SUGGESTIONS+=("agent-architect")
fi

# Detect database work
if echo "$PROMPT_LOWER" | grep -qE "database|sql|schema|migration|dapper|ef core|governance"; then
    echo "Database work detected - remember to use DataGovernanceProxy"
fi

# Output suggestions if any
if [ ${#SUGGESTIONS[@]} -gt 0 ]; then
    echo ""
    echo "Suggested MCP prompts for this task:"
    for prompt in "${SUGGESTIONS[@]}"; do
        echo "   -> $prompt"
    done
    echo ""
fi

exit 0
