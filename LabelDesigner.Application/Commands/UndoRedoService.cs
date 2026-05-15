using LabelDesigner.Core.Interfaces;

namespace LabelDesigner.Application.Commands;

public class UndoRedoService : IUndoRedoService
{
    private readonly Stack<IUndoableCommand> _undoStack = new();
    private readonly Stack<IUndoableCommand> _redoStack = new();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public string? UndoDescription => _undoStack.TryPeek(out var c) ? c.Description : null;
    public string? RedoDescription => _redoStack.TryPeek(out var c) ? c.Description : null;

    public void Execute(IUndoableCommand command)
    {
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();
    }

    public void Undo()
    {
        if (!_undoStack.TryPop(out var cmd)) return;
        cmd.Undo();
        _redoStack.Push(cmd);
    }

    public void Redo()
    {
        if (!_redoStack.TryPop(out var cmd)) return;
        cmd.Execute();
        _undoStack.Push(cmd);
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}
