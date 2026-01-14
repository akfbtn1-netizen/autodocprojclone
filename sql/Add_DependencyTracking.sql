-- Enhanced dependency tracking and impact analysis queries

-- Add to the DocumentVersionHistory SQL script:

-- Create dependency impact tracking table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'DaQa.StoredProcedureDependencies') AND type = 'U')
BEGIN
    CREATE TABLE DaQa.StoredProcedureDependencies (
        DependencyId INT IDENTITY(1,1) PRIMARY KEY,
        IndexID NVARCHAR(50) NOT NULL, -- References MasterIndex
        DependencyType NVARCHAR(20) NOT NULL, -- 'Table', 'Procedure', 'Function', 'View'
        DependencyName NVARCHAR(255) NOT NULL,
        DependencySchema NVARCHAR(128) NOT NULL DEFAULT 'dbo',
        IsCritical BIT NOT NULL DEFAULT 0, -- Critical dependencies that would break the procedure
        LastVerified DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        
        INDEX IX_Dependencies_IndexID (IndexID),
        INDEX IX_Dependencies_Name (DependencyName, DependencySchema),
        UNIQUE INDEX UX_Dependencies (IndexID, DependencyType, DependencyName, DependencySchema)
    );
    
    PRINT 'StoredProcedureDependencies table created for impact analysis';
END

-- Create impact analysis view
IF NOT EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'DaQa.vw_DependencyImpactAnalysis'))
BEGIN
    EXEC('
    CREATE VIEW DaQa.vw_DependencyImpactAnalysis
    AS
    SELECT 
        dep.DependencyName + ''.'' + dep.DependencySchema as FullDependencyName,
        dep.DependencyType,
        COUNT(*) as UsageCount,
        STRING_AGG(mi.DocumentTitle, '', '') as AffectedProcedures,
        MAX(dep.LastVerified) as LastVerified,
        CASE WHEN SUM(CAST(dep.IsCritical as INT)) > 0 THEN 1 ELSE 0 END as HasCriticalUsage
    FROM DaQa.StoredProcedureDependencies dep
    INNER JOIN DaQa.MasterIndex mi ON dep.IndexID = mi.IndexID
    WHERE mi.Status = ''Active''
    GROUP BY dep.DependencyName, dep.DependencySchema, dep.DependencyType
    ');
    
    PRINT 'Dependency impact analysis view created';
END