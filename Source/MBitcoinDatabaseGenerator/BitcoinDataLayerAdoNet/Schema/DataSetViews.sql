--=============================================================================
-- This file contains SQL views that are added to the database in a 
-- production environment when the database is created.
--=============================================================================

-- START SECTION
--=============================================================================
-- VIEW View_IndexInfo
-- Use this view to regenerate the typed dataset IndexInfoDataSet.xsd
--=============================================================================
CREATE VIEW View_IndexInfo AS
SELECT TOP 1
    sys.indexes.name AS IndexName,
    sys.tables.name as TableName
FROM sys.indexes
INNER JOIN sys.tables ON sys.tables.object_id = sys.indexes.object_id
WHERE sys.indexes.type_desc = 'NONCLUSTERED'
GO

-- START SECTION
--=============================================================================
-- VIEW View_SummaryBlock
-- Use this view to regenerate the typed dataset SummaryBlockDataSet.xsd
--=============================================================================
CREATE VIEW View_SummaryBlock AS SELECT BlockId, BlockHash, PreviousBlockHash FROM Block
GO