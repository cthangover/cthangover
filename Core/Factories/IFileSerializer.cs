namespace Cthangover.Core.Factories
{

    public interface IFileSerializer<T> where T : class
    {
        T Read(string content, string resourcePath = null);
    }

}
