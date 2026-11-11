using Lattice.Cli;

using var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

return await LatticeCli.RunAsync(
    args,
    Console.In,
    Console.Out,
    Console.Error,
    Console.IsInputRedirected,
    cancellationSource.Token);
