namespace BitcoinDatabaseGenerator
{
    using System.Collections.Concurrent;
    using System.Data;
    using BitcoinDataLayerAdoNet.DataSets;
    using DBData = BitcoinDataLayerAdoNet.Data;
    using ParserData = BitcoinBlockchain.Data;

    /// <summary>
    /// Stores data tables that will be used as the source for the SQL bulk copy operation.
    /// When client code pushes new data into this instance of <see cref="SourceDataPipeline"/>, that data
    /// will be transferred into some buffer data sets. Once a buffer dataset is "full", it is put aside in a list 
    /// of available tables. New data coming will now fill another dataset that when full will be also added 
    /// to the list of available tables and so on.
    /// Client code that requests available data will be fed tables from the list of available tables.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "DataSet instances do not need to be disposed.")]
    public class SourceDataPipeline
    {
        public const int RowsLimit = 12000;

        private readonly ConcurrentQueue<DataTable> availableDataTables;

        private readonly object blockLockObject;
        private readonly object bitcoinTransactionLockObject;
        private readonly object transactionInputLockObject;
        private readonly object transactionOutputLockObject;

        private BlockDataSet blockDataSet;
        private BitcoinTransactionDataSet bitcoinTransactionDataSetBuffer;
        private TransactionInputDataSet transactionInputDataSetBuffer;
        private TransactionOutputDataSet transactionOutputDataSetBuffer;

        public SourceDataPipeline()
        {
            this.blockLockObject = new object();
            this.bitcoinTransactionLockObject = new object();
            this.transactionInputLockObject = new object();
            this.transactionOutputLockObject = new object();

            this.blockDataSet = new BlockDataSet();
            this.bitcoinTransactionDataSetBuffer = new BitcoinTransactionDataSet();
            this.transactionInputDataSetBuffer = new TransactionInputDataSet();
            this.transactionOutputDataSetBuffer = new TransactionOutputDataSet();

            this.availableDataTables = new ConcurrentQueue<DataTable>();
        }

        public bool HasDataSourceAvailable { get; set; }

        public bool TryGetNextAvailableDataSource(out DataTable dataTable)
        {
            return this.availableDataTables.TryDequeue(out dataTable);
        }

        public void FillBlockchainPipeline(int currentBlockchainFileId, ParserData.Block parserBlock, DatabaseIdSegmentManager databaseIdSegmentManager)
        {
            long blockId = databaseIdSegmentManager.GetNextBlockId();

            lock (this.blockLockObject)
            {
                this.blockDataSet.Block.AddBlockRow(
                    blockId,
                    currentBlockchainFileId,
                    (int)parserBlock.BlockHeader.BlockVersion,
                    parserBlock.BlockHeader.BlockHash.ToArray(),
                    parserBlock.BlockHeader.PreviousBlockHash.ToArray(),
                    parserBlock.TransactionsCount,
                    parserBlock.BlockHeader.MerkleRootHash.ToArray(),
                    parserBlock.BlockHeader.BlockTimestamp);

                if (this.MakeDataTableAvailableIfLarge(this.blockDataSet.Block))
                {
                    this.blockDataSet = new BlockDataSet();
                }
            }

            lock (this.bitcoinTransactionLockObject)
            {
                foreach (ParserData.Transaction parserTransaction in parserBlock.Transactions)
                {
                    long bitcoinTransactionId = databaseIdSegmentManager.GetNextTransactionId();

                    this.bitcoinTransactionDataSetBuffer.BitcoinTransaction.AddBitcoinTransactionRow(
                        bitcoinTransactionId,
                        blockId,
                        parserTransaction.TransactionHash.ToArray(),
                        parserTransaction.TransactionInputsCount,
                        parserTransaction.TransactionOutputsCount,
                        (int)parserTransaction.TransactionVersion,
                        (int)parserTransaction.TransactionLockTime,
                        parserTransaction.IsSegWit);
                }

                if (this.MakeDataTableAvailableIfLarge(this.bitcoinTransactionDataSetBuffer.BitcoinTransaction))
                {
                    this.bitcoinTransactionDataSetBuffer = new BitcoinTransactionDataSet();
                }
            }

            lock (this.transactionInputLockObject)
            {
                databaseIdSegmentManager.ResetNextTransactionId();
                foreach (ParserData.Transaction parserTransaction in parserBlock.Transactions)
                {
                    long bitcoinTransactionId = databaseIdSegmentManager.GetNextTransactionId();

                    foreach (ParserData.TransactionInput parserTransactionInput in parserTransaction.Inputs)
                    {
                        long transactionInput = databaseIdSegmentManager.GetNextTransactionInputId();

                        this.transactionInputDataSetBuffer.TransactionInput.AddTransactionInputRow(
                            transactionInput,
                            bitcoinTransactionId,
                            parserTransactionInput.SourceTransactionHash.ToArray(),
                            (int)parserTransactionInput.SourceTransactionOutputIndex,
                            DBData.TransactionInput.SourceTransactionOutputIdUnknown);
                    }

                    if (this.MakeDataTableAvailableIfLarge(this.transactionInputDataSetBuffer.TransactionInput))
                    {
                        this.transactionInputDataSetBuffer = new TransactionInputDataSet();
                    }
                }
            }

            lock (this.transactionOutputLockObject)
            {
                databaseIdSegmentManager.ResetNextTransactionId();
                foreach (ParserData.Transaction parserTransaction in parserBlock.Transactions)
                {
                    long bitcoinTransactionId = databaseIdSegmentManager.GetNextTransactionId();

                    for (int outputIndex = 0; outputIndex < parserTransaction.Outputs.Count; outputIndex++)
                    {
                        ParserData.TransactionOutput parserTransactionOutput = parserTransaction.Outputs[outputIndex];
                        byte[] temp;
                        temp = parserTransactionOutput.OutputAddress.ToArray();
                        string str = string.Empty;
                        for (int i = 0; i < temp.Length; i++)
                            str += (char)temp[i];
                        long transactionOutputId = databaseIdSegmentManager.GetNextTransactionOutputId();
                        this.transactionOutputDataSetBuffer.TransactionOutput.AddTransactionOutputRow(
                            transactionOutputId,
                            bitcoinTransactionId,
                            outputIndex,
                            (decimal)parserTransactionOutput.OutputValueSatoshi / DatabaseGenerator.BtcToSatoshi,
                            parserTransactionOutput.OutputScript.ToArray(),
                            str,
                            DBData.TransactionOutput.OutputAddressIdUnknown);
                    }
                }

                if (this.MakeDataTableAvailableIfLarge(this.transactionOutputDataSetBuffer.TransactionOutput))
                {
                    this.transactionOutputDataSetBuffer = new TransactionOutputDataSet();
                }
            }
        }

        public void Flush()
        {
            this.availableDataTables.Enqueue(this.blockDataSet.Block);
            this.availableDataTables.Enqueue(this.bitcoinTransactionDataSetBuffer.BitcoinTransaction);
            this.availableDataTables.Enqueue(this.transactionInputDataSetBuffer.TransactionInput);
            this.availableDataTables.Enqueue(this.transactionOutputDataSetBuffer.TransactionOutput);
        }

        private bool MakeDataTableAvailableIfLarge(DataTable dataTable)
        {
            if (dataTable.Rows.Count > RowsLimit)
            {
                this.availableDataTables.Enqueue(dataTable);
                return true;
            }

            return false;
        }
    }
}