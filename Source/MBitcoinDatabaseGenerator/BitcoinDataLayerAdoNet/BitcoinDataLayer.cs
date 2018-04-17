namespace BitcoinDataLayerAdoNet
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using AdoNetHelpers;
    using BitcoinDataLayerAdoNet.Data;
    using BitcoinDataLayerAdoNet.DataSets;
    using ZeroHelpers;

    public class BitcoinDataLayer : IDisposable
    {
        /// <summary>
        /// The timeout in seconds that is used for SQL commands that 
        /// are expected to take a very long time.
        /// </summary>
        public const int ExtendedDbCommandTimeout = 2*7200;

        /// <summary>
        /// The default timeout in seconds that is used for each SQL command.
        /// </summary>
        public const int DefaultDbCommandTimeout = 2*3600;

        private readonly SqlConnection sqlConnection;
        private readonly AdoNetLayer adoNetLayer;

        public BitcoinDataLayer(string connectionString, int commandTimeout = DefaultDbCommandTimeout)
        {
            this.sqlConnection = new SqlConnection(connectionString);
            this.sqlConnection.Open();

            this.adoNetLayer = new AdoNetLayer(this.sqlConnection, commandTimeout);
        }

        /// <summary>
        /// Implements the IDisposable pattern.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.sqlConnection.Dispose();
            }
        }
        
        public string GetLastKnownBlockchainFileName()
        {
            return AdoNetLayer.ConvertDbValue<string>(this.adoNetLayer.ExecuteScalar("SELECT TOP 1 BlockchainFileName FROM BlockchainFile ORDER BY BlockchainFileId DESC"));
        }

        public async Task DeleteLastBlockchainFileAsync()
        {
            const string deleteFromTransactionOutput = @"
                DELETE TransactionOutput FROM TransactionOutput
                INNER JOIN BitcoinTransaction ON BitcoinTransaction.BitcoinTransactionId = TransactionOutput.BitcoinTransactionId
                INNER JOIN Block ON Block.BlockId = BitcoinTransaction.BlockId
                WHERE Block.BlockchainFileId >= @MaxBlockchainFileId";

            const string deleteFromTransactionInput = @"
                DELETE TransactionInput FROM TransactionInput
                INNER JOIN BitcoinTransaction ON BitcoinTransaction.BitcoinTransactionId = TransactionInput.BitcoinTransactionId
                INNER JOIN Block ON Block.BlockId = BitcoinTransaction.BlockId
                WHERE Block.BlockchainFileId >= @MaxBlockchainFileId";

            const string deleteFromBitcoinTransaction = @"
                DELETE BitcoinTransaction FROM BitcoinTransaction
                INNER JOIN Block ON Block.BlockId = BitcoinTransaction.BlockId
                WHERE Block.BlockchainFileId >= @MaxBlockchainFileId";

            const string deleteFromBlock = @" DELETE Block FROM Block WHERE Block.BlockchainFileId >= @MaxBlockchainFileId";

            const string deleteFromBlockchainFile = "DELETE FROM BlockchainFile WHERE BlockchainFile.BlockchainFileId >= @MaxBlockchainFileId";

            int lastBlockchainFileId = AdoNetLayer.ConvertDbValue<int>(this.adoNetLayer.ExecuteScalar("SELECT MAX(BlockchainFileId) from BlockchainFile"));

            await this.adoNetLayer.ExecuteStatementNoResultAsync(deleteFromTransactionOutput, AdoNetLayer.CreateInputParameter("@MaxBlockchainFileId", SqlDbType.Int, lastBlockchainFileId));
            await this.adoNetLayer.ExecuteStatementNoResultAsync(deleteFromTransactionInput, AdoNetLayer.CreateInputParameter("@MaxBlockchainFileId", SqlDbType.Int, lastBlockchainFileId));
            await this.adoNetLayer.ExecuteStatementNoResultAsync(deleteFromBitcoinTransaction, AdoNetLayer.CreateInputParameter("@MaxBlockchainFileId", SqlDbType.Int, lastBlockchainFileId));
            await this.adoNetLayer.ExecuteStatementNoResultAsync(deleteFromBlock, AdoNetLayer.CreateInputParameter("@MaxBlockchainFileId", SqlDbType.Int, lastBlockchainFileId));
            await this.adoNetLayer.ExecuteStatementNoResultAsync(deleteFromBlockchainFile, AdoNetLayer.CreateInputParameter("@MaxBlockchainFileId", SqlDbType.Int, lastBlockchainFileId));
        }

        public long GetTransactionSourceRowsToUpdate()
        {
            const string sqlCountRowsToUpdateCommand = @"SELECT COUNT(1) FROM TransactionInput WHERE SourceTransactionId = -1";
            return AdoNetLayer.ConvertDbValue<long>(this.adoNetLayer.ExecuteScalar(sqlCountRowsToUpdateCommand));
        }

        public long UpdateNullTransactionSources()
        {
            const string sqlUpdateNullSourceTransactionIdCommand = @"
                UPDATE TransactionInput SET SourceTransactionId = NULL
                FROM TransactionInput
                WHERE SourceTransactionId = -1 AND SourceTransactionOutputIndex = -1";
            return this.adoNetLayer.ExecuteStatementNoResult(sqlUpdateNullSourceTransactionIdCommand);
        }

        public int UpdateTransactionSourceBatch(long batchSize)
        {
            const string formattedStatement = @"
                UPDATE TransactionInput
                SET SourceTransactionId = BitcoinTransaction.BitcoinTransactionId
                FROM TransactionInput
                INNER JOIN BitcoinTransaction ON
                    BitcoinTransaction.TransactionHash = TransactionInput.SourceTransactionHash
                INNER JOIN (
                    SELECT TOP {0}
                        TransactionInput.TransactionInputId
                    FROM TransactionInput
                    WHERE TransactionInput.SourceTransactionId = -1 AND 
                        TransactionInput.SourceTransactionOutputIndex != -1
                    ) AS T1 ON T1.TransactionInputId = TransactionInput.TransactionInputId";

            string sqlUpdateSourceTransactionIdCommand = string.Format(CultureInfo.InvariantCulture, formattedStatement, batchSize);
            return this.adoNetLayer.ExecuteStatementNoResult(sqlUpdateSourceTransactionIdCommand);
        }

        public void FixupTransactionSourceIdForDuplicateTransactionHash()
        {
            const string sqlUpdateSourceTransactionIdCommand = @"
                UPDATE TransactionInput
                SET SourceTransactionId = (
                    SELECT TOP 1 BitcoinTransaction.BitcoinTransactionId
                    FROM BitcoinTransaction
                    INNER JOIN TransactionInput ON TransactionInput.SourceTransactionHash = BitcoinTransaction.TransactionHash
                    WHERE
                        TransactionInput.SourceTransactionOutputIndex != -1
                        AND BitcoinTransaction.BitcoinTransactionId < TransactionInput.BitcoinTransactionId
                    ORDER BY BitcoinTransaction.BitcoinTransactionId DESC
                )
                FROM TransactionInput
                WHERE
                    TransactionInput.SourceTransactionHash IN (
                        SELECT TransactionHash
                        FROM BitcoinTransaction
                        GROUP BY TransactionHash
                        HAVING COUNT(1) > 1)";
            this.adoNetLayer.ExecuteStatementNoResult(sqlUpdateSourceTransactionIdCommand);
        }

        public void GetMaximumIdValues(out int blockchainFileId, out long blockId, out long bitcoinTransactionId, out long transactionInputId, out long transactionOutputId)
        {
            blockchainFileId = AdoNetLayer.ConvertDbValue<int>(this.adoNetLayer.ExecuteScalar("SELECT MAX(BlockchainFileId) from BlockchainFile"), -1);
            blockId = AdoNetLayer.ConvertDbValue<long>(this.adoNetLayer.ExecuteScalar("SELECT MAX(BlockId) from Block"), -1);
            bitcoinTransactionId = AdoNetLayer.ConvertDbValue<long>(this.adoNetLayer.ExecuteScalar("SELECT MAX(BitcoinTransactionId) from BitcoinTransaction"), -1);
            transactionInputId = AdoNetLayer.ConvertDbValue<long>(this.adoNetLayer.ExecuteScalar("SELECT MAX(TransactionInputId) from TransactionInput"), -1);
            transactionOutputId = AdoNetLayer.ConvertDbValue<long>(this.adoNetLayer.ExecuteScalar("SELECT MAX(TransactionOutputId) from TransactionOutput"), -1);
        }

        public void GetDatabaseEntitiesCount(out int blockchainFileCount, out int blockCount, out int transactionCount, out int transactionInputCount, out int transactionOutputCount)
        {
            blockchainFileCount = AdoNetLayer.ConvertDbValue<int>(this.adoNetLayer.ExecuteScalar("SELECT COUNT(1) from BlockchainFile"));
            blockCount = AdoNetLayer.ConvertDbValue<int>(this.adoNetLayer.ExecuteScalar("SELECT COUNT(1)  from Block"));
            transactionCount = AdoNetLayer.ConvertDbValue<int>(this.adoNetLayer.ExecuteScalar("SELECT COUNT(1)  from BitcoinTransaction"));
            transactionInputCount = AdoNetLayer.ConvertDbValue<int>(this.adoNetLayer.ExecuteScalar("SELECT COUNT(1) from TransactionInput"));
            transactionOutputCount = AdoNetLayer.ConvertDbValue<int>(this.adoNetLayer.ExecuteScalar("SELECT COUNT(1)  from TransactionOutput"));
        }

        public void AddBlockchainFile(BlockchainFile blockchainFile)
        {
            this.adoNetLayer.ExecuteStatementNoResult(
                "INSERT INTO BlockchainFile(BlockchainFileId, BlockchainFileName) VALUES (@BlockchainFileId, @BlockchainFileName)",
                AdoNetLayer.CreateInputParameter("@BlockchainFileId", SqlDbType.Int, blockchainFile.BlockchainFileId),
                AdoNetLayer.CreateInputParameter("@BlockchainFileName", SqlDbType.NVarChar, blockchainFile.BlockchainFileName));
        }

        public void BulkCopyTable(DataTable dataTable)
        {
            this.adoNetLayer.BulkCopyTable(dataTable.TableName, dataTable, DefaultDbCommandTimeout);
        }

        public SummaryBlockDataSet GetSummaryBlockDataSet()
        {
            return this.GetDataSet<SummaryBlockDataSet>("SELECT BlockId, BlockHash, PreviousBlockHash FROM Block");
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "DataSet instances do not have to be disposed.")]
        private T GetDataSet<T>(string sqlCommandText, params SqlParameter[] sqlParameters) where T : DataSet, new()
        {
            T dataSet = new T() { Locale = CultureInfo.InvariantCulture };
            this.adoNetLayer.FillDataSetFromStatement(dataSet, sqlCommandText, sqlParameters);
            return dataSet;
        }

        public void DeleteBlocks(IEnumerable<long> blocksToDelete)
        {
            foreach (IEnumerable<long> batch in blocksToDelete.GetBatches(100))
            {
                this.DeleteBatchOfBlocks(batch);
            }
        }

        private void DeleteBatchOfBlocks(IEnumerable<long> batchOfBlockIds)
        {
            string inClause = "(" + string.Join(", ", batchOfBlockIds) + ")";

            this.adoNetLayer.ExecuteStatementNoResult(@"
                DELETE TransactionOutput FROM TransactionOutput
                INNER JOIN BitcoinTransaction ON BitcoinTransaction.BitcoinTransactionId = TransactionOutput.BitcoinTransactionId
                INNER JOIN Block ON Block.BlockId = BitcoinTransaction.BlockId
                WHERE Block.BlockId IN " + inClause);

            this.adoNetLayer.ExecuteStatementNoResult(@"
                DELETE TransactionInput FROM TransactionInput
                INNER JOIN BitcoinTransaction ON BitcoinTransaction.BitcoinTransactionId = TransactionInput.BitcoinTransactionId
                INNER JOIN Block ON Block.BlockId = BitcoinTransaction.BlockId
                WHERE Block.BlockId IN " + inClause);

            this.adoNetLayer.ExecuteStatementNoResult(@"
                DELETE BitcoinTransaction FROM BitcoinTransaction
                INNER JOIN Block ON Block.BlockId = BitcoinTransaction.BlockId
                WHERE Block.BlockId IN " + inClause);

            this.adoNetLayer.ExecuteStatementNoResult(@"DELETE FROM Block WHERE Block.BlockId IN " + inClause);
        }

        /// <summary>
        /// Applied after a series of blocks were deleted. 
        /// This method will update the block IDs so that they are forming a consecutive sequence.
        /// </summary>
        /// <param name="blocksDeleted">
        /// The list of IDs for blocks that were deleted.
        /// </param>
        public void CompactBlockIds(IEnumerable<long> blocksDeleted)
        {
            const string sqlCommandUpdateBlockBlockIdSection = @"UPDATE Block SET BlockId = BlockId - @DecrementAmount WHERE BlockId BETWEEN @BlockId1 AND @BlockId2";
            const string sqlCommandUpdateTransactionBlockIdSection = @"UPDATE BitcoinTransaction SET BlockId = BlockId - @DecrementAmount WHERE BlockId BETWEEN @BlockId1 AND @BlockId2";

            const string sqlCommandUpdateBlockBlockIdLastSection = @"UPDATE Block SET BlockId = BlockId - @DecrementAmount WHERE BlockId > @BlockId";
            const string sqlCommandUpdateTransactionBlockIdLastSection = @"UPDATE BitcoinTransaction SET BlockId = BlockId - @DecrementAmount WHERE BlockId > @BlockId";

            List<long> orderedBlocksDeleted = blocksDeleted.OrderBy(id => id).ToList();
            int decrementAmount = 1;

            for (int i = 0; i < orderedBlocksDeleted.Count - 1; i++)
            {
                long blockId1 = orderedBlocksDeleted[i];
                long blockId2 = orderedBlocksDeleted[i + 1];

                this.adoNetLayer.ExecuteStatementNoResult(
                    sqlCommandUpdateBlockBlockIdSection,
                    AdoNetLayer.CreateInputParameter("@DecrementAmount", SqlDbType.Int, decrementAmount),
                    AdoNetLayer.CreateInputParameter("@BlockId1", SqlDbType.BigInt, blockId1),
                    AdoNetLayer.CreateInputParameter("@BlockId2", SqlDbType.BigInt, blockId2));

                this.adoNetLayer.ExecuteStatementNoResult(
                    sqlCommandUpdateTransactionBlockIdSection,
                    AdoNetLayer.CreateInputParameter("@DecrementAmount", SqlDbType.Int, decrementAmount),
                    AdoNetLayer.CreateInputParameter("@BlockId1", SqlDbType.BigInt, blockId1),
                    AdoNetLayer.CreateInputParameter("@BlockId2", SqlDbType.BigInt, blockId2));

                decrementAmount++;
            }

            long blockId = orderedBlocksDeleted[orderedBlocksDeleted.Count - 1];

            this.adoNetLayer.ExecuteStatementNoResult(
                sqlCommandUpdateBlockBlockIdLastSection,
                AdoNetLayer.CreateInputParameter("@DecrementAmount", SqlDbType.BigInt, decrementAmount),
                AdoNetLayer.CreateInputParameter("@BlockId", SqlDbType.BigInt, blockId));

            this.adoNetLayer.ExecuteStatementNoResult(
                sqlCommandUpdateTransactionBlockIdLastSection,
                AdoNetLayer.CreateInputParameter("@DecrementAmount", SqlDbType.BigInt, decrementAmount),
                AdoNetLayer.CreateInputParameter("@BlockId", SqlDbType.BigInt, blockId));
        }

        private IndexInfoDataSet GetAllHeavyIndexes()
        {
            return this.GetDataSet<IndexInfoDataSet>(@"
                SELECT 
                    sys.tables.name as TableName, 
                    sys.indexes.name AS IndexName 
                FROM sys.indexes
                INNER JOIN sys.tables ON sys.tables.object_id = sys.indexes.object_id
                WHERE 
                    sys.indexes.type_desc = 'NONCLUSTERED'
                    AND sys.tables.name != 'BlockchainFile'");
        }

        public void DisableAllHeavyIndexes()
        {
            foreach (IndexInfoDataSet.IndexInfoRow indexInfoRow in this.GetAllHeavyIndexes().IndexInfo)
            {
                string disableIndexStatement = string.Format(
                    CultureInfo.InvariantCulture,
                    "ALTER INDEX {0} ON {1} DISABLE",
                    indexInfoRow.IndexName,
                    indexInfoRow.TableName);

                this.adoNetLayer.ExecuteStatementNoResult(disableIndexStatement);
            }
        }

        public void RebuildAllHeavyIndexes(Action onSectionExecuted)
        {
            foreach (IndexInfoDataSet.IndexInfoRow indexInfoRow in this.GetAllHeavyIndexes().IndexInfo)
            {
                if (onSectionExecuted != null)
                {
                    onSectionExecuted();
                }

                string disableIndexStatement = string.Format(
                    CultureInfo.InvariantCulture,
                    "ALTER INDEX {0} ON {1} REBUILD",
                    indexInfoRow.IndexName,
                    indexInfoRow.TableName);

                this.adoNetLayer.ExecuteStatementNoResult(disableIndexStatement);
            }
        }

        public void ShrinkDatabase(string databaseName)
        {
            string shrinkStatement = string.Format(CultureInfo.InvariantCulture, "DBCC SHRINKDATABASE ([{0}])", databaseName);
            this.adoNetLayer.ExecuteStatementNoResult(shrinkStatement);
        }

        public bool IsDatabaseEmpty()
        {
            return AdoNetLayer.ConvertDbValue<int>(this.adoNetLayer.ExecuteScalar("SELECT CASE WHEN EXISTS (SELECT 1 FROM Block) THEN 0 ELSE 1 END AS IsEmpty")) == 1;
        }

        public bool IsSchemaSetup()
        {
            return AdoNetLayer.ConvertDbValue<int>(this.adoNetLayer.ExecuteScalar(
                "SELECT CASE WHEN EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'BtcDbSettings') THEN 1 ELSE 0 END AS IsSchemaSetup")) == 1;
        }

        public void UpdateTransactionOutputAddressId()
        {
            const string sqlGetFirstTXOutUnsetAddressId = @"
                SELECT TOP 1 TransactionOutputId
                FROM TransactionOutput
                WHERE TransactionOutputId > @lastTransactionOutputId
                AND OutputAddressId = -1
                AND OutputAddress != '0'";

            const string sqlUpdateAddressId = @"
                UPDATE TransactionOutput
                SET OutputAddressId = @mOutputAddressId
                WHERE TransactionOutputId >= @currentTransactionOutputId
                AND OutputAddress = (
                    SELECT OutputAddress
                    FROM TransactionOutput
                    WHERE TransactionOutputId = @currentTransactionOutputId)";

            long OutputAddressId = 0;
            long MaxTransactionOutputId = AdoNetLayer.ConvertDbValue<long>(
                this.adoNetLayer.ExecuteScalar(
                    @"select TOP 1 TransactionOutputId
                      from TransactionOutput
                      order by TransactionOutputId desc"));
            
            long TransactionOutputCount = -1;
            while(TransactionOutputCount < MaxTransactionOutputId)
            {
                TransactionOutputCount = AdoNetLayer.ConvertDbValue<long>(this.adoNetLayer.ExecuteScalar(sqlGetFirstTXOutUnsetAddressId,
                    AdoNetLayer.CreateInputParameter("@lastTransactionOutputId", SqlDbType.BigInt, TransactionOutputCount)));

                this.adoNetLayer.ExecuteStatementNoResult(sqlUpdateAddressId,
                    AdoNetLayer.CreateInputParameter("mOutputAddressId", SqlDbType.BigInt, OutputAddressId),
                    AdoNetLayer.CreateInputParameter("currentTransactionOutputId", SqlDbType.BigInt, TransactionOutputCount));
                OutputAddressId++;
            }    
        }
    }
}