#!/bin/bash
# Bash script to verify all integration test fixes are applied
# Run this from the project root directory

echo "=== Integration Test Fixes Verification ==="
echo ""

all_passed=true

# Check CustomWebApplicationFactory.cs
echo "1. Checking CustomWebApplicationFactory.cs..."
factory_file="tests/Integration/CustomWebApplicationFactory.cs"

if [ -f "$factory_file" ]; then
    # Check 1: SecurityClearanceLevel (not SecurityClearance)
    if grep -q "SecurityClearanceLevel\.Confidential" "$factory_file" && \
       grep -q "SecurityClearanceLevel\.Restricted" "$factory_file"; then
        echo "   ✓ Uses SecurityClearanceLevel enum (correct)"
    else
        echo "   ✗ Missing SecurityClearanceLevel enum usage"
        all_passed=false
    fi

    # Check 2: UserRole.Administrator (not UserRole.Admin)
    if grep -q "UserRole\.Administrator" "$factory_file"; then
        echo "   ✓ Uses UserRole.Administrator (correct)"
    else
        echo "   ✗ Missing UserRole.Administrator"
        all_passed=false
    fi

    # Check 3: UserRole.Manager (not UserRole.DocumentEditor)
    if grep -q "UserRole\.Manager" "$factory_file"; then
        echo "   ✓ Uses UserRole.Manager (correct)"
    else
        echo "   ✗ Missing UserRole.Manager"
        all_passed=false
    fi

    # Check 4: UserRole.Reader (not UserRole.DocumentViewer)
    if grep -q "UserRole\.Reader" "$factory_file"; then
        echo "   ✓ Uses UserRole.Reader (correct)"
    else
        echo "   ✗ Missing UserRole.Reader"
        all_passed=false
    fi

    # Check 5: new User(...) constructor (not User.Create)
    if grep -q "new User\s*(" "$factory_file" && ! grep -q "User\.Create\s*(" "$factory_file"; then
        echo "   ✓ Uses new User(...) constructor (correct)"
    else
        echo "   ✗ Still using User.Create() or missing new User()"
        all_passed=false
    fi

else
    echo "   ✗ File not found: $factory_file"
    all_passed=false
fi

echo ""

# Check IntegrationTestHelpers.cs
echo "2. Checking IntegrationTestHelpers.cs..."
helpers_file="tests/Integration/Helpers/IntegrationTestHelpers.cs"

if [ -f "$helpers_file" ]; then
    # Check 6: SetDocumentVersionUnderReviewAsync method exists
    if grep -q "SetDocumentVersionUnderReviewAsync" "$helpers_file"; then
        echo "   ✓ SetDocumentVersionUnderReviewAsync method exists"
    else
        echo "   ✗ Missing SetDocumentVersionUnderReviewAsync method"
        all_passed=false
    fi

else
    echo "   ✗ File not found: $helpers_file"
    all_passed=false
fi

echo ""
echo "=== Verification Complete ==="

if [ "$all_passed" = true ]; then
    echo "✓ All fixes have been applied successfully!"
    echo ""
    echo "Next steps:"
    echo "1. Run: dotnet build"
    echo "2. Run: dotnet test"
    exit 0
else
    echo "✗ Some fixes are missing. Please review the errors above."
    exit 1
fi
