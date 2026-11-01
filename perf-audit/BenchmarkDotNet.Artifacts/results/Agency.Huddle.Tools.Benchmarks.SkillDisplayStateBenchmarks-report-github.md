```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host]     : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v4


```
| Method | Mean     | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------- |---------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| Old    | 3.727 μs | 0.0435 μs | 0.0407 μs |  1.00 |    0.02 | 0.6676 |  10.94 KB |        1.00 |
| New    | 2.559 μs | 0.0387 μs | 0.0362 μs |  0.69 |    0.01 | 0.3319 |   5.47 KB |        0.50 |
