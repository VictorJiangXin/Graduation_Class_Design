namespace BitcoinDataLayerAdoNet.Data
{
    using BitcoinBlockchain.Data;

    /// <summary>
    /// Contains information about a Bitcoin transaction input as saved in the Bitcoin SQL database.
    /// </summary>
    public class TransactionInput
    {
        public const int SourceTransactionOutputIdUnknown = -1;

        public long TransactionInputId { get; private set; }
        public long BitcoinTransactionId { get; private set; }
        public ByteArray SourceTransactionHash { get; private set; }
        public int SourceTransactionOutputIndex { get; private set; }
        public long? SourceTransactionId { get; private set; }
  

        public TransactionInput(
            long transactionInputId,
            long bitcoinTransactionId,
            ByteArray sourceTransactionHash,
            int sourceTransactionOutputIndex,
            long? sourceTransactionId)
        {
            this.TransactionInputId = transactionInputId;
            this.BitcoinTransactionId = bitcoinTransactionId;
            this.SourceTransactionHash = sourceTransactionHash;
            this.SourceTransactionOutputIndex = sourceTransactionOutputIndex;
            this.SourceTransactionId = sourceTransactionId;
        }
    }
}