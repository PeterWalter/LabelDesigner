# ADR-0003 — Syncfusion Ribbon Command Re-entrancy

**Status**: Accepted  
**Date**: 2026-05-27

---

## Context

Clicking "Preview" on the Syncfusion ribbon caused an unhandled `System.InvalidCastException` inside `Syncfusion.Ribbon.WinUI.dll`, terminating the application. The exception was not caught by the ViewModel's try-catch because it did not originate inside the async method body.

---

## Root cause

The `PreviewPrint` ribbon button is bound to a `RelayCommand` (sync) that fires `_ = PreviewPrintAsync()`. When no CSV data source is loaded, `PreviewPrintAsync` reached `await dialog.ShowAsync()` **without encountering any prior await point**, meaning it ran entirely on the original call stack — inside Syncfusion's button-click handler.

Opening a modal `ContentDialog` mid-stack, while Syncfusion's click handler was still executing, caused Syncfusion's post-click state cleanup to encounter an unexpected modal state and throw `InvalidCastException`.

---

## Decision

Add `await Task.Yield()` as the **first statement** of every `async Task` method that is called fire-and-forget from a sync `RelayCommand` wrapper and may show UI (dialogs, pickers, windows) before its first natural await point.

```csharp
private async Task PreviewPrintAsync()
{
    // Yield so Syncfusion finishes its button-click processing before UI opens.
    await Task.Yield();
    ...
}
```

`Task.Yield()` schedules the continuation as a new work item on the current `SynchronizationContext` (the UI thread dispatcher), returning control immediately to the caller (Syncfusion). Syncfusion completes its handler; on the next dispatcher frame, `PreviewPrintAsync` resumes and safely shows the dialog.

---

## General rule for Syncfusion ribbon buttons

All `[RelayCommand]` methods on `DesignerViewModel` that wrap async operations **must not** make UI calls (dialog, file picker, window) synchronously before the first await. Either:

1. **`await Task.Yield()`** at the top, or  
2. **`await Task.Delay(0)`** (equivalent), or  
3. Ensure the async method's first statement is a naturally asynchronous call (e.g., `await BuildMailMergePrintDocumentsAsync(ds)` when data source is present).

Option 1 is preferred for clarity.

---

## Why `AsyncRelayCommand` is not used

Syncfusion ribbon buttons internally cast the bound `ICommand` to a Syncfusion-specific type that does not accept `IAsyncRelayCommand`. Using `[RelayCommand]` on an `async Task` method generates `AsyncRelayCommand`, which causes a separate `InvalidCastException` during command binding. Therefore, all async ribbon actions use the sync-wrapper pattern:

```csharp
[RelayCommand]
private void DoSomething()          // generates RelayCommand (sync) ✓
{
    _ = DoSomethingAsync();
}

private async Task DoSomethingAsync()
{
    await Task.Yield();             // required if UI may open before first await
    ...
}
```

---

## Consequences

- Every async ribbon action needs the `await Task.Yield()` guard reviewed if UI is shown.
- The pattern is established and consistent — new ribbon actions should follow the same template.
- No impact on test coverage (tests call `DoSomethingAsync()` directly, not via the command).

---

## Related files

| File | Role |
|------|------|
| `LabelDesigner.App/ViewModels/DesignerViewModel.cs` | All sync wrapper + async pairs; `PreviewPrintAsync` has the `Task.Yield()` fix |
