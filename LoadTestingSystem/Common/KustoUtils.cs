using Azure.Core;
using Azure.Identity;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using Kusto.Data;
using System;
using System.Threading.Tasks;

namespace Microsoft.PowerBI.Test.E2E.Common.NotebookRunners.Utils.KustoClient
{
    /// <summary>
    /// Class to interact with Kusto.
    /// </summary>
    internal class KustoUtils
    {
        private static AccessToken token;

        /// <summary>
        /// Get the result of a Kusto query.
        /// </summary>
        /// <param name="kustoEndPoint"></param>
        /// <param name="kustoDb"></param>
        /// <param name="query"></param>
        /// <returns>Result of query.</returns>
        internal static async Task<object[]> GetKustoQueryResultAsync(string kustoEndPoint, string kustoDb, string query)
        {
            var credential = new DefaultAzureCredential();
            if (token.Token == null || token.ExpiresOn.UtcDateTime - DateTimeOffset.UtcNow < TimeSpan.FromMinutes(5))
            {
                token = await credential.GetTokenAsync(new TokenRequestContext(new[] { kustoEndPoint }));
            }

            var kcsb = new KustoConnectionStringBuilder(kustoEndPoint)
            {
                FederatedSecurity = true,
                UserToken = token.Token,
            };
            using var queryProvider = KustoClientFactory.CreateCslQueryProvider(kcsb);
            var clientRequestProperties = new ClientRequestProperties() { ClientRequestId = Guid.NewGuid().ToString() };
            clientRequestProperties.SetOption(ClientRequestProperties.OptionNoTruncation, true);
            using (var reader = await queryProvider.ExecuteQueryAsync(
                kustoDb,
                query,
                clientRequestProperties))
            {
                object[] queryResult = new object[reader.FieldCount];
                while (reader.Read())
                {
                    reader.GetValues(queryResult);
                }
                return queryResult;
            }
        }
    }
}
