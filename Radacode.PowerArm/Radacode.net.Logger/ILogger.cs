namespace radacode.net.logger
{
    public interface ILogger
    {
        void Log(string message);

        void Error(string message, string stackTrace);
    }
}
