namespace BitcoinDataLayerAdoNet.Data
{
    using BitcoinBlockchain.Data;

    /// <summary>
    /// Contains information about a Bitcoin transaction output as saved in the Bitcoin SQL database.
    /// </summary>
    public class TransactionOutput
    {
        public static int OutputAddressIdUnknown = -1;
        public long TransactionOutputId { get; private set; }
        public long BitcoinTransactionId { get; private set; }
        public int OutputIndex { get; private set; }
        public decimal OutputValueBtc { get; private set; }
        public ByteArray OutputScript { get; private set; }
        public string OutputAddress { get; private set; }
        public long OutputAddressId { get; private set; }

        public TransactionOutput(
            long transactionOutputId,
            long bitcoinTransactionId,
            int outputIndex,
            decimal outputValueBtc,
            ByteArray outputScript,
            string outputAddress)
        {
            this.TransactionOutputId = transactionOutputId;
            this.BitcoinTransactionId = bitcoinTransactionId;
            this.OutputIndex = outputIndex;
            this.OutputValueBtc = outputValueBtc;
            this.OutputScript = outputScript;
            this.OutputAddress = outputAddress;
        }

    }
}