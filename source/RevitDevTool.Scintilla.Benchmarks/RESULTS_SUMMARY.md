# Benchmark Summary

## Benchmark Suites

### 1. HeadlessPipelineBenchmarks
- **Mục tiêu:** So sánh Serilog vs ZLogger + Scintilla (không có UI)
- **Params:** MessageSizeBytes (64, 256, 1024), TokenDensity, StructuredPayload, BatchSize

### 2. LoggerPipelineBenchmarks
- **Mục tiêu:** So sánh Serilog vs ZLogger MEL pipeline
- **Params:** MessageSizeBytes, TokenDensity, StructuredPayload

### 3. UiControlBenchmarks
- **Mục tiêu:** RichTextBox vs ScintillaNET text append
- **Params:** MessageSizeBytes, TokenDensity, StructuredPayload

### 4. UiAttachedBenchmarks
- **Mục tiêu:** Full combo comparison
  - Serilog + RichTextBox
  - ZLogger(MEL) + RichTextBox
  - Serilog + ScintillaNET
  - ZLogger(MEL) + ScintillaNET
- **Params:** MessageSizeBytes, TokenDensity, StructuredPayload

### 5. FilterStressBenchmarks (NEW)
- **Mục tiêu:** Đo filter performance với large dataset
- **Test cases:**
  - Filter: ASCII lowercase
  - Filter: ASCII match case
  - Filter: URL substring
  - Filter: Token
  - Filter: Level Error
  - Filter: Level + Text combined
  - Clear filter
  - Clear all data
- **Params:** DatasetSize (1K, 10K, 50K), MessageSizeBytes, TokenDensity

## Cách Chạy Benchmark

```powershell
# Chạy tất cả
dotnet run --project source/RevitDevTool.Scintilla.Benchmarks -c Release

# Chạy một benchmark cụ thể
dotnet run --project source/RevitDevTool.Scintilla.Benchmarks -c Release -- --filter "FilterStress*"

# Chạy với params cụ thể
dotnet run --project source/RevitDevTool.Scintilla.Benchmarks -c Release -- --filter "*" --param DatasetSize=10000
```

## Kết Quả Mong Đợi

### Hiệu năng mong đợi của ZLogger + Scintilla vs Serilog + RichTextBox:

| Scenario | Expected ZLogger+Scintilla | Expected Serilog+RTB |
|----------|---------------------------|---------------------|
| Single-thread plain | ~10-20% faster | Baseline |
| Multi-threaded | ~30-50% faster | Baseline |
| Structured JSON | ~50-100% faster | Slower (formatting overhead) |
| Filter 10K entries | <100ms | N/A |
| Filter 50K entries | <500ms | N/A |

## Các Tối Đã Áp Dụng

1. **Buffer Copy Optimization:** Giảm từ 2 copies xuống 1 copy
2. **URL Scanner:** Span-based thay regex
3. **Filter Pre-computation:** UTF-8 bytes được compute sẵn
4. **Thread-local buffers:** Tránh contention

## Nhận xét

- ZLogger + Scintilla được tối ưu cho high-throughput scenarios
- Structured JSON là điểm mạnh của ZLogger do zero-allocation formatting
- Filter performance phụ thuộc vào dataset size và filter complexity
