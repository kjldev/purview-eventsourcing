using Purview.EventSourcing.Serialization;

namespace Purview.EventSourcing.Samples.ValueObjects;

[ValueObject]
public readonly partial record struct Money
{
	public decimal Amount { get; }

	public CurrencyCode Currency { get; }

	Money(decimal amount, CurrencyCode currency)
	{
		Amount = amount;
		Currency = currency;
	}

	partial void OnValidate(decimal amount, CurrencyCode currency)
	{
		if (amount < 0)
			throw new ArgumentOutOfRangeException(nameof(amount), "Amount cannot be negative.");

		if (currency == CurrencyCode.Empty)
			throw new ArgumentException("Currency cannot be empty.", nameof(currency));
	}
}
