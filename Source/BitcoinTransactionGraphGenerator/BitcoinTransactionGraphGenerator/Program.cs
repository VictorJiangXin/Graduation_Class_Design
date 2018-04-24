using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitcoinDataLayerAdoNet;
using System.Data.SqlClient;
using System.IO;

namespace BitcoinTransactionGraphGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            string FilePath = @"F:\Edge.csv";
            DatabaseConnection bc = DatabaseConnection.CreateSqlServerConnection(@"localhost\sq2", "BITCOIN");
            using (BitcoinAdoNetLayer bitcoinAdoNetLay = new BitcoinAdoNetLayer(bc.ConnectionString)) {
                SqlDataReader sr = bitcoinAdoNetLay.GetTransactionGraphEdgeReader(764746401, 766746401);
                using(StreamWriter sw = File.CreateText(FilePath))
                {
                    sw.WriteLine("Source, Target, Value");
                    while (sr.Read())
                    {
                        sw.WriteLine("{0}, {1}, {2}", sr["SourceId"], sr["TargetId"], sr["Value"]);
                    }
                    sw.Close();
                }
                sr.Close();
            }
        }
    }
}
