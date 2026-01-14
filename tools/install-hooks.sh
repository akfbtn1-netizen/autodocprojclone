#!/bin/bash
# Git pre-commit hook installation script
# Sets up automatic quality gate validation

HOOK_DIR=".git/hooks"
HOOK_FILE="$HOOK_DIR/pre-commit"

echo "🔧 Installing Enterprise Quality Gate Pre-Commit Hook..."

# Create hooks directory if it doesn't exist
mkdir -p "$HOOK_DIR"

# Create pre-commit hook
cat > "$HOOK_FILE" << 'EOF'
#!/bin/bash
# Enterprise Quality Gate Pre-Commit Hook
# Automatically validates code quality before commits

echo "🔍 Running Enterprise Quality Gate..."

# Check if PowerShell is available
if command -v pwsh >/dev/null 2>&1; then
    pwsh -File "./tools/quality-gate.ps1" -FailOnViolations
    exit_code=$?
elif command -v powershell >/dev/null 2>&1; then
    powershell -File "./tools/quality-gate.ps1" -FailOnViolations
    exit_code=$?
else
    echo "⚠️ PowerShell not found. Skipping quality gate validation."
    echo "Please install PowerShell or run quality checks manually."
    exit_code=0
fi

if [ $exit_code -ne 0 ]; then
    echo ""
    echo "❌ COMMIT BLOCKED by Quality Gate"
    echo "Fix the violations above and try again."
    echo ""
    echo "💡 To bypass (not recommended): git commit --no-verify"
    exit 1
fi

echo "✅ Quality Gate passed! Proceeding with commit..."
exit 0
EOF

# Make hook executable
chmod +x "$HOOK_FILE"

echo "✅ Pre-commit hook installed successfully!"
echo ""
echo "🎯 Quality Gate Features:"
echo "  • Complexity validation (≤6)"
echo "  • Method length checking (≤20 lines)"  
echo "  • Class size limits (≤200 lines)"
echo "  • Documentation requirements"
echo "  • Automatic enforcement on commits"
echo ""
echo "🚀 Your repository now has automated quality enforcement!"