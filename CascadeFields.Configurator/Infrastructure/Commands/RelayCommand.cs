using System;
using System.Windows.Input;

namespace CascadeFields.Configurator.Infrastructure.Commands
{
    /// <summary>
    /// Synchronous implementation of the ICommand pattern for binding user actions (button clicks, menu items) to ViewModel methods.
    /// Provides a simple, testable way to handle UI commands without code-behind in Windows Forms applications.
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b></para>
    /// RelayCommand "relays" execution to a delegate (Action) when the command is invoked. This enables the MVVM pattern
    /// in Windows Forms by keeping UI action logic in the ViewModel rather than in event handlers in the View.
    ///
    /// <para><b>Usage in MVVM:</b></para>
    /// ViewModels expose ICommand properties (RelayCommand instances) that are bound to UI button clicks.
    /// When the button is clicked, the command's Execute method runs, which invokes the delegate provided in the constructor.
    ///
    /// <para><b>CanExecute Support:</b></para>
    /// The optional canExecute predicate determines whether the command can currently execute.
    /// This enables automatic button enable/disable based on application state (e.g., disable Save until data is valid).
    ///
    /// <para><b>Example:</b></para>
    /// <code>
    /// public class MyViewModel
    /// {
    ///     public ICommand SaveCommand { get; }
    ///
    ///     public MyViewModel()
    ///     {
    ///         SaveCommand = new RelayCommand(
    ///             execute: () => SaveData(),
    ///             canExecute: () => IsDataValid);
    ///     }
    ///
    ///     private void SaveData() { /* save logic */ }
    ///     private bool IsDataValid => !string.IsNullOrEmpty(Name);
    /// }
    /// </code>
    ///
    /// <para><b>Windows Forms Integration:</b></para>
    /// This implementation uses a custom CommandManager (not WPF's) to avoid dependencies on WPF assemblies.
    /// This makes the command usable in pure Windows Forms / XrmToolBox plugin environments.
    /// </remarks>
    public class RelayCommand : ICommand
    {
        /// <summary>
        /// The action to execute when the command is invoked.
        /// </summary>
        private readonly Action<object> _execute;

        /// <summary>
        /// Optional predicate that determines whether the command can execute in its current state.
        /// </summary>
        private readonly Predicate<object>? _canExecute;

        /// <summary>
        /// Occurs when changes occur that affect whether the command should execute.
        /// Subscribers (typically UI controls) should reevaluate <see cref="CanExecute"/> when this event is raised.
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RelayCommand"/> class with a parameter-accepting action.
        /// </summary>
        /// <param name="execute">
        /// The action to execute when the command is invoked. Receives the command parameter (typically from the UI).
        /// Cannot be null.
        /// </param>
        /// <param name="canExecute">
        /// Optional predicate that determines whether the command can execute. If null, command can always execute.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown if execute is null.</exception>
        public RelayCommand(Action<object> execute, Predicate<object>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RelayCommand"/> class with a parameterless action.
        /// Convenience constructor for commands that don't need a parameter.
        /// </summary>
        /// <param name="execute">
        /// The action to execute when the command is invoked. Cannot be null.
        /// </param>
        /// <param name="canExecute">
        /// Optional function that determines whether the command can execute. If null, command can always execute.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown if execute is null.</exception>
        /// <remarks>
        /// This constructor adapts the parameterless Action and Func&lt;bool&gt; to the parameter-accepting signatures
        /// by wrapping them in lambdas that ignore the parameter.
        /// </remarks>
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
            : this(
                execute != null ? (Action<object>)(_ => execute()) : throw new ArgumentNullException(nameof(execute)),
                canExecute != null ? (Predicate<object>)(_ => canExecute()) : null)
        {
        }

        /// <summary>
        /// Determines whether the command can execute in its current state.
        /// </summary>
        /// <param name="parameter">
        /// Data used by the command. Can be ignored if the command doesn't require a parameter.
        /// </param>
        /// <returns>
        /// <c>true</c> if the command can execute; otherwise, <c>false</c>.
        /// Returns <c>true</c> if no canExecute predicate was provided (command always enabled).
        /// </returns>
        /// <remarks>
        /// UI controls typically call this method to determine whether to enable or disable themselves.
        /// For example, a button bound to this command will be disabled when CanExecute returns false.
        /// </remarks>
        public bool CanExecute(object parameter)
        {
            return _canExecute?.Invoke(parameter) ?? true;
        }

        /// <summary>
        /// Executes the command's action.
        /// </summary>
        /// <param name="parameter">
        /// Data used by the command. Passed to the execute action provided in the constructor.
        /// </param>
        /// <remarks>
        /// This method is called by the UI framework when the user interacts with a control bound to this command
        /// (e.g., clicks a button). It should only be called if <see cref="CanExecute"/> returns true.
        /// </remarks>
        public void Execute(object parameter)
        {
            _execute(parameter);
        }

        /// <summary>
        /// Manually raises the <see cref="CanExecuteChanged"/> event to force reevaluation of <see cref="CanExecute"/>.
        /// </summary>
        /// <remarks>
        /// Call this method when application state changes in a way that affects whether the command can execute.
        /// For example, if CanExecute depends on a property value, call this method when that property changes.
        /// This causes bound UI controls to update their enabled/disabled state.
        /// </remarks>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// Lightweight helper class that provides command reevaluation infrastructure without WPF dependencies.
    /// Allows RelayCommand and AsyncRelayCommand to work in Windows Forms / XrmToolBox environments.
    /// </summary>
    /// <remarks>
    /// This class mimics the minimal functionality of WPF's CommandManager.RequerySuggested event.
    /// It allows commands to notify subscribers when their CanExecute state may have changed,
    /// without requiring the full WPF framework.
    /// </remarks>
    internal static class CommandManager
    {
        public static event EventHandler? RequerySuggested;

        public static void InvalidateRequerySuggested()
        {
            RequerySuggested?.Invoke(null, EventArgs.Empty);
        }
    }
}
