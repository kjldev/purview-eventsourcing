using System.Collections;

namespace Purview.EventSourcing;

/// <summary>
/// Event-sourced list collection.
/// Exposes read-only semantics to consumers while providing IList access for EF materialization.
/// </summary>
public sealed class EventStoreList<T> : IList<T>, IReadOnlyList<T>, IList
{
	readonly List<T> _items;

	public EventStoreList()
	{
		_items = [];
	}

	public EventStoreList(int capacity) => _items = [with(capacity)];

	public EventStoreList(IEnumerable<T> items)
	{
		ArgumentNullException.ThrowIfNull(items);
		_items = [.. items];
	}

	public int Count => _items.Count;

	public T this[int index] => _items[index];

	bool ICollection<T>.IsReadOnly => false;

	T IList<T>.this[int index]
	{
		get => _items[index];
		set => _items[index] = value;
	}

	public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	void ICollection<T>.Add(T item) => _items.Add(item);

	void ICollection<T>.Clear() => _items.Clear();

	bool ICollection<T>.Contains(T item) => _items.Contains(item);

	void ICollection<T>.CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

	bool ICollection<T>.Remove(T item) => _items.Remove(item);

	int IList<T>.IndexOf(T item) => _items.IndexOf(item);

	void IList<T>.Insert(int index, T item) => _items.Insert(index, item);

	void IList<T>.RemoveAt(int index) => _items.RemoveAt(index);

	bool IList.IsFixedSize => false;

	bool IList.IsReadOnly => false;

	object ICollection.SyncRoot => this;

	bool ICollection.IsSynchronized => false;

	object? IList.this[int index]
	{
		get => _items[index];
		set
		{
			ArgumentNullException.ThrowIfNull(value);
			_items[index] = (T)value;
		}
	}

	int IList.Add(object? value)
	{
		ArgumentNullException.ThrowIfNull(value);
		_items.Add((T)value);
		return _items.Count - 1;
	}

	bool IList.Contains(object? value) => value is T typed && _items.Contains(typed);

	int IList.IndexOf(object? value) => value is T typed ? _items.IndexOf(typed) : -1;

	void IList.Insert(int index, object? value)
	{
		ArgumentNullException.ThrowIfNull(value);
		_items.Insert(index, (T)value);
	}

	void IList.Remove(object? value)
	{
		if (value is T typed)
			_items.Remove(typed);
	}

	void IList.Clear() => _items.Clear();

	void IList.RemoveAt(int index) => _items.RemoveAt(index);

	void ICollection.CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
}

/// <summary>
/// Event-sourced set collection.
/// Enforces uniqueness while providing IList access for EF materialization.
/// </summary>
public sealed class EventStoreSet<T> : IList<T>, IReadOnlySet<T>, IReadOnlyList<T>, IList
{
	readonly List<T> _items;
	readonly HashSet<T> _set;

	public EventStoreSet()
	{
		_items = [];
		_set = [];
	}

	public EventStoreSet(int capacity)
	{
		_items = [with(capacity)];
		_set = [];
	}

	public EventStoreSet(IEnumerable<T> items)
		: this(items, comparer: null) { }

	public EventStoreSet(IEnumerable<T> items, IEqualityComparer<T>? comparer)
	{
		ArgumentNullException.ThrowIfNull(items);
		_items = [];
		_set = [with(comparer)];

		foreach (var item in items)
			AddUnique(item);
	}

	public int Count => _items.Count;

	public T this[int index] => _items[index];

	bool ICollection<T>.IsReadOnly => false;

	T IList<T>.this[int index]
	{
		get => _items[index];
		set
		{
			var existing = _items[index];
			if (EqualityComparer<T>.Default.Equals(existing, value))
				return;

			if (_set.Contains(value))
				throw new InvalidOperationException("EventStoreSet does not allow duplicate values.");

			_set.Remove(existing);
			_items[index] = value;
			_set.Add(value);
		}
	}

	public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	void ICollection<T>.Add(T item) => AddUnique(item);

	void ICollection<T>.Clear()
	{
		_items.Clear();
		_set.Clear();
	}

	public bool Contains(T item) => _set.Contains(item);

	bool ICollection<T>.Contains(T item) => _set.Contains(item);

	void ICollection<T>.CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

	bool ICollection<T>.Remove(T item)
	{
		if (!_set.Remove(item))
			return false;

		_items.Remove(item);
		return true;
	}

	int IList<T>.IndexOf(T item) => _items.IndexOf(item);

	void IList<T>.Insert(int index, T item)
	{
		if (_set.Contains(item))
			return;

		_items.Insert(index, item);
		_set.Add(item);
	}

	void IList<T>.RemoveAt(int index)
	{
		var item = _items[index];
		_items.RemoveAt(index);
		_set.Remove(item);
	}

	public bool IsProperSubsetOf(IEnumerable<T> other) => _set.IsProperSubsetOf(other);

	public bool IsProperSupersetOf(IEnumerable<T> other) => _set.IsProperSupersetOf(other);

	public bool IsSubsetOf(IEnumerable<T> other) => _set.IsSubsetOf(other);

	public bool IsSupersetOf(IEnumerable<T> other) => _set.IsSupersetOf(other);

	public bool Overlaps(IEnumerable<T> other) => _set.Overlaps(other);

	public bool SetEquals(IEnumerable<T> other) => _set.SetEquals(other);

	bool IList.IsFixedSize => false;

	bool IList.IsReadOnly => false;

	object ICollection.SyncRoot => this;

	bool ICollection.IsSynchronized => false;

	object? IList.this[int index]
	{
		get => _items[index];
		set
		{
			ArgumentNullException.ThrowIfNull(value);
			((IList<T>)this)[index] = (T)value;
		}
	}

	int IList.Add(object? value)
	{
		ArgumentNullException.ThrowIfNull(value);
		var typed = (T)value;
		if (_set.Contains(typed))
			return _items.IndexOf(typed);

		_items.Add(typed);
		_set.Add(typed);
		return _items.Count - 1;
	}

	bool IList.Contains(object? value) => value is T typed && _set.Contains(typed);

	int IList.IndexOf(object? value) => value is T typed ? _items.IndexOf(typed) : -1;

	void IList.Insert(int index, object? value)
	{
		ArgumentNullException.ThrowIfNull(value);
		((IList<T>)this).Insert(index, (T)value);
	}

	void IList.Remove(object? value)
	{
		if (value is T typed)
			((ICollection<T>)this).Remove(typed);
	}

	void IList.Clear() => ((ICollection<T>)this).Clear();

	void IList.RemoveAt(int index) => ((IList<T>)this).RemoveAt(index);

	void ICollection.CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

	void AddUnique(T item)
	{
		if (!_set.Add(item))
			return;

		_items.Add(item);
	}
}
