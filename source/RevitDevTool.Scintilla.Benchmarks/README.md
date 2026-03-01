# RevitDevTool.Scintilla.Benchmarks

BenchmarkDotNet suite so sánh trực tiếp:

- `Serilog + RichTextBox`
- `ZLogger + Scintilla`

## Workload Groups

- `FullPipeline`: ingest + drain + paint hash (apple-to-apple cho trải nghiệm hiển thị).
- `Core`: ingest/filter/search logic không cộng pixel draw.
- `Pixel`: paint-only cost (WM_PRINTCLIENT hash), tách riêng draw bottleneck.

## Run Commands

```powershell
.\source\RevitDevTool.Scintilla.Benchmarks\run-benchmarks.ps1 -Suite full
```

```powershell
.\source\RevitDevTool.Scintilla.Benchmarks\run-benchmarks.ps1 -Suite core
```

```powershell
.\source\RevitDevTool.Scintilla.Benchmarks\run-benchmarks.ps1 -Suite pixel
```

```powershell
.\source\RevitDevTool.Scintilla.Benchmarks\run-benchmarks.ps1 -Suite all -RunRegressionGuard -RegressionTolerancePercent 5
```

Artifacts mặc định nằm ở `BenchmarkDotNet.Artifacts`. Có thể override bằng biến môi trường `RDT_BENCH_ARTIFACTS`.

## Regression Guard

`check-regression.ps1` đọc report CSV FullPipeline và fail-fast nếu Scintilla chậm hơn ngoài tolerance:

- Append (`ZLogger + Scintilla` vs `Serilog + RichTextBox`)
- Colorized (`ZLogger + Scintilla` vs `Serilog + RichTextBox`)
- Filter/Search (`Scintilla` vs RichTextBox)

```powershell
.\source\RevitDevTool.Scintilla.Benchmarks\check-regression.ps1 -TolerancePercent 5
```
