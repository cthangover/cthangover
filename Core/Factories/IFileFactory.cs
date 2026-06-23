namespace Cthangover.Core.Factories
{
    public interface IFileFactory<out T> where T : class, IIdentifiable
    {
        string GroupName { get; }
        T Get(string id);
    }
}
