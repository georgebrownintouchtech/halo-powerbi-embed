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
                // Get the user identity from the query string for RLS.
                // In a real app, you'd get this from a validated JWT or session cookie.
                string? rlsUser = req.Query["userId"];
                if (string.IsNullOrWhiteSpace(rlsUser) || rlsUser.Equals("null", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Request received without a 'userId' query parameter for RLS.");
                    var badReqResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReqResponse.WriteStringAsync("A 'userId' query parameter is required.");
                    return badReqResponse;
                }

                // Authenticate with Azure AD
                var credential = new ClientSecretCredential(_powerBiOptions.TenantId, _powerBiOptions.ClientId, _powerBiOptions.ClientSecret);
                var accessToken = await credential.GetTokenAsync(new TokenRequestContext(new[] { PowerBiScope }));
                var tokenCredentials = new TokenCredentials(accessToken.Token, "Bearer");

                // Instantiate the Power BI client, providing both the API endpoint and the credential.
                using var client = new PowerBIClient(tokenCredentials) { BaseUri = new Uri("https://api.powerbi.com/") };
                // Retrieve report
                var reportResponse = await client.Reports.GetReportInGroupAsync(Guid.Parse(_powerBiOptions.WorkspaceId), Guid.Parse(_powerBiOptions.ReportId));
                var report = reportResponse;

                // Define the RLS identity. This assumes you have a role named 'UserRole' in your PBIX file.
                // For this SDK version, EffectiveIdentity uses a parameterless constructor and properties are set.
                var rlsIdentity = new EffectiveIdentity
                {
                    Username = rlsUser,
                    Roles = { "UserRole" },
                    Datasets = { report.DatasetId }
                };

                var tokenRequest = new GenerateTokenRequestV2
                {
                    Reports = { new GenerateTokenRequestV2Report(report.Id) },
                    Datasets = { new GenerateTokenRequestV2Dataset(report.DatasetId) },
                    TargetWorkspaces = { new GenerateTokenRequestV2TargetWorkspace(Guid.Parse(_powerBiOptions.WorkspaceId)) },
                    Identities = { rlsIdentity }
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
