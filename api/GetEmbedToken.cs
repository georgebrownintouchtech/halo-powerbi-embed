using System;
using System.Net;
using System.Threading.Tasks;
using System.Collections.Generic;
using Azure.Identity;
using Azure.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using Microsoft.Rest;

namespace HaloPowerBiEmbed.Api
{
    public class GetEmbedToken
    {
        private readonly ILogger<GetEmbedToken> _logger;
        private readonly PowerBiOptions _powerBiOptions;
        private const string PowerBiScope = "https://analysis.windows.net/powerbi/api/.default";

        public GetEmbedToken(IOptions<PowerBiOptions> powerBiOptions, ILogger<GetEmbedToken> logger)
        {
            _logger = logger;
            _powerBiOptions = powerBiOptions.Value;
        }

        [Function("GetEmbedToken")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            try
            {
                // Authenticate with Azure AD
                var credential = new ClientSecretCredential(_powerBiOptions.TenantId, _powerBiOptions.ClientId, _powerBiOptions.ClientSecret);
                var accessToken = await credential.GetTokenAsync(new TokenRequestContext(new[] { PowerBiScope }));
                var tokenCredentials = new TokenCredentials(accessToken.Token, "Bearer");

                // Instantiate the Power BI client, providing both the API endpoint and the credential.
                using var client = new PowerBIClient(tokenCredentials) { BaseUri = new Uri("https://api.powerbi.com/") };
                // Retrieve report
                var reportResponse = await client.Reports.GetReportInGroupAsync(Guid.Parse(_powerBiOptions.WorkspaceId), Guid.Parse(_powerBiOptions.ReportId));
                var report = reportResponse;

                var tokenRequest = new GenerateTokenRequestV2
                {
                    Reports = { new GenerateTokenRequestV2Report(report.Id) },
                    Datasets = { new GenerateTokenRequestV2Dataset(report.DatasetId) },
                    TargetWorkspaces = { new GenerateTokenRequestV2TargetWorkspace(Guid.Parse(_powerBiOptions.WorkspaceId)) }
                    // Identities property is removed as we are not using RLS.
                };
                // Generate embed token
                var embedTokenResponse = await client.EmbedToken.GenerateTokenAsync(tokenRequest);
                var embedToken = embedTokenResponse;

                // Return JSON
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    reportId = report.Id,
                    embedUrl = report.EmbedUrl,
                    embedToken = embedToken.Token,
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embed token");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError); // Don't leak exception details to the client
                await error.WriteStringAsync("An error occurred while processing your request.");
                return error;
            }
        }
    }
}
