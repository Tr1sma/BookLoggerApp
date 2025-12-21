using BookLoggerApp.Core.Services.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BookLoggerApp.Infrastructure.Services
{
    public class BackButtonService : IBackButtonService
    {
        private readonly List<Func<Task<bool>>> _handlers = new();
        private readonly object _lock = new();

        public void Register(Func<Task<bool>> handler)
        {
            if (handler == null) return;
            lock (_lock)
            {
                _handlers.Add(handler);
                System.Diagnostics.Debug.WriteLine($"[BackButtonService] Registered handler. Count: {_handlers.Count}");
            }
        }

        public void Unregister(Func<Task<bool>> handler)
        {
            if (handler == null) return;
            lock (_lock)
            {
                _handlers.Remove(handler);
                System.Diagnostics.Debug.WriteLine($"[BackButtonService] Unregistered handler. Count: {_handlers.Count}");
            }
        }

        public async Task<bool> HandleBackAsync()
        {
            List<Func<Task<bool>>> handlersSnapshot;
            lock (_lock)
            {
                handlersSnapshot = new List<Func<Task<bool>>>(_handlers);
                System.Diagnostics.Debug.WriteLine($"[BackButtonService] HandleBackAsync called. Handler count snapshot: {handlersSnapshot.Count}");
            }

            // Iterate backwards (LIFO)
            for (int i = handlersSnapshot.Count - 1; i >= 0; i--)
            {
                var handler = handlersSnapshot[i];
                try
                {
                    bool handled = await handler();
                    System.Diagnostics.Debug.WriteLine($"[BackButtonService] Handler {i} returned: {handled}");
                    if (handled)
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in BackButton handler: {ex.Message}");
                }
            }

            return false;
        }
    }
}
