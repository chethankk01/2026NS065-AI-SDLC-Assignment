using SpendWise.Infrastructure;

return CliHost.Run(args, Directory.GetCurrentDirectory(), new SystemClock(), Console.Out, Console.Error);
