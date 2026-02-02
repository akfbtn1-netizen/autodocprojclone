#!/bin/bash



# SessionStart: Load memory + validate environment



# Runs at beginning of each session


touch .claude/hook-last-run.txt




echo "=== Session Initialization ==="



echo ""







# 1. Validate required environment variables



MISSING_VARS=()







[ -z "$AZURE_OPENAI_API_KEY" ] && MISSING_VARS+=("AZURE_OPENAI_API_KEY")



[ -z "$AZURE_OPENAI_ENDPOINT" ] && MISSING_VARS+=("AZURE_OPENAI_ENDPOINT")



[ -z "$AZURE_OPENAI_DEPLOYMENT" ] && MISSING_VARS+=("AZURE_OPENAI_DEPLOYMENT")







if [ ${#MISSING_VARS[@]} -gt 0 ]; then



    echo "Warning: Missing environment variables:"



    for var in "${MISSING_VARS[@]}"; do



        echo "   - $var"



    done



    echo ""



fi







# 2. Load working memory



echo "Working Memory:"



echo "---"



if [ -f "$CLAUDE_PROJECT_DIR/.claude/working-memory.md" ]; then



    cat "$CLAUDE_PROJECT_DIR/.claude/working-memory.md"



else



    echo "(No working memory found - use memory_update MCP tool to save context)"



fi



echo "---"



echo ""







# 3. Show current troubleshooting focus



echo "Current Focus Areas:"



echo "   - End-to-end doc pipeline troubleshooting"



echo "   - Frontend-API wiring verification"



echo "   - Database connectivity security"



echo ""







exit 0