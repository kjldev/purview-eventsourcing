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

	public static Money Create(decimal amount, CurrencyCode currency)
	{
		return amount < 0
			? throw new ArgumentOutOfRangeException(nameof(amount), "Amount cannot be negative.")
			: new(amount, currency);
	}
}
