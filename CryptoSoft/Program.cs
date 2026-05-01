namespace CryptoSoft;

public static class Program
{
    public static void Main(string[] args)
    {
        try
        {
            foreach (var arg in args)
            {
                Console.WriteLine(arg);
            }

            var fileManager = new FileManager(args[0], args[1]);
            int elapsedTime = fileManager.TransformFile();
            Console.WriteLine($"ElapsedTimeMs={elapsedTime}");
            Environment.Exit(elapsedTime);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            Environment.Exit(-99);
        }
    }
}
