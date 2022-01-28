using System.Data;
using System.Threading;
using System.Threading.Tasks;
using TTG.SiteMap.Models;

namespace TTG.SiteMap
{
    public interface ISelectQuery
    {
        DataTable RunQuery(int page = 0);
        void SetConnectionString(string connectionString);
        void SetBatchConfiguration(BatchConfiguration batchConfiguration);
    }
}