using System;
using System.Net;
using System.Threading.Tasks;
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
                _logger.LogInformation("PowerBiOptions: TenantId={TenantId}, ClientId={ClientId}, ClientSecretPresent={ClientSecretPresent}, WorkspaceId={WorkspaceId}, ReportId={ReportId}",
                    _powerBiOptions.TenantId,
                    _powerBiOptions.ClientId,
                    !string.IsNullOrEmpty(_powerBiOptions.ClientSecret) ? "Yes" : "No", // Log presence, not the secret itself
                    _powerBiOptions.WorkspaceId,
                    _powerBiOptions.ReportId);

                var workspaceId = Guid.Parse(_powerBiOptions.WorkspaceId);
                var reportId = Guid.Parse(_powerBiOptions.ReportId);

                // Authenticate with Azure AD
                var credential = new ClientSecretCredential(_powerBiOptions.TenantId, _powerBiOptions.ClientId, _powerBiOptions.ClientSecret);
                var accessToken = await credential.GetTokenAsync(new TokenRequestContext(new[] { PowerBiScope }));

                var tokenCredentials = new TokenCredentials(accessToken.Token, "Bearer");

                using var client = new PowerBIClient(new Uri("https://api.powerbi.com/"), tokenCredentials);
                var report = await client.Reports.GetReportInGroupAsync(workspaceId, reportId);

                if (report == null)
                {
                    _logger.LogError("Report with ID '{ReportId}' not found in workspace '{WorkspaceId}'. Check IDs and permissions.", _powerBiOptions.ReportId, _powerBiOptions.WorkspaceId);
                    throw new InvalidOperationException($"Report '{_powerBiOptions.ReportId}' not found in workspace '{_powerBiOptions.WorkspaceId}'. Please verify the ReportId, WorkspaceId, and the Azure AD application's permissions.");
                }

                // Create the token request for the report
                var tokenRequest = new GenerateTokenRequestV2
                {
                    Reports = new List<GenerateTokenRequestV2Report> { new(report.Id) },
                    TargetWorkspaces = new List<GenerateTokenRequestV2TargetWorkspace> { new(workspaceId) },
                    Datasets = new List<GenerateTokenRequestV2Dataset>()
                };
 
                // *** FIX ***
                // Only add the dataset if the report has one.
                // Paginated reports (RDL) will not have a DatasetId here and will cause a NullReferenceException.
                if (!string.IsNullOrEmpty(report.DatasetId))
                {
                    tokenRequest.Datasets.Add(new GenerateTokenRequestV2Dataset(report.DatasetId));
                }

                // Generate embed token
                var embedToken = await client.EmbedToken.GenerateTokenAsync(tokenRequest);
                
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