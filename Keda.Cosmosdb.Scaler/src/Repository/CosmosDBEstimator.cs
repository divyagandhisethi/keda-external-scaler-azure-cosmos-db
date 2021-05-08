using Keda.CosmosDB.Scaler.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Keda.CosmosDB.Scaler.Repository
{
    public class CosmosDBEstimator : ICosmosDBEstimator
    {
        private ILogger _logger;
        private ConcurrentDictionary<CosmosDBTrigger, ChangeFeedEstimator> _changeFeedBuilderMap;

        public CosmosDBEstimator(ILogger<CosmosDBEstimator> logger)
        {
            _logger = logger;
            _changeFeedBuilderMap = new ConcurrentDictionary<CosmosDBTrigger, ChangeFeedEstimator>(new CosmosDBTriggerComparer());
        }

        internal ChangeFeedEstimator GetOrCreateEstimator(CosmosDBTrigger trigger)
        {
            if (_changeFeedBuilderMap.TryGetValue(trigger, out ChangeFeedEstimator estimator))
            {
                return estimator;
            }

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Gateway
            };

            CosmosClient monitoredCosmosDBService = new CosmosClient(trigger.CosmosDBConnectionString, clientOptions);
            CosmosClient leaseCosmosDBService = new CosmosClient(trigger.Lease.LeasesCosmosDBConnectionString, clientOptions);

            var monitoredContainer = monitoredCosmosDBService.GetContainer(trigger.DatabaseName, trigger.CollectionName);
            var leaseContainer = leaseCosmosDBService.GetContainer(trigger.Lease.LeaseDatabaseName, trigger.Lease.LeaseCollectionName);

            estimator = monitoredContainer.GetChangeFeedEstimator(trigger.Lease.LeaseCollectionPrefix ?? string.Empty, leaseContainer);

            //TODO: Fix addorupdate
           // _changeFeedBuilderMap.AddOrUpdate(trigger, estimator);
            return estimator;
        }

        public async Task<long> GetEstimatedWork(CosmosDBTrigger trigger)
        {
            var estimator = GetOrCreateEstimator(trigger);
            using FeedIterator<ChangeFeedProcessorState> estimatorIterator = estimator.GetCurrentStateIterator();
            while (estimatorIterator.HasMoreResults)
            {
                FeedResponse<ChangeFeedProcessorState> states = await estimatorIterator.ReadNextAsync();
                foreach (ChangeFeedProcessorState leaseState in states)
                {
                    if (leaseState.EstimatedLag > 0)
                    {
                        _logger.LogDebug(
                            $"Lease {leaseState.LeaseToken} owned by host {leaseState.InstanceName ?? "None"} has an estimated lag of {leaseState.LeaseToken}");
                        return leaseState.EstimatedLag;
                    }
                }
            }
            return 0;
        }
    }
}