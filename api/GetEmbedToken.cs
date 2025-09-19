using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using Microsoft.Identity.Client;
using System.Net;
using System;
using System.Threading.Tasks;

namespace HaloPowerBiEmbed.Api
{
    public class GetEmbedToken
    {
        private readonly ILogger _logger;

        public GetEmbedToken(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<GetEmbedToken>();
        }

        [Function("GetEmbedToken")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request to get Power BI embed token.");

            try
            {
                // Get credentials from Application Settings (Environment Variables)
                string tenantId = Environment.GetEnvironmentVariable("PowerBi:TenantId");
                string clientId = Environment.GetEnvironmentVariable("PowerBi:ClientId");
                string clientSecret = Environment.GetEnvironmentVariable("PowerBi:ClientSecret");
                string workspaceId = Environment.GetEnvironmentVariable("PowerBi:WorkspaceId");
                string reportId = Environment.GetEnvironmentVariable("PowerBi:ReportId");

                // Authenticate with Azure AD
                var authorityUrl = $"https://login.microsoftonline.com/{tenantId}";
                var app = ConfidentialClientApplicationBuilder.Create(clientId)
                    .WithClientSecret(clientSecret)
                    .WithAuthority(new Uri(authorityUrl))
                    .Build();

                var scopes = new[] { "https://analysis.windows.net/powerbi/api/.default" };
                var authResult = await app.AcquireTokenForClient(scopes).ExecuteAsync();

                // Connect to Power BI
                var powerBiClient = new PowerBIClient(new Uri("https://api.powerbi.com/"), new Microsoft.Rest.TokenCredentials(authResult.AccessToken, "Bearer"));

                // Get the report from the workspace
                var report = await powerBiClient.Reports.GetReportInGroupAsync(new Guid(workspaceId), new Guid(reportId));

                // Generate the Embed Token for "View" access
                var generateTokenRequestParameters = new GenerateTokenRequest(accessLevel: "View");
                var embedToken = await powerBiClient.Reports.GenerateTokenInGroupAsync(new Guid(workspaceId), new Guid(reportId), generateTokenRequestParameters);

                // Build the response object for the frontend
                var responsePayload = new
                {
                    reportId = report.Id.ToString(),
                    embedUrl = report.EmbedUrl,
                    embedToken = embedToken.Token
                };
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(responsePayload);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate Power BI embed token.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}