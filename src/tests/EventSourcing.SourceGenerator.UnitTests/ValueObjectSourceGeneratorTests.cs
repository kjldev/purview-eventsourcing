using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Purview.EventSourcing.SourceGenerator;

public sealed class ValueObjectSourceGeneratorTests : SourceGeneratorTestBase<ValueObjectSourceGenerator>
{
	static string GetGeneratedSource(GeneratorDriverRunResult result) =>
		string.Join(
			Environment.NewLine,
			result
				.Results.SelectMany(static generatorResult => generatorResult.GeneratedSources)
				.Select(static generatedSource => generatedSource.SourceText.ToString())
		);

	static Diagnostic[] GetGeneratorDiagnostics(GeneratorDriverRunResult result) =>
		[.. result.Results.SelectMany(static generatorResult => generatorResult.Diagnostics).OrderBy(static d => d.Id)];

	[Test]
	public async Task ScalarGeneration_UsesStrictCreateAndHydrateCorrectly(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{

			[Purview.EventSourcing.Serialization.Scalar]
			public readonly partial record struct EmailAddress
			{
				public string Value { get; }

				private EmailAddress(string value) => Value = value;

				static partial void OnNormalize(ref string value)
				{
					value = value?.Trim().ToLowerInvariant()!;
				}

				static partial void OnValidate(string value)
				{
					if (string.IsNullOrWhiteSpace(value))
						throw new System.ArgumentException("Email address cannot be empty.", nameof(value));

					if (!value.Contains("@", System.StringComparison.Ordinal))
						throw new System.ArgumentException("Invalid email address format.", nameof(value));
				}
			}

			public static class ValueObjectHarness
			{
				public static string StrictCreate() => EmailAddress.Create(" TEST@Example.COM ").Value;

				public static string HydratePreserves() => EmailAddress.Hydrate(" TEST@Example.COM ").Value;

				public static string HydrateInvalid() => EmailAddress.Hydrate("not-an-email").Value;

				public static bool TryCreateInvalid() => EmailAddress.TryCreate("not-an-email", out _);

				public static string SerializeEmail() => System.Text.Json.JsonSerializer.Serialize(EmailAddress.Create("test@example.com"));

				public static string DeserializeEmail() => System.Text.Json.JsonSerializer.Deserialize<EmailAddress>("\"not-an-email\"").Value;

				public static string ImplicitFromPrimitive()
				{
					EmailAddress email = " TEST@Example.COM ";
					return email.Value;
				}

				public static string ImplicitToPrimitive()
				{
					string value = EmailAddress.Create("test@example.com");
					return value;
				}

				public static bool EqualsPrimitive() => EmailAddress.Create("test@example.com").Equals("test@example.com");

				public static bool OperatorEqualsPrimitive() =>
					EmailAddress.Create("test@example.com") == "test@example.com";

				public static bool OperatorEqualsPrimitiveReverse() =>
					"test@example.com" == EmailAddress.Create("test@example.com");

				public static int CompareWithPrimitive() => EmailAddress.Create("b@example.com").CompareTo("a@example.com");

				public static int CompareWithObject() => EmailAddress.Create("a@example.com").CompareTo((object)EmailAddress.Create("b@example.com"));
			}
			}
			""";

		var assembly = await CompileToAssemblyAsync(source, cancellationToken);
		var harnessType = assembly.GetType("Testing.ValueObjectHarness")!;

		var strictCreate = (string)harnessType.GetMethod("StrictCreate")!.Invoke(null, null)!;
		var hydratePreserves = (string)harnessType.GetMethod("HydratePreserves")!.Invoke(null, null)!;
		var hydrateInvalid = (string)harnessType.GetMethod("HydrateInvalid")!.Invoke(null, null)!;
		var tryCreateInvalid = (bool)harnessType.GetMethod("TryCreateInvalid")!.Invoke(null, null)!;
		var serialized = (string)harnessType.GetMethod("SerializeEmail")!.Invoke(null, null)!;
		var deserialized = (string)harnessType.GetMethod("DeserializeEmail")!.Invoke(null, null)!;
		var implicitFrom = (string)harnessType.GetMethod("ImplicitFromPrimitive")!.Invoke(null, null)!;
		var implicitTo = (string)harnessType.GetMethod("ImplicitToPrimitive")!.Invoke(null, null)!;
		var equalsPrimitive = (bool)harnessType.GetMethod("EqualsPrimitive")!.Invoke(null, null)!;
		var operatorEqualsPrimitive = (bool)harnessType.GetMethod("OperatorEqualsPrimitive")!.Invoke(null, null)!;
		var operatorEqualsPrimitiveReverse = (bool)
			harnessType.GetMethod("OperatorEqualsPrimitiveReverse")!.Invoke(null, null)!;
		var comparePrimitive = (int)harnessType.GetMethod("CompareWithPrimitive")!.Invoke(null, null)!;
		var compareObject = (int)harnessType.GetMethod("CompareWithObject")!.Invoke(null, null)!;

		await Assert.That(strictCreate).IsEqualTo("test@example.com");
		await Assert.That(hydratePreserves).IsEqualTo(" TEST@Example.COM ");
		await Assert.That(hydrateInvalid).IsEqualTo("not-an-email");
		await Assert.That(tryCreateInvalid).IsFalse();
		await Assert.That(serialized).IsEqualTo("\"test@example.com\"");
		await Assert.That(deserialized).IsEqualTo("not-an-email");
		await Assert.That(implicitFrom).IsEqualTo("test@example.com");
		await Assert.That(implicitTo).IsEqualTo("test@example.com");
		await Assert.That(equalsPrimitive).IsTrue();
		await Assert.That(operatorEqualsPrimitive).IsTrue();
		await Assert.That(operatorEqualsPrimitiveReverse).IsTrue();
		await Assert.That(comparePrimitive).IsEqualTo(1);
		await Assert.That(compareObject).IsEqualTo(-1);
	}

	[Test]
	public async Task ScalarGeneration_GeneratesPrivateConstructorWhenMissing(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{

				[Purview.EventSourcing.Serialization.Scalar]
				public readonly partial record struct PhoneNumber
				{
					public string Value { get; }
				}

				public static class PhoneHarness
				{
					public static string CreatePhone() => PhoneNumber.Create("12345").Value;

					public static string HydratePhone() => PhoneNumber.Hydrate("67890").Value;
				}
			}
			""";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetGeneratedSource(result);
		var diagnostics = GetGeneratorDiagnostics(result);

		await Assert.That(generatedSource).Contains("private PhoneNumber(string value) => Value = value;");
		await Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id)).DoesNotContain(
			GeneratorDiagnostics.ScalarConstructorMissing.Id
		);

		var assembly = await CompileToAssemblyAsync(source, cancellationToken);
		var harnessType = assembly.GetType("Testing.PhoneHarness")!;

		var created = (string)harnessType.GetMethod("CreatePhone")!.Invoke(null, null)!;
		var hydrated = (string)harnessType.GetMethod("HydratePhone")!.Invoke(null, null)!;

		await Assert.That(created).IsEqualTo("12345");
		await Assert.That(hydrated).IsEqualTo("67890");
	}

	[Test]
	public async Task ScalarGeneration_GeneratesPrivateConstructorOnIContextualValueObjectWhenMissing(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				public class ProjectAggregate : AggregateBase
				{
					public ProjectId Id { get; }

					protected override void RegisterEvents()
					{
					}
				}

				[Scalar]
				public readonly partial record struct ProjectId : IContextualValueObject<ProjectId, string, ProjectAggregate>
				{
					public string Value { get; }

					static partial void OnValidate(string value)
					{
						if (!Guid.TryParse(value, out var parsedValue))
							throw new ArgumentException("ProjectId must be a valid GUID.", nameof(value));

						if (parsedValue == Guid.Empty)
							throw new ArgumentException("ProjectId cannot be empty.", nameof(value));
					}

					public static ProjectId Create(string value, in ValueObjectContext<ProjectAggregate> context) => new(value);
				}

				public static class ProjectHarness
				{
					public static string CreateProjectId() => ProjectId.Create("5801da4a-ed0f-46de-ba9d-5b6adda6e917").Value;

					public static string HydrateProjectId() => ProjectId.Hydrate("6801da4a-ed0f-46de-ba9d-5b6adda6e917").Value;
				}
			}
			""";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetGeneratedSource(result);

		await Assert.That(generatedSource).Contains("private ProjectId(string value) => Value = value;");

		var assembly = await CompileToAssemblyAsync(source, cancellationToken);
		var harnessType = assembly.GetType("Testing.ProjectHarness")!;

		var created = (string)harnessType.GetMethod("CreateProjectId")!.Invoke(null, null)!;
		var hydrated = (string)harnessType.GetMethod("HydrateProjectId")!.Invoke(null, null)!;

		await Assert.That(created).IsEqualTo("5801da4a-ed0f-46de-ba9d-5b6adda6e917");
		await Assert.That(hydrated).IsEqualTo("6801da4a-ed0f-46de-ba9d-5b6adda6e917");
	}

	[Test]
	public async Task ScalarGeneration_WarnsWhenScalarStructIsNotRecordStruct(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Purview.EventSourcing.Serialization.Scalar]
				public readonly partial struct LegacyStatus
				{
					public string Value { get; }

					private LegacyStatus(string value) => Value = value;
				}
			}
			""";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var warnings = GetGeneratorDiagnostics(result)
			.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning)
			.ToArray();

		await Assert
			.That(warnings.Select(static diagnostic => diagnostic.Id))
			.Contains(GeneratorDiagnostics.ScalarShouldBeRecordStruct.Id);
	}

	[Test]
	public async Task ScalarGeneration_GeneratesEnumConvenienceProperties(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Purview.EventSourcing.Serialization.Scalar]
				public readonly partial record struct ReportProcessingStatus
				{
					public ReportProcessingStatusCode Value { get; }

					private ReportProcessingStatus(ReportProcessingStatusCode value) => Value = value;
				}

				public enum ReportProcessingStatusCode
				{
					Uploaded,
					Processing,
					Completed,
					Failed
				}

				public static class StatusHarness
				{
					public static bool AreEqual() =>
						ReportProcessingStatus.Failed == ReportProcessingStatus.Hydrate(ReportProcessingStatusCode.Failed);
				}
			}
			""";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetGeneratedSource(result);

		await Assert
			.That(generatedSource)
			.Contains(
				"public static global::Testing.ReportProcessingStatus Uploaded => Hydrate(global::Testing.ReportProcessingStatusCode.Uploaded);"
			);
		await Assert
			.That(generatedSource)
			.Contains(
				"public static global::Testing.ReportProcessingStatus Processing => Hydrate(global::Testing.ReportProcessingStatusCode.Processing);"
			);
		await Assert
			.That(generatedSource)
			.Contains(
				"public static global::Testing.ReportProcessingStatus Completed => Hydrate(global::Testing.ReportProcessingStatusCode.Completed);"
			);
		await Assert
			.That(generatedSource)
			.Contains(
				"public static global::Testing.ReportProcessingStatus Failed => Hydrate(global::Testing.ReportProcessingStatusCode.Failed);"
			);

		var assembly = await CompileToAssemblyAsync(source, cancellationToken);
		var harnessType = assembly.GetType("Testing.StatusHarness")!;
		var areEqual = (bool)harnessType.GetMethod("AreEqual")!.Invoke(null, null)!;

		await Assert.That(areEqual).IsTrue();
	}

	[Test]
	public async Task ScalarGeneration_CanDisableEnumConvenienceProperties(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Purview.EventSourcing.Serialization.Scalar(GenerateEnumProperties = false)]
				public readonly partial record struct ReportProcessingStatus
				{
					public ReportProcessingStatusCode Value { get; }

					private ReportProcessingStatus(ReportProcessingStatusCode value) => Value = value;
				}

				public enum ReportProcessingStatusCode
				{
					Uploaded,
					Processing,
					Completed,
					Failed
				}
			}
			""";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetGeneratedSource(result);

		await Assert
			.That(generatedSource)
			.DoesNotContain(
				"public static global::Testing.ReportProcessingStatus Uploaded => Hydrate(global::Testing.ReportProcessingStatusCode.Uploaded);"
			);
		await Assert
			.That(generatedSource)
			.DoesNotContain(
				"public static global::Testing.ReportProcessingStatus Processing => Hydrate(global::Testing.ReportProcessingStatusCode.Processing);"
			);
		await Assert
			.That(generatedSource)
			.DoesNotContain(
				"public static global::Testing.ReportProcessingStatus Completed => Hydrate(global::Testing.ReportProcessingStatusCode.Completed);"
			);
		await Assert
			.That(generatedSource)
			.DoesNotContain(
				"public static global::Testing.ReportProcessingStatus Failed => Hydrate(global::Testing.ReportProcessingStatusCode.Failed);"
			);
	}

	[Test]
	public async Task ComplexValueObjectGeneration_UsesObjectShapedJson(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{

			[Purview.EventSourcing.Serialization.Scalar]
			public readonly partial record struct CurrencyCode
			{
				public string Value { get; }

				private CurrencyCode(string value) => Value = value;

				static partial void OnNormalize(ref string value)
				{
					value = value?.Trim().ToUpperInvariant()!;
				}

				static partial void OnValidate(string value)
				{
					if (string.IsNullOrWhiteSpace(value) || value.Length != 3)
						throw new System.ArgumentException("Invalid currency code.", nameof(value));
				}
			}

			[Purview.EventSourcing.Serialization.ValueObject]
			public readonly partial record struct Money
			{
				public decimal Amount { get; }

				public CurrencyCode Currency { get; }

				private Money(decimal amount, CurrencyCode currency)
				{
					Amount = amount;
					Currency = currency;
				}

				public static Money Create(decimal amount, CurrencyCode currency)
				{
					if (amount < 0)
						throw new System.ArgumentOutOfRangeException(nameof(amount));

					return new(amount, currency);
				}
			}

			public static class ComplexHarness
			{
				public static string SerializeMoney()
				{
					var options = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
					return System.Text.Json.JsonSerializer.Serialize(Money.Create(10.50m, CurrencyCode.Create("GBP")), options);
				}

				public static decimal DeserializeAmount()
				{
					var options = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
					var money = System.Text.Json.JsonSerializer.Deserialize<Money>("{\"amount\":10.5,\"currency\":\"GBP\"}", options);
					return money.Amount;
				}

				public static int CompareMoney() => Money.Create(10.5m, CurrencyCode.Create("GBP")).CompareTo(Money.Create(11m, CurrencyCode.Create("GBP")));
			}
			}
			""";

		var assembly = await CompileToAssemblyAsync(source, cancellationToken);
		var harnessType = assembly.GetType("Testing.ComplexHarness")!;

		var json = (string)harnessType.GetMethod("SerializeMoney")!.Invoke(null, null)!;
		var amount = (decimal)harnessType.GetMethod("DeserializeAmount")!.Invoke(null, null)!;
		var compareResult = (int)harnessType.GetMethod("CompareMoney")!.Invoke(null, null)!;

		await Assert.That(json).Contains("\"amount\"");
		await Assert.That(json).Contains("\"currency\"");
		await Assert.That(amount).IsEqualTo(10.5m);
		await Assert.That(compareResult).IsEqualTo(-1);
	}

	[Test]
	public async Task ComplexValueObjectGeneration_GeneratesPrivateConstructorAndEqualityWhenMissing(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{

			[Purview.EventSourcing.Serialization.ValueObject]
			public partial class Address
			{
				public string Line1 { get; }

				public string City { get; }
			}

			public static class AddressHarness
			{
				public static bool AreEqual() => Address.Hydrate("1 Example Street", "London") == Address.Hydrate("1 Example Street", "London");

				public static string City() => Address.Hydrate("1 Example Street", "London").City;
			}
			}
			""";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetGeneratedSource(result);

		await Assert.That(generatedSource).Contains("private Address(string line1, string city)");
		await Assert.That(generatedSource).Contains("public bool Equals(global::Testing.Address other)");
		await Assert
			.That(generatedSource)
			.Contains("public static bool operator ==(global::Testing.Address left, global::Testing.Address right)");

		var assembly = await CompileToAssemblyAsync(source, cancellationToken);
		var harnessType = assembly.GetType("Testing.AddressHarness")!;

		var areEqual = (bool)harnessType.GetMethod("AreEqual")!.Invoke(null, null)!;
		var city = (string)harnessType.GetMethod("City")!.Invoke(null, null)!;

		await Assert.That(areEqual).IsTrue();
		await Assert.That(city).IsEqualTo("London");
	}

	[Test]
	public async Task ScalarJsonStrictMode_UsesCreateOnDeserialization(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{

				[Scalar(DeserializationMode = ValueObjectDeserializationMode.Strict)]
				public readonly partial record struct StrictEmailAddress
				{
					public string Value { get; }

					private StrictEmailAddress(string value) => Value = value;

					static partial void OnValidate(string value)
					{
						if (!value.Contains("@", System.StringComparison.Ordinal))
							throw new System.ArgumentException("Invalid email address.", nameof(value));
					}
				}

				public static class StrictHarness
				{
					public static string DeserializeValid() => System.Text.Json.JsonSerializer.Deserialize<StrictEmailAddress>("\"test@example.com\"").Value;

					public static void DeserializeInvalid() => _ = System.Text.Json.JsonSerializer.Deserialize<StrictEmailAddress>("\"not-an-email\"");
				}
			}
			""";

		var assembly = await CompileToAssemblyAsync(source, cancellationToken);
		var harnessType = assembly.GetType("Testing.StrictHarness")!;

		var valid = (string)harnessType.GetMethod("DeserializeValid")!.Invoke(null, null)!;

		var threw = false;
		try
		{
			harnessType.GetMethod("DeserializeInvalid")!.Invoke(null, null);
		}
		catch (TargetInvocationException ex) when (ex.InnerException is ArgumentException)
		{
			threw = true;
		}
		catch (TargetInvocationException ex)
			when (ex.InnerException is System.Text.Json.JsonException jsonException
				&& jsonException.InnerException is ArgumentException
			)
		{
			threw = true;
		}

		await Assert.That(valid).IsEqualTo("test@example.com");
		await Assert.That(threw).IsTrue();
	}

	[Test]
	public async Task ScalarComparable_GeneratesRelationalAndPrimitiveEqualityOperators(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Purview.EventSourcing.Serialization.Scalar]
				public readonly partial record struct Name
				{
					public string Value { get; }

					private Name(string value) => Value = value;
				}
			}
			""";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetGeneratedSource(result);

		await Assert.That(generatedSource).Contains("public static bool operator <(");
		await Assert.That(generatedSource).Contains("public static bool operator >(");
		await Assert.That(generatedSource).Contains("public static bool operator <=(");
		await Assert.That(generatedSource).Contains("public static bool operator >=(");
		await Assert.That(generatedSource).Contains("return left.CompareTo(right) < 0;");
		await Assert.That(generatedSource).Contains("return left.CompareTo(right) > 0;");
		await Assert.That(generatedSource).Contains("return left.CompareTo(right) <= 0;");
		await Assert.That(generatedSource).Contains("return left.CompareTo(right) >= 0;");
		await Assert.That(generatedSource).Contains("operator <(global::Testing.Name left, string right)");
		await Assert.That(generatedSource).Contains("operator >(global::Testing.Name left, string right)");
		await Assert.That(generatedSource).Contains("operator <=(global::Testing.Name left, string right)");
		await Assert.That(generatedSource).Contains("operator >=(global::Testing.Name left, string right)");
		await Assert.That(generatedSource).Contains("public bool Equals(string other)");
		await Assert.That(generatedSource).Contains("operator ==(global::Testing.Name left, string right)");
		await Assert.That(generatedSource).Contains("operator !=(global::Testing.Name left, string right)");
		await Assert.That(generatedSource).Contains("operator ==(string left, global::Testing.Name right)");
		await Assert.That(generatedSource).Contains("operator !=(string left, global::Testing.Name right)");
		await Assert
			.That(generatedSource)
			.DoesNotContain("operator <(global::System.String left, global::Testing.Name right)");
		await Assert
			.That(generatedSource)
			.DoesNotContain("operator >(global::System.String left, global::Testing.Name right)");
		await Assert
			.That(generatedSource)
			.DoesNotContain("operator <=(global::System.String left, global::Testing.Name right)");
		await Assert
			.That(generatedSource)
			.DoesNotContain("operator >=(global::System.String left, global::Testing.Name right)");
	}

	[Test]
	public async Task ScalarGeneration_GeneratesSelfEqualityOperatorsForPlainStruct(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Purview.EventSourcing.Serialization.Scalar]
				public readonly partial struct ReportProcessingStatus
				{
					public ReportProcessingStatusCode Value { get; }

					private ReportProcessingStatus(ReportProcessingStatusCode value) => Value = value;
				}

				public enum ReportProcessingStatusCode
				{
					Uploaded,
					Processing,
					Completed,
					Failed
				}

				public static class StatusHarness
				{
					public static bool AreEqual()
					{
						ReportProcessingStatus status = ReportProcessingStatus.Hydrate(ReportProcessingStatusCode.Failed);
						var other = ReportProcessingStatus.Hydrate(ReportProcessingStatusCode.Failed);
						return status == other;
					}
				}
			}
			""";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetGeneratedSource(result);

		await Assert
			.That(generatedSource)
			.Contains(
				"public static bool operator ==(global::Testing.ReportProcessingStatus left, global::Testing.ReportProcessingStatus right)"
			);
		await Assert
			.That(generatedSource)
			.Contains(
				"public static bool operator !=(global::Testing.ReportProcessingStatus left, global::Testing.ReportProcessingStatus right)"
			);

		var assembly = await CompileToAssemblyAsync(source, cancellationToken);
		var harnessType = assembly.GetType("Testing.StatusHarness")!;

		var areEqual = (bool)harnessType.GetMethod("AreEqual")!.Invoke(null, null)!;

		await Assert.That(areEqual).IsTrue();
	}

	[Test]
	public async Task Scalar_GenerateComparisonOperatorsFalse_GeneratesCompareToOnly(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Purview.EventSourcing.Serialization.Scalar(GenerateComparisonOperators = false)]
				public readonly partial record struct Name
				{
					public string Value { get; }

					private Name(string value) => Value = value;
				}
			}
			""";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetGeneratedSource(result);

		await Assert.That(generatedSource).Contains("public int CompareTo(global::Testing.Name other)");
		await Assert.That(generatedSource).Contains("public int CompareTo(string? other)");
		await Assert.That(generatedSource).DoesNotContain("public static bool operator <(");
		await Assert.That(generatedSource).DoesNotContain("public static bool operator >(");
		await Assert.That(generatedSource).DoesNotContain("public static bool operator <=(");
		await Assert.That(generatedSource).DoesNotContain("public static bool operator >=(");
	}

	[Test]
	public async Task Scalar_GenerateComparableFalse_SuppressesCompareToAndOperators(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Purview.EventSourcing.Serialization.Scalar(GenerateComparable = false, GenerateComparisonOperators = true)]
				public readonly partial record struct Name
				{
					public string Value { get; }

					private Name(string value) => Value = value;
				}
			}
			""";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetGeneratedSource(result);

		await Assert.That(generatedSource).DoesNotContain("public int CompareTo(");
		await Assert.That(generatedSource).DoesNotContain("public static bool operator <(");
		await Assert.That(generatedSource).DoesNotContain("public static bool operator >(");
		await Assert.That(generatedSource).DoesNotContain("public static bool operator <=(");
		await Assert.That(generatedSource).DoesNotContain("public static bool operator >=(");
	}

	[Test]
	public async Task ValueObjectComparable_GeneratesSelfRelationalOperators(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[Purview.EventSourcing.Serialization.ValueObject]
				public readonly partial record struct Money
				{
					public decimal Amount { get; }

					private Money(decimal amount)
					{
						Amount = amount;
					}
				}
			}
			""";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetGeneratedSource(result);

		await Assert
			.That(generatedSource)
			.Contains("operator <(global::Testing.Money left, global::Testing.Money right)");
		await Assert
			.That(generatedSource)
			.Contains("operator >(global::Testing.Money left, global::Testing.Money right)");
		await Assert
			.That(generatedSource)
			.Contains("operator <=(global::Testing.Money left, global::Testing.Money right)");
		await Assert
			.That(generatedSource)
			.Contains("operator >=(global::Testing.Money left, global::Testing.Money right)");
	}

	[Test]
	public async Task ComplexValueObjectGeneration_SupportsRecordClassesWithMultipleProperties(
		CancellationToken cancellationToken
	)
	{
		const string source = """
			namespace Testing
			{
				[Purview.EventSourcing.Serialization.ValueObject]
				public sealed partial record UserDetails
				{
					public System.Guid Id { get; }

					public string DisplayName { get; }

					partial void OnValidate(System.Guid id, string displayName)
					{
						if (id == System.Guid.Empty)
							throw new System.ArgumentException("Id must be a valid GUID.", nameof(id));

						if (string.IsNullOrWhiteSpace(displayName))
							throw new System.ArgumentException("DisplayName cannot be null or empty.", nameof(displayName));
					}
				}

				[Purview.EventSourcing.Serialization.ValueObject]
				public sealed partial record UserDetails2(System.Guid Id, string DisplayName)
				{
					partial void OnValidate(System.Guid id, string displayName)
					{
						if (id == System.Guid.Empty)
							throw new System.ArgumentException("Id must be a valid GUID.", nameof(id));

						if (string.IsNullOrWhiteSpace(displayName))
							throw new System.ArgumentException("DisplayName cannot be null or empty.", nameof(displayName));
					}
				}

				public static class UserDetailsHarness
				{
					public static bool UserDetailsValidationThrows()
					{
						try
						{
							_ = UserDetails.Create(System.Guid.Empty, "Display");
							return false;
						}
						catch (System.ArgumentException)
						{
							return true;
						}
					}

					public static bool UserDetails2ValidationThrows()
					{
						try
						{
							_ = UserDetails2.Create(System.Guid.Empty, "Display");
							return false;
						}
						catch (System.ArgumentException)
						{
							return true;
						}
					}

					public static int UserDetailsHashSetCount()
					{
						var id = System.Guid.Parse("11111111-1111-1111-1111-111111111111");
						var values = new System.Collections.Generic.HashSet<UserDetails>
						{
							UserDetails.Create(id, "Alice"),
							UserDetails.Create(id, "Alice"),
							UserDetails.Create(System.Guid.Parse("22222222-2222-2222-2222-222222222222"), "Alice")
						};
						return values.Count;
					}

					public static int UserDetails2HashSetCount()
					{
						var id = System.Guid.Parse("11111111-1111-1111-1111-111111111111");
						var values = new System.Collections.Generic.HashSet<UserDetails2>
						{
							UserDetails2.Create(id, "Alice"),
							UserDetails2.Create(id, "Alice"),
							UserDetails2.Create(System.Guid.Parse("22222222-2222-2222-2222-222222222222"), "Alice")
						};
						return values.Count;
					}

					public static int CompareUserDetails() =>
						UserDetails.Create(System.Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alice")
							.CompareTo(UserDetails.Create(System.Guid.Parse("22222222-2222-2222-2222-222222222222"), "Alice"));

					public static int CompareUserDetails2() =>
						UserDetails2.Create(System.Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alice")
							.CompareTo(UserDetails2.Create(System.Guid.Parse("22222222-2222-2222-2222-222222222222"), "Alice"));
				}
			}
			""";

		var assembly = await CompileToAssemblyAsync(source, cancellationToken);
		var harnessType = assembly.GetType("Testing.UserDetailsHarness")!;

		var userDetailsValidationThrows = (bool)
			harnessType.GetMethod("UserDetailsValidationThrows")!.Invoke(null, null)!;
		var userDetails2ValidationThrows = (bool)
			harnessType.GetMethod("UserDetails2ValidationThrows")!.Invoke(null, null)!;
		var userDetailsHashSetCount = (int)harnessType.GetMethod("UserDetailsHashSetCount")!.Invoke(null, null)!;
		var userDetails2HashSetCount = (int)harnessType.GetMethod("UserDetails2HashSetCount")!.Invoke(null, null)!;
		var compareUserDetails = (int)harnessType.GetMethod("CompareUserDetails")!.Invoke(null, null)!;
		var compareUserDetails2 = (int)harnessType.GetMethod("CompareUserDetails2")!.Invoke(null, null)!;

		await Assert.That(userDetailsValidationThrows).IsTrue();
		await Assert.That(userDetails2ValidationThrows).IsTrue();
		await Assert.That(userDetailsHashSetCount).IsEqualTo(2);
		await Assert.That(userDetails2HashSetCount).IsEqualTo(2);
		await Assert.That(compareUserDetails).IsEqualTo(-1);
		await Assert.That(compareUserDetails2).IsEqualTo(-1);
	}
}
