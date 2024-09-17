namespace CustomAssetsInjector.Actions;

public interface IAction
{
    /// <summary>
    /// Execute the action.
    /// </summary>
    public void Execute();
    /// <summary>
    /// Revert anything performed in the Execute method.
    /// </summary>
    public void Revert();
}