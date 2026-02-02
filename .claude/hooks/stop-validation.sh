#!/bin/bash
# Stop: Build validation + API check + test reminder
# Exit 2 = force Claude to fix issues

cd "$CLAUDE_PROJECT_DIR" || exit 0

echo ""
echo "=== End of Turn Validation ==="

# 1. Quick .NET build check
echo "Build check..."
if ! dotnet build --no-restore --verbosity quiet 2>&1 | tail -5; then
    echo "" >&2
    echo "Build failed - fix errors before continuing" >&2
    echo "   Use 'build_dotnet' MCP tool for detailed output" >&2
    exit 2
fi
echo "   Build passed"

# 2. Check if controller files were modified (API check reminder)
MODIFIED_CONTROLLERS=$(git diff --name-only HEAD 2>/dev/null | grep -E "Controller\.cs$" || true)
if [ -n "$MODIFIED_CONTROLLERS" ]; then
    echo ""
    echo "API controllers modified:"
    echo "$MODIFIED_CONTROLLERS" | sed 's/^/   /'
    echo "   -> Consider using 'api_list_endpoints' MCP tool to verify routes"
fi

# 3. Check if .cs files were modified (test reminder)
MODIFIED_CS=$(git diff --name-only HEAD 2>/dev/null | grep -E "\.cs$" | grep -v "Test" || true)
if [ -n "$MODIFIED_CS" ]; then
    echo ""
    echo "C# files modified - consider running tests:"
    echo "   -> Use 'test_dotnet' MCP tool"
    echo "   -> Or: dotnet test --filter Category=Unit"
fi

# 4. Check if frontend files were modified
MODIFIED_TS=$(git diff --name-only HEAD 2>/dev/null | grep -E "\.(ts|tsx)$" || true)
if [ -n "$MODIFIED_TS" ]; then
    echo ""
    echo "Frontend files modified:"
    echo "   -> Verify API wiring with 'api-integration-specialist' prompt"
    echo "   -> Check component renders correctly"
fi

echo ""
echo "Validation complete"
exit 0
