namespace CryptoSoft;

public static class Program
{
    private const int InvalidArgumentsExitCode = -2;
    private const int BusyExitCode = -20;
    private const string SingleInstanceMutexName = @"Global\ProSoft.EasySave.CryptoSoft";

    public static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: CryptoSoft <filePath> <key>");
            Environment.Exit(InvalidArgumentsExitCode);
        }

        var hasLock = false;
        using var singleInstanceMutex = new Mutex(false, SingleInstanceMutexName);

        try
        {
            try
            {
                hasLock = singleInstanceMutex.WaitOne(0, false);
            }
            catch (AbandonedMutexException)
            {
                hasLock = true;
            }

            if (!hasLock)
            {
                Console.WriteLine("CryptoSoft is already running.");
                Environment.Exit(BusyExitCode);
            }

            var fileManager = new FileManager(args[0], args[1]);
            var elapsedTime = fileManager.TransformFile();
            Console.WriteLine($"ElapsedTimeMs={elapsedTime}");
            Environment.Exit(elapsedTime);
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception.Message);
            Environment.Exit(-99);
        }
        finally
        {
            if (hasLock)
            {
                singleInstanceMutex.ReleaseMutex();
            }
        }
    }
}
