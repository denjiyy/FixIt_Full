using System.Collections.Concurrent;

namespace FixIt.Mobile.Services;

/// <summary>
/// Lightweight thread-safe key-value cache with a fixed capacity. When full,
/// the oldest entry is evicted on the next insert. Preserves insertion order
/// via a queue, which makes "oldest" precise without per-access bookkeeping.
///
/// Replaces the unbounded Dictionary&lt;,&gt; caches in ApiService that would
/// have grown without limit over a long mobile session. LRU semantics are
/// overkill here — a small capacity (~50) means even FIFO eviction is fine
/// for typical browse-back-and-forth navigation.
/// </summary>
public sealed class BoundedCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly ConcurrentDictionary<TKey, TValue> _items;
    private readonly Queue<TKey> _insertionOrder;
    private readonly object _evictionLock = new();

    public BoundedCache(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
        }
        _capacity = capacity;
        _items = new ConcurrentDictionary<TKey, TValue>();
        _insertionOrder = new Queue<TKey>(capacity);
    }

    public int Count => _items.Count;

    public bool TryGetValue(TKey key, out TValue? value)
    {
        if (_items.TryGetValue(key, out var v))
        {
            value = v;
            return true;
        }
        value = default;
        return false;
    }

    public TValue? GetValueOrDefault(TKey key) =>
        _items.TryGetValue(key, out var v) ? v : default;

    public TValue this[TKey key]
    {
        get => _items[key];
        set => Set(key, value);
    }

    public bool Remove(TKey key)
    {
        // Don't bother updating the FIFO queue — eviction is per-insert and
        // queue can carry stale keys harmlessly (Set/Remove are idempotent).
        return _items.TryRemove(key, out _);
    }

    public void Set(TKey key, TValue value)
    {
        // Evict from queue first if the key is new — otherwise updating an
        // existing key would still trigger eviction unnecessarily.
        var isNew = !_items.ContainsKey(key);
        _items[key] = value;

        if (isNew)
        {
            lock (_evictionLock)
            {
                _insertionOrder.Enqueue(key);
                while (_insertionOrder.Count > _capacity)
                {
                    var oldest = _insertionOrder.Dequeue();
                    _items.TryRemove(oldest, out _);
                }
            }
        }
    }

    public void Clear()
    {
        lock (_evictionLock)
        {
            _items.Clear();
            _insertionOrder.Clear();
        }
    }
}
