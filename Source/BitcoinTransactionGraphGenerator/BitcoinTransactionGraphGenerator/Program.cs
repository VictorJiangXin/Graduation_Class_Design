using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitcoinDataLayerAdoNet;

namespace BitcoinTransactionGraphGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            DatabaseConnection bc = DatabaseConnection.CreateSqlServerConnection(@"localhost\sq2", "BITCOIN");
            using (BitcoinAdoNetLayer bitcoinAdoNetLay = new BitcoinAdoNetLayer(bc.ConnectionString)) {
                bitcoinAdoNetLay.GenerateEdgeByBatch();
            }
        }
    }
}
