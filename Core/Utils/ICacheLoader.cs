namespace Cthangover.Core.Utils
{
    public interface ICacheLoader<TKey, TValue>
    {
        TValue Load(TKey key);
    }
}
