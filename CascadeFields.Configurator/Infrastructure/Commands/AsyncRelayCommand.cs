using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CascadeFields.Configurator.Infrastructure.Commands
{
    /// <summary>
    /// Asynchronous implementation of the ICommand pattern for binding long-running operations to UI commands.
    /// Prevents command re-execution while an async operation is in progress and provides automatic UI state management.
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b></para>
    /// AsyncRelayCommand enables asynchronous operations (like loading metadata from Dataverse, publishing configurations,
    /// or querying data) to be invoked from UI commands while maintaining responsive UI and preventing concurrent execution.
    ///
    /// <para><b>Key Features:</b></para>
    /// <list type="bullet">
    ///     <item><description><b>Execution Prevention:</b> Automatically disables the command while an async operation is running</description></item>
    ///     <item><description><b>UI Responsiveness:</b> Uses async/await so the UI thread doesn't block during long operations</description></item>
    ///     <item><description><b>Automatic State Management:</b> Sets _isExecuting flag and raises CanExecuteChanged events automatically</description></item>
    ///     <item><description><b>Error Safety:</b> Uses try/finally to ensure _isExecuting is reset even if the operation throws</description></item>
    /// </list>
    ///
    /// <para><b>Usage Example:</b></para>
    /// <code>
    /// public class MyViewModel
    /// {
    ///     public ICommand LoadDataCommand { get; }
    ///
    ///     public MyViewModel(IMetadataService metadataService)
    ///     {
    ///         LoadDataCommand = new AsyncRelayCommand(
    ///             execute: async () => await LoadDataAsync(),
    ///             canExecute: () => IsConnected);
    ///     }
    ///
    ///     private async Task LoadDataAsync()
    ///     {
    ///         // Long-running async operation
    ///         var entities = await metadataService.GetEntitiesAsync();
    ///         Entities = entities;
    ///     }
    /// }
    /// </code>
    ///
    /// <para><b>Execution Flow:</b></para>
    /// <list type="number">
    ///     <item><description>User clicks button bound to AsyncRelayCommand</description></item>
    ///     <item><description>CanExecute is checked (returns false if already executing)</description></item>
    ///     <item><description>_isExecuting set to true, CanExecuteChanged raised (button disables)</description></item>
    ///     <item><description>Async operation executes without blocking UI</description></item>
    ///     <item><description>Finally block sets _isExecuting to false, CanExecuteChanged raised (button re-enables)</description></item>
    /// </list>
    ///
    /// <para><b>Common Use Cases:</b></para>
    /// <list type="bullet">
    ///     <item><description>Loading metadata from Dataverse (entities, attributes, relationships)</description></item>
    ///     <item><description>Publishing plugin configurations via SDK</description></item>
    ///     <item><description>Querying existing configurations from the database</description></item>
    ///     <item><description>Any operation that takes time and should not block the UI</description></item>
    /// </list>
    /// </remarks>
    public class AsyncRelayCommand : ICommand
    {
        /// <summary>
        /// The asynchronous function to execute when the command is invoked.
        /// </summary>
        private readonly Func<object, Task> _execute;

        /// <summary>
        /// Optional predicate that determines whether the command can execute (in addition to the isExecuting check).
        /// </summary>
        private readonly Predicate<object>? _canExecute;

        /// <summary>
        /// Flag indicating whether an async operation is currently executing.
        /// Prevents concurrent execution and automatically disables the command during execution.
        /// </summary>
        private bool _isExecuting;

        /// <summary>
        /// Occurs when changes occur that affect whether the command should execute.
        /// Automatically raised when execution starts and completes.
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncRelayCommand"/> class with a parameter-accepting async function.
        /// </summary>
        /// <param name="execute">
        /// The asynchronous function to execute when the command is invoked. Cannot be null.
        /// Receives the command parameter and returns a Task representing the async operation.
        /// </param>
        /// <param name="canExecute">
        /// Optional predicate that determines whether the command can execute (checked in addition to the isExecuting flag).
        /// If null, command can execute when not already executing.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown if execute is null.</exception>
        public AsyncRelayCommand(Func<object, Task> execute, Predicate<object>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncRelayCommand"/> class with a parameterless async function.
        /// Convenience constructor for commands that don't need a parameter.
        /// </summary>
        /// <param name="execute">
        /// The asynchronous function to execute when the command is invoked. Cannot be null.
        /// Returns a Task representing the async operation.
        /// </param>
        /// <param name="canExecute">
        /// Optional function that determines whether the command can execute (checked in addition to the isExecuting flag).
        /// If null, command can execute when not already executing.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown if execute is null.</exception>
        /// <remarks>
        /// This constructor adapts the parameterless Func&lt;Task&gt; and Func&lt;bool&gt; to the parameter-accepting signatures
        /// by wrapping them in lambdas that ignore the parameter.
        /// </remarks>
        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
            : this(
                execute != null ? (Func<object, Task>)(_ => execute()) : throw new ArgumentNullException(nameof(execute)),
                canExecute != null ? (Predicate<object>)(_ => canExecute()) : null)
        {
        }

        /// <summary>
        /// Determines whether the command can execute in its current state.
        /// </summary>
        /// <param name="parameter">Data used by the command. Can be ignored if the command doesn't require a parameter.</param>
        /// <returns>
        /// <c>true</c> if the command can execute; otherwise, <c>false</c>.
        /// Returns <c>false</c> if an async operation is currently executing (prevents concurrent execution).
        /// </returns>
        /// <remarks>
        /// <para><b>Execution Prevention:</b></para>
        /// This method automatically returns false while _isExecuting is true, preventing the command from
        /// being invoked again until the current async operation completes. This protects against concurrent
        /// execution issues and provides automatic button disable/enable behavior.
        ///
        /// <para><b>CanExecute Predicate:</b></para>
        /// If a canExecute predicate was provided, it is also evaluated. Both conditions must be true
        /// for the command to execute (_isExecuting must be false AND canExecute must return true).
        /// </remarks>
        public bool CanExecute(object parameter)
        {
            return !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);
        }

        /// <summary>
        /// Executes the command's asynchronous operation.
        /// Automatically manages execution state and raises CanExecuteChanged events.
        /// </summary>
        /// <param name="parameter">Data used by the command. Passed to the execute function provided in the constructor.</param>
        /// <remarks>
        /// <para><b>Async void Pattern:</b></para>
        /// This method is intentionally async void (required by ICommand.Execute). The async operation is awaited internally,
        /// and any exceptions will propagate through the async void method (typically handled by the application's
        /// unhandled exception handler).
        ///
        /// <para><b>Execution Flow:</b></para>
        /// <list type="number">
        ///     <item><description>CanExecute is checked; if false, method returns immediately without executing</description></item>
        ///     <item><description>_isExecuting is set to true and CanExecuteChanged is raised (button disables)</description></item>
        ///     <item><description>The async function is awaited (UI remains responsive)</description></item>
        ///     <item><description>Finally block ensures _isExecuting is reset to false and CanExecuteChanged is raised (button re-enables)</description></item>
        /// </list>
        ///
        /// <para><b>Error Handling:</b></para>
        /// The try/finally pattern ensures that _isExecuting is reset even if the async operation throws an exception.
        /// This prevents the command from becoming permanently disabled due to errors.
        /// </remarks>
        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter))
                return;

            _isExecuting = true;
            RaiseCanExecuteChanged();

            try
            {
                await _execute(parameter);
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Manually raises the <see cref="CanExecuteChanged"/> event to force reevaluation of <see cref="CanExecute"/>.
        /// </summary>
        /// <remarks>
        /// This method is called automatically when execution starts and completes.
        /// You can also call it manually when application state changes that affect the canExecute predicate
        /// (e.g., when a property the predicate depends on changes).
        /// </remarks>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
