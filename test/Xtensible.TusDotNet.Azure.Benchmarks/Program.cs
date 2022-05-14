using BenchmarkDotNet.Running;
using Microsoft.Extensions.Configuration;
using Xtensible.TusDotNet.Azure;
using Xtensible.TusDotNet.Azure.Benchmarks;


public static class Program
{
    public static async Task Main(string[] args)
    {

        BenchmarkRunner.Run<Benchmarks>();
    }
}
