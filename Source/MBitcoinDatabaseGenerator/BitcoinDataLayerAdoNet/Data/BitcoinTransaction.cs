namespace BitcoinDataLayerAdoNet.Data
{
    using BitcoinBlockchain.Data;

    ///<summary>
    ///Contains information about a Bitcoin transaction as saved in the Bitcoin SQL Database 
    /// </summary>
    public class BitcoinTransaction
    {
        public long BitcoinTransactionId { get; private set; }
        public long BlockId { get; private set; }
        public ByteArray TransactionHash { get; private set; }
        public int TransactionInputsCount { get; private set; }
        public int TransactionOutputsCount { get; private set; }
        public int TransactionVersion { get; private set; }
        public int TransactionLockTime { get; private set; }
        public bool IsSegWit { get; private set; }

        public BitcoinTransaction(
            long bitcoinTransactionId,
            long blockId,
            ByteArray transactionHash,
            int transactionInputsCount,
            int transactionOutputsCount,
            int transactionVersion,
            int transactionLockTime,
            bool isSegWit)
        {
            this.BitcoinTransactionId = bitcoinTransactionId;
            this.BlockId = blockId;
            this.TransactionHash = transactionHash;
            this.TransactionInputsCount = transactionInputsCount;
            this.TransactionOutputsCount = transactionOutputsCount;
            this.TransactionVersion = transactionVersion;
            this.TransactionLockTime = transactionLockTime;
            this.IsSegWit = isSegWit;
        } 

    }
}