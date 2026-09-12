namespace Purview.EventSourcing.Admin.Client;

/// <summary>
/// The default configuration section used to bind <see cref="AdminClientOptions"/>.
/// </summary>
public static class AdminClientDefaults
{
	/// <summary>
	/// The configuration section name.
	/// </summary>
	public const string SectionName = "AdminClient";

	/// <summary>
	/// The name of the underlying <see cref="HttpClient"/> registration.
	/// </summary>
	public const string HttpClientName = "Purview.EventSourcing.Admin.Client";
}

/// <summary>
/// Configures the generated Admin API client.
/// </summary>
public sealed class AdminClientOptions
{
	/// <summary>
	/// Gets or sets the base URL (origin) of the Admin API, for example <c>https://admin.example.com</c>.
	/// When <see langword="null"/>, the client resolves the origin from the current HTTP request
	/// (same-origin), which is appropriate when the caller runs inside the web application that hosts the Admin
	/// API.
	/// </summary>
	public Uri? BaseURL { get; set; }

	/// <summary>
	/// Gets or sets the bearer access token sent with every request, or <see langword="null"/> when the API does
	/// not require one.
	/// </summary>
	public string? AccessToken { get; set; }
}
