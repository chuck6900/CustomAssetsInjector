using System.Collections.Generic;
using CustomAssetsInjector.Actions;

namespace CustomAssetsInjector.Services;

public class StateManager
{
    private Stack<IAction> m_UndoStack = new();
    private Stack<IAction> m_RedoStack = new();

    public void Reset()
    {
        m_UndoStack.Clear();
        m_RedoStack.Clear();
    }
    
    public void ExecuteAction(IAction action)
    {
        action.Execute();
        m_UndoStack.Push(action);
        
        // clear redo stack after running the new action
        m_RedoStack.Clear();
    }
    
    public void Undo()
    {
        if (m_UndoStack.Count == 0)
            return;
        
        var actionToRevert = m_UndoStack.Pop();
        actionToRevert.Revert();
        
        m_RedoStack.Push(actionToRevert);
    }

    public void Redo()
    {
        if (m_RedoStack.Count == 0)
            return;
        
        var actionToPerform = m_RedoStack.Pop();
        actionToPerform.Execute();
        
        m_UndoStack.Push(actionToPerform);
    }
}