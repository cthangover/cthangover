namespace Cthangover.Core.Factories
{
    public interface IPrefabFactory<out T>
    {
        string GroupName { get; }

        T Get(string id);
    }
}
