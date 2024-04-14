namespace RTArchiver;

public class UniqueList<T>
{
	readonly List<T> _items = new List<T>();
	readonly HashSet<T> _uniqueItems = new HashSet<T>();

	public void Add(T item)
	{
		if (_uniqueItems.Add(item))
			_items.Add(item);
	}

	public T this[int index] => _items[index];
}
