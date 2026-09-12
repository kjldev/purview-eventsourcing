using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the generated Admin API OpenAPI client with the dependency injection container.
/// </summary>
/// <remarks>
/// The members of this type are hidden from IntelliSense as the type is only intended to be consumed
/// through the <see langword="static"/> using for the <c>Microsoft.Extensions.DependencyInjection</c> namespace.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class AdminClientServiceCollectionExtensions
{
	/// <summary>
	/// Registers <see cref="AdminApiClient"/> as a typed <see cref="HttpClient"/>.
	/// </summary>
	/// <param name="services">The service collection to configure.</param>
	/// <param name="configure">Optional <see cref="AdminClientOptions"/> configuration.</param>
	/// <param name="configureClient">
	/// Optional hook to extend the underlying <see cref="IHttpClientBuilder"/>, for example to add delegating
	/// handlers that resolve the API origin from the current HTTP request.
	/// </param>
	/// <returns>The configured service collection.</returns>
	/// <remarks>
	/// <para>
	/// The generated client targets the Admin API route prefix from the OpenAPI document (by default
	/// <c>/admin/api</c>). When <see cref="AdminClientOptions.BaseURL"/> is <see langword="null"/> the client
	/// sends relative request URIs, so the hosting application must either configure
	/// <see cref="AdminClientOptions.BaseURL"/> or add a delegating handler (via
	/// <paramref name="configureClient"/>) that resolves the origin.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddAdminAPIClient(
		[NotNull] this IServiceCollection services,
		Action<AdminClientOptions>? configure = null,
		Action<IHttpClientBuilder>? configureClient = null
	)
	{
		services.AddOptions<AdminClientOptions>().Configure(options => configure?.Invoke(options));

		var builder = services
			.AddHttpClient(
				AdminClientDefaults.HttpClientName,
				(serviceProvider, client) =>
				{
					var options = serviceProvider.GetRequiredService<IOptions<AdminClientOptions>>().Value;
					client.BaseAddress =
						options.BaseURL
						// HttpClient requires an absolute BaseAddress for relative request URIs. When no base URL
						// is configured, a non-routable placeholder is used and a delegating handler (for example
						// the same-origin resolver used by the Admin portal site) rewrites each request to the real
						// origin.
						?? new Uri("http://admin.invalid/", UriKind.Absolute);
				}
			)
			.AddTypedClient(
				(httpClient, serviceProvider) =>
				{
					var options = serviceProvider.GetRequiredService<IOptions<AdminClientOptions>>().Value;
					return new AdminApiClient(options.BaseURL?.ToString() ?? string.Empty, httpClient);
				}
			)
			.AddHttpMessageHandler(serviceProvider => new AdminClientDelegatingHandler(serviceProvider));

		configureClient?.Invoke(builder);

		return services;
	}
}

sealed class AdminClientDelegatingHandler(IServiceProvider serviceProvider) : DelegatingHandler
{
	protected override Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken
	)
	{
		var options = serviceProvider.GetRequiredService<IOptions<AdminClientOptions>>().Value;

		if (!string.IsNullOrWhiteSpace(options.AccessToken))
			request.Headers.Authorization = new("Bearer", options.AccessToken);

		return base.SendAsync(request, cancellationToken);
	}
}
