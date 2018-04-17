using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitcoinDatabaseGenerator
{
    using System;
    using System.Globalization;
    using System.Reflection;
    using BitcoinBlockchain.Parser;
    using BitcoinDataLayerAdoNet;
    using ZeroHelpers.Exceptions;
    using ZeroHelpers.ParameterParser;

    class Program
    {
        static int Main(string[] args)
        {
            int result = 1; // Assume error.

            try
            {
                ParameterParser<DatabaseGeneratorParameters> parameterParser = new ParameterParser<DatabaseGeneratorParameters>(DatabaseGeneratorParameters.ParameterListRules);
                DatabaseGeneratorParameters parameters = parameterParser.ParseParameters(args);

                if (parameters.IsHelpSpecified)
                {
                    TypeHelpPage();
                    result = 0; // Success.
                }
                else
                {
                    Console.WriteLine(GetApplicationNameAndVersion());

                    if (parameters.IsShowDbSchemaSpecified)
                    {
                        TypeDbSchema();
                        result = 0; // Success.
                    }
                    else
                    {
                        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Active threads: {0}", parameters.Threads));
                        Console.WriteLine();

                        DatabaseConnection databaseConnection = DatabaseConnection.CreateSqlServerConnection(parameters.SqlServerName, parameters.SqlDbName, parameters.SqlUserName, parameters.SqlPassword);
                        DatabaseGenerator databaseGenerator = new DatabaseGenerator(parameters, databaseConnection);
                        databaseGenerator.GenerateAndPopulateDatabase().Wait();

                        //一般情况下，只是调用GenerateAndPopulateDatabase()，将数据存入数据库，
                        //但有时为了做图方便及其他，为每个TX_IN直接添加对应的SourceTranceactionId,则
                        //把UpdateSourceTransactionId（）的注释删除。为建立索引，如果数据库已经建好的，一定要
                        //把GenerateAndPopulateDatabase()注释掉，否则会浪费很多时间。
                        //同理UpdateTransactionOutputAddressId()函数是为每个TXOUT的比特币地址设置一个ID值，相同的ADDR具有相同的ID
                        //选择性调用

                        //databaseGenerator.UpdateSourceTransactionId();
                        //databaseGenerator.UpdateTransactionOutputAddressId();

                        result = 0; // Success.
                    }
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }

            Console.WriteLine();

            //// TypePeakMemoryUsage();

            return result;
        }

        private static void HandleException(Exception ex)
        {
            if (ex is AggregateException)
            {
                HandleException(ex.InnerException);
            }
            else
            {
                Console.Error.WriteLine();

                if (ex is InvalidEnvironmentException)
                {
                    Console.Error.WriteLine("ERROR: {0}", ex.Message);
                }
                else if (ex is InvalidParameterException)
                {
                    Console.Error.WriteLine("Invalid command line: {0}", ex.Message);
                }
                else if (ex is InvalidBlockchainFilesException)
                {
                    Console.Error.WriteLine("ERROR: {0}", ex.Message);
                }
                else if (ex is InvalidBlockchainContentException)
                {
                    Console.Error.WriteLine("{0}{0}", Environment.NewLine);
                    Console.Error.WriteLine("ERROR: {0}", ex.Message);
                    Console.Error.WriteLine("It appears that the blockchain imported in the database is invalid.\nPlease make sure that files imported are valid and then run the \nBitcoinDatabaseGenerator.exe again specifying the /DropDb\nparameter in order to rebuild the entire database.");
                }
                else if (ex is UnknownBlockVersionException)
                {
                    Console.Error.WriteLine("ERROR: The blockchain contains blocks with an unknown version. {0}", ex.Message);
                }
                else
                {
                    Console.Error.WriteLine("AN ERROR OCCURRED:{0}{1}", Environment.NewLine, ex);
                }
            }
        }

        ////private static void TypePeakMemoryUsage()
        ////{
        ////    using (Process process = Process.GetCurrentProcess())
        ////    {
        ////        Console.WriteLine();
        ////        Console.WriteLine("Peak memory usage: {0:n0} bytes.", process.PeakWorkingSet64);
        ////    }
        ////}

        private static void TypeDbSchema()
        {
            Console.WriteLine();
            Console.WriteLine("Database schema:");
            Console.WriteLine("-----------------------------------------------------------------------------");
            Console.WriteLine(DatabaseManager.GetDatabaseSchema());
            Console.WriteLine("-----------------------------------------------------------------------------");
            Console.WriteLine();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", Justification = "Various strings in the help message need to be ignored by the spell checker.")]
        private static void TypeHelpPage()
        {
            Console.WriteLine();
            Console.WriteLine(GetApplicationNameAndVersion());

            Console.Write(
@"
Transfers data from Bitcoin blockchain files into a SQL Server database.

For access to sources and more information visit: 
https://github.com/ladimolnar/BitcoinDatabaseGenerator/wiki

Usage:  {0} 
        [/?] | [<transfer-options>] |
        [/ShowDbSchema] | [<auto-validation-options>]

<transfer-options>: 
        /BlockchainPath path 
        [/SqlServerName sql_server_name] /SqlDbName db_name
        [/SqlUserName user_name /SqlPassword pwd]
        [/Threads number_of_threads] [/DropDb] 
        [/BlockId block_id_value]

<auto-validation-options>:
        /RunValidation   
        [/SqlServerName sql_server_name] /SqlDbName db_name
        [/SqlUserName user_name /SqlPassword pwd]

/?               Displays this help page.
/BlockchainPath  Specifies the path to the folder where the
                 Bitcoin Core blockchain files are stored.
/SqlServerName   Specifies the name of the SQL Server.
                 Default value: localhost
/SqlDbName       Specifies the name of a SQL Server database.
/SqlUserName     Specifies the SQL Server user name.
/SqlPassword     Specifies the SQL Server user password.
                 When the SQL server user name and password are not
                 specified, Windows Authentication is used.
/Threads         Specifies the number of background threads.
                 Default value: the number of logical processors on your 
                 system.
                 The valid range is [1-100].
/BlockId         Specifies the value that will be used to check against
                 the BlockId of each block when parsing the blockchain.
                 Useful when the blockchain is generated on a test net
                 where the Block Id has a value different than the 
                 default one.
                 Default value: 0xD9B4BEF9
/DropDb          When specified the database will be dropped and recreated 
                 before the blockchain transfer is executed.
/ShowDbSchema    When specified the database schema will be displayed. 
                 You may want to use the command line redirect syntax in 
                 order to redirect the output to a file:
                 BitcoinDatabaseGenerator /ShowDbSchema > schema.txt
                 or the pipe syntax to copy the output to the clipboard:
                 BitcoinDatabaseGenerator /ShowDbSchema | clip
/RunValidation   Runs the application in auto-validation mode.
                 Reserved for development.
                 If you are a developer and make changes to the sources, 
                 in addition to the available test automation, you can run
                 the application in auto-validation mode. 
                 The application will run certain queries over an existing 
                 database, save the results to temporary data files and 
                 compare their content against built-in baselines. 
                 This test is based on the fact that data once in the 
                 blockchain should never change. The built-in baseline data
                 may be updated in future versions as the blockchain grows.

The tool will also work when you do not have access to the SQL Server at a
level needed to create a new database. If you have write access to a new
empty database, the tool will be able to setup the schema and execute the
data transfer using that database.",
                GetApplicationName());

            Console.WriteLine();
        }

        private static string GetApplicationName()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
        }

        private static string GetApplicationNameAndVersion()
        {
            AssemblyName assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName();

#if DEBUG
            const string configuration = " [DEBUG]";
#else
            const string configuration = null;
#endif

            return string.Format(CultureInfo.InvariantCulture, "{0} {1}.{2}{3}", assemblyName.Name, assemblyName.Version.Major, assemblyName.Version.Minor, configuration);
        }
    }
}
