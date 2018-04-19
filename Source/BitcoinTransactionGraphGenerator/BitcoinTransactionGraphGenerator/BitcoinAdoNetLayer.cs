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
    }
}
