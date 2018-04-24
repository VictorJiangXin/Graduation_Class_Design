using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Diagnostics;
using AdoNetHelpers;
using System.Data.SqlClient;

namespace BitcoinTransactionGraphGenerator
{
    class BitcoinAdoNetLayer : IDisposable
    {
        public const int DefaultDbCommandTimeout = 7200;

        private readonly SqlConnection sqlConnection;
        private readonly AdoNetLayer adoNetLayer;

        public BitcoinAdoNetLayer(string connectionString, int commandTimeout = DefaultDbCommandTimeout)
        {
            sqlConnection = new SqlConnection(connectionString);
            sqlConnection.Open();
            adoNetLayer = new AdoNetLayer(sqlConnection, commandTimeout);
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

        public void GenerateEdgeByBatch()
        {
            const int maxBatchSize = 10000000;
            Stopwatch updateTransactionSourceOutputWatch = new Stopwatch();
            updateTransactionSourceOutputWatch.Start();
            long BitcoinTransactionNum = this.GetMaxTransactionNum() + 1;

            long batchSize = BitcoinTransactionNum / 20;
            batchSize = batchSize >= 1 ? batchSize : 1;
            batchSize = batchSize <= maxBatchSize ? batchSize : maxBatchSize;

            string sqlCommandGenerateEdge = @"INSERT INTO Edge (SourceId, TargetId, Value)
	SELECT BitcoinTransaction.BitcoinTransactionId AS SourceId, T1.BitcoinTransactionId AS TargetId, TransactionOutput.OutputValueBtc AS Value
	FROM TransactionOutput
	INNER JOIN BitcoinTransaction ON BitcoinTransaction.BitcoinTransactionId = TransactionOutput.BitcoinTransactionId
	INNER JOIN TransactionInput ON TransactionInput.SourceTransactionHash = BitcoinTransaction.TransactionHash
								AND TransactionOutput.OutputIndex = TransactionInput.SourceTransactionOutputIndex
	INNER JOIN (
                    SELECT
							BitcoinTransaction.BitcoinTransactionId
                    FROM BitcoinTransaction
                    WHERE BitcoinTransaction.BitcoinTransactionId >= @leftBoundary and BitcoinTransaction.BitcoinTransactionId < @rightBoundary
                ) AS T1 ON T1.BitcoinTransactionId = TransactionInput.BitcoinTransactionId 
	order by TargetId";

            long leftBoundary = 0;
            long rightBoundary = leftBoundary + batchSize;

            Console.Write("\rGenerateing Edges(this may take a long time)... {0}%", 100 * (rightBoundary - batchSize) / BitcoinTransactionNum);

            while(leftBoundary < BitcoinTransactionNum)
            {
                this.adoNetLayer.ExecuteStatementNoResult(sqlCommandGenerateEdge, 
                    AdoNetLayer.CreateInputParameter("@leftBoundary", SqlDbType.BigInt, leftBoundary),
                    AdoNetLayer.CreateInputParameter("@rightBoundary", SqlDbType.BigInt, rightBoundary));
                leftBoundary += batchSize;
                rightBoundary = leftBoundary + batchSize;
                Console.Write("\rGenerateing Edges(this may take a long time)... {0}%", 100 * (rightBoundary - batchSize) / BitcoinTransactionNum);
            }

            updateTransactionSourceOutputWatch.Stop();
            Console.WriteLine("\rGenerate Graphic completed in {0:0.000} seconds.          ", updateTransactionSourceOutputWatch.Elapsed.TotalSeconds);
        }

        private long GetMaxTransactionNum()
        {
            string sqlCommandGetMaxTransactionId = @"SELECT TOP 1 BitcoinTransaction.BitcoinTransactionId
                                                     FROM BitcoinTransaction
                                                     ORDER BY BitcoinTransactionId desc";
            return AdoNetLayer.ConvertDbValue<long>(adoNetLayer.ExecuteScalar(sqlCommandGetMaxTransactionId));
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

            const string sqlGetMaxOutputAddressId = @"
                    SELECT MAX(OutputAddressId)
                    FROM TransactionOutput";

            long MaxOutputAddressId = AdoNetLayer.ConvertDbValue<long>(
                this.adoNetLayer.ExecuteScalar(sqlGetMaxOutputAddressId));

            long OutputAddressId = MaxOutputAddressId + 1;

            //得到TransactionOutput的数量
            long MaxTransactionOutputId = AdoNetLayer.ConvertDbValue<long>(
                this.adoNetLayer.ExecuteScalar(
                    @"select TOP 1 TransactionOutputId
                      from TransactionOutput
                      order by TransactionOutputId desc"));

            long TransactionOutputCount = -1;

            long SolveCount = 0;

            Console.WriteLine("开始为地址编号......耗时也许会很久");
            while (TransactionOutputCount < MaxTransactionOutputId)
            {
                //找到第一个OutputAddressId=-1 的 TxOutId即第一个未被编号的地址。
                TransactionOutputCount = AdoNetLayer.ConvertDbValue<long>(this.adoNetLayer.ExecuteScalar(sqlGetFirstTXOutUnsetAddressId,
                    AdoNetLayer.CreateInputParameter("@lastTransactionOutputId", SqlDbType.BigInt, TransactionOutputCount)));

                this.adoNetLayer.ExecuteStatementNoResult(sqlUpdateAddressId,
                    AdoNetLayer.CreateInputParameter("mOutputAddressId", SqlDbType.BigInt, OutputAddressId),
                    AdoNetLayer.CreateInputParameter("currentTransactionOutputId", SqlDbType.BigInt, TransactionOutputCount));
                SolveCount++;
                if (SolveCount % 100000 == 0)
                    Console.Write("\r已经处理了{0}个地址", SolveCount);
                OutputAddressId++;
            }
        }

        public SqlDataReader GetTransactionGraphEdgeReader(long bottomBound, long topBound)
        {
            string sqlGetEdge = @"
                SELECT Edge.SourceId, Edge.TargetId, Edge.Value
                FROM Edge
                WHERE EdgeId >= @BottomBound AND EdgeId < @TopBound";

            return this.adoNetLayer.ExecuteStatementReader(sqlGetEdge,
                AdoNetLayer.CreateInputParameter("@BottomBound", SqlDbType.BigInt, bottomBound),
                AdoNetLayer.CreateInputParameter("@TopBound", SqlDbType.BigInt, topBound));
        }
    }
}
