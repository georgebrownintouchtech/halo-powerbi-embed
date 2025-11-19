using System.ComponentModel.DataAnnotations;

namespace HaloPowerBiEmbed.Api;

public class PowerBiOptions
{
    [Required(ErrorMessage = "PowerBi:TenantId is required.")]
    public string TenantId { get; set; } = string.Empty;
    [Required(ErrorMessage = "PowerBi:ClientId is required.")]
    public string ClientId { get; set; } = string.Empty;
    [Required(ErrorMessage = "PowerBi:ClientSecret is required.")]
    public string ClientSecret { get; set; } = string.Empty;
    [Required(ErrorMessage = "PowerBi:WorkspaceId is required.")]
    public string WorkspaceId { get; set; } = string.Empty;
    [Required(ErrorMessage = "PowerBi:ReportId is required.")]
    public string ReportId { get; set; } = string.Empty;
}