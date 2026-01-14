#!/bin/bash
# PreToolUse: Block dangerous bash commands
# Exit 2 = block and show message

COMMAND="$CLAUDE_BASH_COMMAND"

# Blocked patterns
DANGEROUS_PATTERNS=(
    "rm -rf /"
    "rm -rf /\*"
    ":\(\)\{ :\|:& \};"
    "> /dev/sda"
    "mkfs\."
    "dd if=/dev"
    "chmod -R 777 /"
    "git push.*--force.*main"
    "git push.*--force.*master"
    "DROP DATABASE"
    "DELETE FROM.*WHERE 1=1"
    "TRUNCATE TABLE"
    "format c:"
)

for pattern in "${DANGEROUS_PATTERNS[@]}"; do
    if echo "$COMMAND" | grep -qiE "$pattern"; then
        echo "BLOCKED: Dangerous command pattern detected" >&2
        echo "   Pattern: $pattern" >&2
        exit 2
    fi
done

# Allow command
exit 0
