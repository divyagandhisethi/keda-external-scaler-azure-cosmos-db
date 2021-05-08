using Microsoft.Azure.Cosmos;
using System.Threading.Tasks;
using Keda.CosmosDB.Scaler.Services;

namespace Keda.CosmosDB.Scaler.Repository
{
    public interface ICosmosDBEstimator
    {
        Task<long> GetEstimatedWork(CosmosDBTrigger trigger);
    }
}