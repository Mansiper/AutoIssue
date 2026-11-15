using AutoIssue.Internal;
using AutoIssue.Options;
using AutoIssue.Providers;
using AutoIssue.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace AutoIssue;

/// <summary>
/// Extension methods for registering AutoIssue services in the dependency injection container.
/// </summary>
public static class Bootstrap
{
    /// <summary>
    /// Registers the GitHub Feedback Bridge services.
    /// Reads configuration from the <c>GitHubFeedback</c> section of <paramref name="configuration"/>.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">Application configuration containing the <c>GitHubFeedback</c> section.</param>
    /// <param name="validateOnStart">
    /// When <see langword="true"/> (default), configuration is validated when the application starts.
    /// Set to <see langword="false"/> to defer validation until the service is first used,
    /// for example during local development with placeholder credentials.
    /// </param>
    public static IServiceCollection AddGitHubFeedback(
        this IServiceCollection services, IConfiguration configuration, bool validateOnStart = true)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IValidateOptions<GitHubFeedbackOptions>, GitHubFeedbackOptionsValidator>();

        var optBuilder = services.AddOptions<GitHubFeedbackOptions>()
            .Bind(configuration.GetSection(GitHubFeedbackOptions.SectionName));

        if (validateOnStart)
            optBuilder.ValidateOnStart();

        services.AddHttpClient(HttpClientNames.Issues, (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<GitHubFeedbackOptions>>().Value;
            ConfigureClient(client, opts.ApiBaseUrl, opts.Issues.Token, opts.ApiVersion, opts.UserAgent);
        });

        services.AddHttpClient(HttpClientNames.Attachments, (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<GitHubFeedbackOptions>>().Value;
            var token = string.IsNullOrWhiteSpace(opts.Attachments.Token)
                ? opts.Issues.Token
                : opts.Attachments.Token;
            ConfigureClient(client, opts.ApiBaseUrl, token, opts.ApiVersion, opts.UserAgent);
        });

        services.AddSingleton<IGitHubHttpProvider, GitHubHttpProvider>();
        services.AddScoped<IAttachmentFileValidator, DefaultAttachmentFileValidator>();
        services.AddScoped<IFeedbackAttachmentStore, GitHubRepoAttachmentStore>();
        services.AddScoped<IGitHubFeedbackService, GitHubFeedbackService>();

        return services;
    }

    private static void ConfigureClient(
        HttpClient client, string baseUrl, string token, string apiVersion, string userAgent)
    {
        client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + '/');
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", apiVersion);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
    }
}