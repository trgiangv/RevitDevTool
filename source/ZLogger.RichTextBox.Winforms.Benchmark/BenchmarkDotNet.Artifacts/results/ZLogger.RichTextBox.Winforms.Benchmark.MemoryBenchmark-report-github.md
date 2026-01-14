```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7462/25H2/2025Update/HudsonValley2)
Intel Core Ultra 7 265K 3.90GHz, 1 CPU, 20 logical and 20 physical cores
.NET SDK 9.0.307
  [Host] : .NET 8.0.22 (8.0.22, 8.0.2225.52707), X64 RyuJIT x86-64-v3

Runtime=.NET 8.0  

```
| Method                           | Job       | Toolchain              | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|--------------------------------- |---------- |----------------------- |---------:|---------:|---------:|------:|--------:|-----:|---------:|---------:|-----------:|------------:|
| &#39;Serilog - String Interpolation&#39; | .NET 8.0  | Default                |       NA |       NA |       NA |     ? |       ? |    ? |       NA |       NA |         NA |           ? |
| &#39;ZLogger - String Interpolation&#39; | .NET 8.0  | Default                |       NA |       NA |       NA |     ? |       ? |    ? |       NA |       NA |         NA |           ? |
| &#39;Serilog - Value Types Only&#39;     | .NET 8.0  | Default                |       NA |       NA |       NA |     ? |       ? |    ? |       NA |       NA |         NA |           ? |
| &#39;ZLogger - Value Types Only&#39;     | .NET 8.0  | Default                |       NA |       NA |       NA |     ? |       ? |    ? |       NA |       NA |         NA |           ? |
| &#39;Serilog - Large String&#39;         | .NET 8.0  | Default                |       NA |       NA |       NA |     ? |       ? |    ? |       NA |       NA |         NA |           ? |
| &#39;ZLogger - Large String&#39;         | .NET 8.0  | Default                |       NA |       NA |       NA |     ? |       ? |    ? |       NA |       NA |         NA |           ? |
| &#39;Serilog - Dictionary&#39;           | .NET 8.0  | Default                |       NA |       NA |       NA |     ? |       ? |    ? |       NA |       NA |         NA |           ? |
| &#39;ZLogger - Dictionary&#39;           | .NET 8.0  | Default                |       NA |       NA |       NA |     ? |       ? |    ? |       NA |       NA |         NA |           ? |
| &#39;Serilog - Nested Exception&#39;     | .NET 8.0  | Default                |       NA |       NA |       NA |     ? |       ? |    ? |       NA |       NA |         NA |           ? |
| &#39;ZLogger - Nested Exception&#39;     | .NET 8.0  | Default                |       NA |       NA |       NA |     ? |       ? |    ? |       NA |       NA |         NA |           ? |
|                                  |           |                        |          |          |          |       |         |      |          |          |            |             |
| &#39;Serilog - String Interpolation&#39; | InProcess | InProcessEmitToolchain | 319.2 μs |  2.56 μs |  2.40 μs |  1.00 |    0.01 |    4 |  33.2031 |  16.6016 |  515.63 KB |        1.00 |
| &#39;ZLogger - String Interpolation&#39; | InProcess | InProcessEmitToolchain | 523.0 μs |  4.79 μs |  4.00 μs |  1.64 |    0.02 |    6 |  56.6406 |  28.3203 |  874.92 KB |        1.70 |
| &#39;Serilog - Value Types Only&#39;     | InProcess | InProcessEmitToolchain | 252.4 μs |  1.74 μs |  1.62 μs |  0.79 |    0.01 |    3 |  32.2266 |  16.1133 |     500 KB |        0.97 |
| &#39;ZLogger - Value Types Only&#39;     | InProcess | InProcessEmitToolchain | 563.7 μs |  3.73 μs |  3.49 μs |  1.77 |    0.02 |    7 |  55.6641 |  27.3438 |  866.41 KB |        1.68 |
| &#39;Serilog - Large String&#39;         | InProcess | InProcessEmitToolchain | 207.8 μs |  1.48 μs |  1.38 μs |  0.65 |    0.01 |    1 |  22.9492 |  11.4746 |  353.54 KB |        0.69 |
| &#39;ZLogger - Large String&#39;         | InProcess | InProcessEmitToolchain | 718.2 μs | 13.67 μs | 13.43 μs |  2.25 |    0.04 |    9 | 221.6797 | 220.7031 | 3408.23 KB |        6.61 |
| &#39;Serilog - Dictionary&#39;           | InProcess | InProcessEmitToolchain | 603.5 μs |  7.17 μs |  6.70 μs |  1.89 |    0.02 |    8 |  83.9844 |  41.9922 | 1297.41 KB |        2.52 |
| &#39;ZLogger - Dictionary&#39;           | InProcess | InProcessEmitToolchain | 739.5 μs |  7.32 μs |  6.85 μs |  2.32 |    0.03 |    9 |  92.7734 |  45.8984 |  1422.4 KB |        2.76 |
| &#39;Serilog - Nested Exception&#39;     | InProcess | InProcessEmitToolchain | 214.8 μs |  0.92 μs |  0.77 μs |  0.67 |    0.01 |    2 |  24.4141 |  12.2070 |  375.38 KB |        0.73 |
| &#39;ZLogger - Nested Exception&#39;     | InProcess | InProcessEmitToolchain | 398.3 μs |  1.34 μs |  1.25 μs |  1.25 |    0.01 |    5 |  48.8281 |  24.4141 |  750.38 KB |        1.46 |

Benchmarks with issues:
  MemoryBenchmark.'Serilog - String Interpolation': .NET 8.0(Runtime=.NET 8.0)
  MemoryBenchmark.'ZLogger - String Interpolation': .NET 8.0(Runtime=.NET 8.0)
  MemoryBenchmark.'Serilog - Value Types Only': .NET 8.0(Runtime=.NET 8.0)
  MemoryBenchmark.'ZLogger - Value Types Only': .NET 8.0(Runtime=.NET 8.0)
  MemoryBenchmark.'Serilog - Large String': .NET 8.0(Runtime=.NET 8.0)
  MemoryBenchmark.'ZLogger - Large String': .NET 8.0(Runtime=.NET 8.0)
  MemoryBenchmark.'Serilog - Dictionary': .NET 8.0(Runtime=.NET 8.0)
  MemoryBenchmark.'ZLogger - Dictionary': .NET 8.0(Runtime=.NET 8.0)
  MemoryBenchmark.'Serilog - Nested Exception': .NET 8.0(Runtime=.NET 8.0)
  MemoryBenchmark.'ZLogger - Nested Exception': .NET 8.0(Runtime=.NET 8.0)
