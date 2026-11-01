```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.402
  [Host]     : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v4


```
| Method | Mean         | Error      | StdDev     | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------- |-------------:|-----------:|-----------:|------:|--------:|-------:|-------:|----------:|------------:|
| Old    | 1,576.821 ns | 31.5651 ns | 50.9718 ns | 1.001 |    0.04 | 0.4082 | 0.0019 |    6848 B |        1.00 |
| New    |     7.079 ns |  0.2169 ns |  0.6119 ns | 0.004 |    0.00 |      - |      - |         - |        0.00 |
