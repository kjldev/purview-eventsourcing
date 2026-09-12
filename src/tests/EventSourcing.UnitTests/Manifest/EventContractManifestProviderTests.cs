using Microsoft.Extensions.DependencyInjection;

namespace Purview.EventSourcing.Manifest;

public sealed class EventContractManifestProviderTests
{
	const string ManifestJson = /*lang=json,strict*/
		"""{"formatVersion":1,"aggregates":[]}""";

	[Test]
	public async Task GetAsync_GivenNoBaseline_ReportsNotConfigured(CancellationToken cancellationToken)
	{
		EventContractManifestProvider provider = new(1, ManifestJson, baselineJson: null);

		var info = await provider.GetAsync(cancellationToken);

		await Assert.That(info.FormatVersion).IsEqualTo(1);
		await Assert.That(info.Json).IsEqualTo(ManifestJson);
		await Assert.That(info.CompatibilityStatus).IsEqualTo(EventContractCompatibilityStatus.NotConfigured);
	}

	[Test]
	public async Task GetAsync_GivenMatchingBaseline_ReportsCompatible(CancellationToken cancellationToken)
	{
		EventContractManifestProvider provider = new(1, ManifestJson, baselineJson: ManifestJson);

		var info = await provider.GetAsync(cancellationToken);

		await Assert.That(info.CompatibilityStatus).IsEqualTo(EventContractCompatibilityStatus.Compatible);
	}

	[Test]
	public async Task GetAsync_GivenMismatchedBaseline_ReportsIncompatible(CancellationToken cancellationToken)
	{
		const string changedManifest =
			/*lang=json,strict*/
			"""{"formatVersion":1,"aggregates":[{"name":"OrderAggregate","namespace":"Testing","events":[]}]}""";
		EventContractManifestProvider provider = new(1, ManifestJson, baselineJson: changedManifest);

		var info = await provider.GetAsync(cancellationToken);

		await Assert.That(info.CompatibilityStatus).IsEqualTo(EventContractCompatibilityStatus.Incompatible);
	}

	[Test]
	public async Task AddEventContractManifest_RegistersProvider(CancellationToken cancellationToken)
	{
		ServiceCollection services = new();
		services.AddEventContractManifest(1, ManifestJson, baselineJson: ManifestJson);

		using var provider = services.BuildServiceProvider();
		var manifestProvider = provider.GetRequiredService<IEventContractManifestProvider>();

		var info = await manifestProvider.GetAsync(cancellationToken);
		await Assert.That(info.CompatibilityStatus).IsEqualTo(EventContractCompatibilityStatus.Compatible);
	}
}
