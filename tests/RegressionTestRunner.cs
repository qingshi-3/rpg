internal static class RegressionTestRunner
{
    internal static void Run(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
            Environment.ExitCode = 1;
        }
    }
}
