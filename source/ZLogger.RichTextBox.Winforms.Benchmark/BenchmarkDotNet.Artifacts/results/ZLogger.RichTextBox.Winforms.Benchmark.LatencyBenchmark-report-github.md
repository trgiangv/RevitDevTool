```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7462/25H2/2025Update/HudsonValley2)
Intel Core Ultra 7 265K 3.90GHz, 1 CPU, 20 logical and 20 physical cores
.NET SDK 9.0.307
  [Host] : .NET 8.0.22 (8.0.22, 8.0.2225.52707), X64 RyuJIT x86-64-v3

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method            | SampleCount | Mean | Error | Ratio | RatioSD | Rank | Alloc Ratio |
|------------------ |------------ |-----:|------:|------:|--------:|-----:|------------:|
| &#39;Serilog Latency&#39; | 100         |   NA |    NA |     ? |       ? |    ? |           ? |

Benchmarks with issues:
  LatencyBenchmark.'Serilog Latency': .NET 8.0(Runtime=.NET 8.0) [SampleCount=100]
