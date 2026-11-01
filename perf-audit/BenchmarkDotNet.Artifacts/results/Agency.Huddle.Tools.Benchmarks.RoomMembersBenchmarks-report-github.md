```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host]     : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v4


```
| Method | Mean      | Error    | StdDev   | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------- |----------:|---------:|---------:|------:|--------:|-------:|-------:|----------:|------------:|
| Old    | 159.62 μs | 3.072 μs | 3.017 μs |  1.00 |    0.03 | 2.4414 |      - |  43.57 KB |        1.00 |
| New    |  43.16 μs | 0.517 μs | 0.458 μs |  0.27 |    0.01 | 1.5259 | 0.0610 |  25.27 KB |        0.58 |
