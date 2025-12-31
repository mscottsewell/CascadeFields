using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CascadeFields.Configurator.ViewModels
{
    /// <summary>
    /// Abstract base class for all ViewModels in the CascadeFields Configurator.
    /// Implements the INotifyPropertyChanged pattern to support data binding in Windows Forms applications.
    /// </summary>
    /// <remarks>
    /// <para><b>MVVM Pattern with Windows Forms:</b></para>
    /// This class enables a Model-View-ViewModel (MVVM) architecture within the Windows Forms XrmToolBox plugin.
    /// While MVVM is more commonly associated with WPF, this implementation adapts the pattern for Windows Forms
    /// by using INotifyPropertyChanged for property binding and ICommand for user actions.
    ///
    /// <para><b>Usage:</b></para>
    /// All ViewModel classes in the application should inherit from this base class to gain automatic
    /// property change notification capabilities. Use <see cref="SetProperty{T}"/> in property setters
    /// to automatically raise PropertyChanged events when values change.
    ///
    /// <para><b>Benefits:</b></para>
    /// <list type="bullet">
    ///     <item><description>Separation of concerns: UI logic in ViewModels, presentation in Views (Controls)</description></item>
    ///     <item><description>Testability: ViewModels can be unit tested without UI dependencies</description></item>
    ///     <item><description>Automatic UI updates: Controls bound to ViewModel properties update automatically via data binding</description></item>
    ///     <item><description>CallerMemberName: Compile-time property name safety with automatic capture</description></item>
    /// </list>
    /// </remarks>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        /// <summary>
        /// Occurs when a property value changes. Subscribers (UI controls) are notified to refresh their display.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event to notify subscribers that a property value has changed.
        /// </summary>
        /// <param name="propertyName">
        /// The name of the property that changed. This parameter is optional and automatically provided
        /// by the compiler when called from a property setter using [CallerMemberName].
        /// </param>
        /// <remarks>
        /// <para><b>Automatic Property Name Capture:</b></para>
        /// The [CallerMemberName] attribute causes the compiler to automatically pass the calling property's name.
        /// This eliminates magic strings and provides compile-time safety for property names.
        ///
        /// <para><b>Example Usage:</b></para>
        /// <code>
        /// private string _name;
        /// public string Name
        /// {
        ///     get => _name;
        ///     set
        ///     {
        ///         _name = value;
        ///         OnPropertyChanged(); // propertyName automatically set to "Name"
        ///     }
        /// }
        /// </code>
        ///
        /// <para><b>Virtual Method:</b></para>
        /// This method is virtual to allow derived classes to add custom logic when properties change,
        /// such as raising additional related property notifications or triggering command reevaluation.
        /// </remarks>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Sets the value of a property's backing field and raises <see cref="PropertyChanged"/> if the value actually changed.
        /// Provides optimized property setter implementation with automatic change detection and notification.
        /// </summary>
        /// <typeparam name="T">The type of the property being set.</typeparam>
        /// <param name="field">
        /// Reference to the backing field that stores the property's value.
        /// This field is updated if the new value differs from the current value.
        /// </param>
        /// <param name="value">The new value to assign to the property.</param>
        /// <param name="propertyName">
        /// The name of the property. Automatically captured via [CallerMemberName] when called from a property setter.
        /// </param>
        /// <returns>
        /// <c>true</c> if the value was changed and PropertyChanged was raised; <c>false</c> if the value was already equal and no change occurred.
        /// </returns>
        /// <remarks>
        /// <para><b>Change Detection:</b></para>
        /// Uses <see cref="EqualityComparer{T}.Default"/> for efficient, type-appropriate equality comparison.
        /// If the new value equals the current value, no assignment or notification occurs (optimization).
        ///
        /// <para><b>Recommended Usage Pattern:</b></para>
        /// <code>
        /// private string _entityName;
        /// public string EntityName
        /// {
        ///     get => _entityName;
        ///     set => SetProperty(ref _entityName, value);
        /// }
        /// </code>
        ///
        /// <para><b>Benefits:</b></para>
        /// <list type="bullet">
        ///     <item><description>Eliminates boilerplate: Reduces property setters to a single line</description></item>
        ///     <item><description>Prevents unnecessary notifications: Only raises PropertyChanged when value actually changes</description></item>
        ///     <item><description>Type-safe: Generic implementation works with any property type</description></item>
        ///     <item><description>Compile-time safety: CallerMemberName ensures correct property name is passed</description></item>
        /// </list>
        /// </remarks>
        protected bool SetProperty<T>(
            ref T field,
            T value,
            [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
