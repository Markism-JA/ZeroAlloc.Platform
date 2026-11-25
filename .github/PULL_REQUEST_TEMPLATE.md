## Summary

<!-- Describe what this PR changes and why. Link the related issue if applicable. -->

## Checklist

- [ ] Built cleanly with zero warnings (`dotnet build ZeroAlloc.Platform.slnx --no-incremental -c Release`, including `CS1591` XML docs)
- [ ] All tests pass in both `Release` and `Debug` (`dotnet test ZeroAlloc.Platform.slnx`)
- [ ] Verified compatibility with both `net10.0` (`[LibraryImport]`) and `netstandard2.1` (`[DllImport]`)
- [ ] Added/updated unit or concurrency tests for behavior, P/Invoke, or `unsafe` changes
- [ ] Added BenchmarkDotNet numbers if claiming performance improvements
