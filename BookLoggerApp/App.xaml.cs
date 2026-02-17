using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp
{
    public partial class App : Application
    {
        private readonly ITimerStateService _timerStateService;

        public App(ITimerStateService timerStateService)
        {
            System.Diagnostics.Debug.WriteLine("=== App Constructor Started ===");
            _timerStateService = timerStateService;

            InitializeComponent();
            System.Diagnostics.Debug.WriteLine("InitializeComponent completed");
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            System.Diagnostics.Debug.WriteLine("=== CreateWindow Started ===");
            var window = new Window(new MainPage()) { Title = "BookLoggerApp" };

            window.Resumed += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine("=== App Window Resumed ===");
                _timerStateService.NotifyAppResumed();
            };

            return window;
        }
    }
}
