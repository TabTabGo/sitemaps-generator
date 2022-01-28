using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TTG.SiteMap.Models;

namespace TTG.SiteMap
{
    public class MSSqlSelectQuery : ISelectQuery
    {
        private BatchConfiguration _configuration;
        private string _connectionString;
        private readonly ILogger _logger;
        public MSSqlSelectQuery(ILogger<MSSqlSelectQuery>  logger)
        {
            _logger = logger;
        }

        public DataTable RunQuery(int page = 0)
        {
            var queryWithPaging = $"SELECT TOP {_configuration.MaxNumberOfLinks} * FROM (SELECT ROW_NUMBER() OVER (ORDER BY {_configuration.OrderByColumn}) AS RowNum, * FROM ({_configuration.SelectQuery}) AS source ) AS PagedResults WHERE RowNum > {page * _configuration.MaxNumberOfLinks}";
            var result = new DataTable();
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(queryWithPaging, connection))
                {
                    var sqlDataAdapter = new SqlDataAdapter(command);
                    sqlDataAdapter.Fill(result);
                }
                connection.Close();
            }

            return result;
        }

        public void SetConnectionString(string connectionString)
        {
            this._connectionString = connectionString;
        }

        public void SetBatchConfiguration(BatchConfiguration batchConfiguration)
        {
            this._configuration = batchConfiguration;
        }
    }


}