using FishChamp.Data.Models;
using Remora.Rest.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FishChamp.Tracker;

public interface IInstanceTracker<T> where T : class
{
    void Add(Snowflake id, T instance);
    bool TryRemove(Snowflake id, out T? instance);

    bool TryGetValue(Snowflake id, out T instance);
    IReadOnlyList<T> GetAll();
}

public class InstanceTracker<T> : IInstanceTracker<T> where T : class
{
    private readonly Dictionary<Snowflake, T> _instances = new();
    private readonly object _lock = new();

    public void Add(Snowflake id, T instance)
    {
        lock (_lock)
        {
            _instances.Add(id, instance);
        }
    }

    public bool TryRemove(Snowflake id, [NotNullWhen(true)] out T? instance)
    {
        lock (_lock)
        {
            if (_instances.TryGetValue(id, out instance))
            {
                _instances.Remove(id);
                return true;
            }

            return false;
        }
    }

    public bool TryGetValue(Snowflake id, out T instance)
    {
        lock (_lock)
        {
            return _instances.TryGetValue(id, out instance);
        }
    }

    public IReadOnlyList<T> GetAll()
    {
        lock (_lock)
        {
            return _instances.Values.ToList();
        }
    }
}
