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
		await Assert.That(comparePrimitive).IsEqualTo(1);
		await Assert.That(compareObject).IsEqualTo(-1);
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
	public async Task ScalarJsonStrictMode_UsesCreateOnDeserialization(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{

			[Purview.EventSourcing.Serialization.Scalar(DeserializationMode = Purview.EventSourcing.Serialization.ValueObjectDeserializationMode.Strict)]
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
	public async Task ScalarComparable_GeneratesRelationalOperatorsWithoutEqualityMembers(
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
		await Assert.That(generatedSource).DoesNotContain("operator ==(");
		await Assert.That(generatedSource).DoesNotContain("operator !=(");
		await Assert.That(generatedSource).DoesNotContain("override bool Equals(");
		await Assert.That(generatedSource).DoesNotContain("override int GetHashCode(");
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
}
