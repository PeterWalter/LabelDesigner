namespace LabelDesigner.Core.Interfaces;

public interface IUndoRedoService
{
    void Execute(IUndoableCommand command);
    bool CanUndo { get; }
    bool CanRedo { get; }
    string? UndoDescription { get; }
    string? RedoDescription { get; }
    void Undo();
    void Redo();
    void Clear();
}

public interface IUndoableCommand
{
    void Execute();
    void Undo();
    string Description { get; }
}
