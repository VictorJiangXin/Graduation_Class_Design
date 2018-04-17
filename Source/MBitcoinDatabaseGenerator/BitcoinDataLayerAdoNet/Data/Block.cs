namespace BitcoinDataLayerAdoNet.Data
{
    using BitcoinBlockchain.Data;
    using System;
    ///<summary>
    ///Contains information about a Bitcoin transaction as saved in the Bitcoin SQL Database 
    /// </summary>
    public class BLOCK
    {
        public long BlockId { get; private set; }
        public long BlockchainFileId { get; private set; }
        public int BlockVersion { get; private set; }
        public ByteArray BlockHash { get; private set; }
        public ByteArray PreviousBlockHash { get; private set; }
        public int TransactionsCount { get; private set; }
        public ByteArray MerkleRootHash { get; private set; }
        public DateTime BlockTimeStamp { get; private set; }


        public BLOCK(
            long blockId,
            long blockchainFileId,
            int blockVersion,
            ByteArray blockHash,
            ByteArray previousBlockHash,
            int transactionsCount,
            ByteArray merkleRootHash,
            DateTime blockTimeStamp
            )
        {
            this.BlockId = blockId;
            this.BlockchainFileId = blockchainFileId;
            this.BlockVersion = blockVersion;
            this.BlockHash = blockHash;
            this.PreviousBlockHash = previousBlockHash;
            this.TransactionsCount = transactionsCount;
            this.MerkleRootHash = merkleRootHash;
            this.BlockTimeStamp = blockTimeStamp;
        }

    }
}