using DurableFunction.Domain;
using DurableFunction.Domain.Enums;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace DurableFunction.Client
{
    /// <summary>
    /// SortLogic
    /// </summary>
    public static class SortLogic
    {
        /// <summary>
        /// Runs the orchestrator.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="logger">The logger.</param>
        /// <returns>Collection of SortJob</returns>
        [FunctionName(Constants.SortOrchestartorName)]
        public static async Task<SortJob> RunOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext context, ILogger logger)
        {
            var outputs = new SortJob();
            if (!context.IsReplaying)
            {
                logger.LogInformation("Collecting the input...");
            }
            var sortJobData = context.GetInput<int[]>();
            outputs = await context.CallActivityAsync<SortJob>(Constants.SortActivityName, sortJobData);
            if (!context.IsReplaying)
            {
                logger.LogInformation("Message sent to Activity function...");
            }

            if (outputs == null)
            {
                context.SetCustomStatus("Activity Function failed...");
            }
            return outputs;
        }

        /// <summary>
        /// Does the sort.
        /// </summary>
        /// <param name="jobData">The job data.</param>
        /// <param name="log">The log.</param>
        /// <returns>SortJob</returns>
        [FunctionName(Constants.SortActivityName)]
        public static async Task<SortJob> DoSort([ActivityTrigger] int[] jobData, ILogger log)
        {
            log.LogInformation($"Sort Job {string.Join(",", jobData)}.");
            Stopwatch sw = Stopwatch.StartNew();
            await Task.Delay(2000);
            var sortedData = jobData.OrderBy(n => n).ToArray();
            sw.Stop();
            var jobResult = new SortJob
            {
                Id = Guid.NewGuid(),
                Input = jobData,
                Output = sortedData,
                Status = nameof(SortJobStatus.Completed),
                Duration = sw.Elapsed
            };
            sw.Stop();
            sw = null;
            return jobResult;
        }

        /// <summary>
        /// HTTPs the start.
        /// </summary>
        /// <param name="httpRequest">The HTTP request.</param>
        /// <param name="starter">The starter.</param>
        /// <param name="log">The log.</param>
        /// <returns>HttpResponseMessage</returns>
        [FunctionName(Constants.SortOrchestartorClientName)]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestMessage httpRequest,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            var payload = await httpRequest.Content.ReadAsAsync<int[]>();
            string instanceId = await starter.StartNewAsync(Constants.SortOrchestartorName, payload);
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
            return starter.CreateCheckStatusResponse(httpRequest, instanceId);
        }
    }
}