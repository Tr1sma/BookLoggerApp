using System;
using System.Threading.Tasks;

namespace BookLoggerApp.Core.Services.Abstractions
{
    public interface IBackButtonService
    {
        /// <summary>
        /// Registers a back-button handler. Handlers run LIFO; returning true stops propagation.
        /// </summary>
        void Register(Func<Task<bool>> handler);

        /// <summary>Unregisters a previously registered handler.</summary>
        void Unregister(Func<Task<bool>> handler);

        /// <summary>
        /// Invokes the registered handlers. Returns true if handled, false otherwise.
        /// </summary>
        Task<bool> HandleBackAsync();
    }
}
