using System;
using System.Threading.Tasks;

namespace BookLoggerApp.Core.Services.Abstractions
{
    public interface IBackButtonService
    {
        /// <summary>
        /// Registers a handler for the back button.
        /// Handlers are invoked for the last registered (LIFO) to the first.
        /// If a handler returns true, the back action is considered handled and propagation stops.
        /// </summary>
        void Register(Func<Task<bool>> handler);
        
        /// <summary>
        /// Unregisters a previously registered handler.
        /// </summary>
        void Unregister(Func<Task<bool>> handler);

        /// <summary>
        /// Invokes the registered handlers. Returns true if handled, false otherwise.
        /// </summary>
        Task<bool> HandleBackAsync();
    }
}
