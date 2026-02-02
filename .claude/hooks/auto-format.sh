#!/bin/bash
# PostToolUse: Auto-format changed files
# Must complete in <1 second

FILE_PATH="$CLAUDE_FILE_PATH"

# Skip if no file path
[ -z "$FILE_PATH" ] && exit 0

# Format based on file type
case "$FILE_PATH" in
    *.cs)
        # Quick .NET format (single file)
        dotnet format --include "$FILE_PATH" --verbosity quiet 2>/dev/null || true
        ;;
    *.ts|*.tsx|*.js|*.jsx)
        # Prettier format
        npx prettier --write "$FILE_PATH" 2>/dev/null || true
        ;;
    *.json)
        # Prettier for JSON
        npx prettier --write "$FILE_PATH" 2>/dev/null || true
        ;;
    *.css|*.scss)
        # Prettier for styles
        npx prettier --write "$FILE_PATH" 2>/dev/null || true
        ;;
esac

exit 0
