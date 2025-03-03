using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Primitives;
using Purview.EventSourcing.Aggregates.Persistence.Events;

namespace Purview.EventSourcing.Aggregates.Persistence;

public sealed class PersistenceAggregate : AggregateBase, IAggregateTest
{
	[Range(0, int.MaxValue)]
	public int IncrementInt32 { get; private set; }

	public int Int32Value { get; private set; }

	public Guid? OldEventValue { get; private set; }

	public string StringProperty { get; private set; } = default!;

	public Dictionary<string, StringValues> StringValuesDictionary { get; } = [];

	public Dictionary<string, string> StringsDictionary { get; } = [];

	public ComplexTestType? ComplexTestType { get; private set; }

	public void RegisterOldEventType()
	{
		Register<OldEvent>(Apply);
	}

	protected override void RegisterEvents()
	{
		Register<IncrementInt32ValueEvent>(_ => IncrementInt32++);
		Register<SetIncrementEvent>(@event => IncrementInt32 = @event.Value);
		Register<SetInt32ValueEvent>(@event => Int32Value = @event.Value);
		Register<StringValueEvent>(@event => StringProperty += @event.Value);
		Register<SetComplexPropertyEvent>(@event => ComplexTestType = @event.ComplexProperty);
		Register<AddStringValuesDictionaryKVPsEvent>(Apply);
		Register<AddStringDictionaryKVPsEvent>(Apply);
	}

	void Apply(AddStringValuesDictionaryKVPsEvent obj)
	{
		foreach (var kvp in obj.KVPs)
			StringValuesDictionary.Add(kvp.Key, kvp.Value);
	}

	void Apply(AddStringDictionaryKVPsEvent obj)
	{
		foreach (var kvp in obj.KVPs)
			StringsDictionary.Add(kvp.Key, kvp.Value);
	}

	void Apply(OldEvent @event) => OldEventValue = @event.Value;

	public void SetValidatedProperty(int value)
		=> RecordAndApply(new SetIncrementEvent { Value = value });

	public void IncrementInt32Value()
		=> RecordAndApply(new IncrementInt32ValueEvent());

	public void SetInt32Value(int value)
	{
		if (Int32Value != value)
			RecordAndApply(new SetInt32ValueEvent { Value = value });
	}

	public void AppendString(string value)
	{
		RecordAndApply(new StringValueEvent
		{
			Value = value
		});
	}

	public void AddKVPs(params KeyValuePair<string, StringValues>[] pairs)
		=> RecordAndApply(new AddStringValuesDictionaryKVPsEvent { KVPs = pairs });

	public void AddKVPs(params KeyValuePair<string, string>[] pairs)
		=> RecordAndApply(new AddStringDictionaryKVPsEvent { KVPs = pairs });

	public void SetOldEventValue(Guid value)
	{
		if (value == Guid.Empty)
			throw new ArgumentException("Don't use an empty guid, just for clarity.");

		RecordAndApply(new OldEvent
		{
			Value = value
		});
	}

	public void SetComplexProperty(ComplexTestType complexTestType)
		=> RecordAndApply(new SetComplexPropertyEvent { ComplexProperty = complexTestType });
}
