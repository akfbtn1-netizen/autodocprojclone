-- Clean up DocumentChanges duplicates
SET QUOTED_IDENTIFIER ON;

-- Show current state
SELECT 'Before cleanup' as Status, COUNT(*) as TotalRows FROM DaQa.DocumentChanges;

-- Create a temp table with unique JIRA numbers (keeping the first occurrence)
SELECT MIN(Id) as KeepId, JiraNumber
INTO #KeepRows
FROM DaQa.DocumentChanges
GROUP BY JiraNumber;

-- Delete all rows except those we want to keep
DELETE FROM DaQa.DocumentChanges 
WHERE Id NOT IN (SELECT KeepId FROM #KeepRows);

-- Clean up temp table
DROP TABLE #KeepRows;

-- Show results
SELECT 'After cleanup' as Status, COUNT(*) as TotalRows FROM DaQa.DocumentChanges;