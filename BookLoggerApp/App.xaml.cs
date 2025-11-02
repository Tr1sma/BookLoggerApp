namespace BookLoggerApp
{
    public partial class App : Application
    {
        public App()
        {
            System.Diagnostics.Debug.WriteLine("=== App Constructor Started ===");
            
            InitializeComponent();
            System.Diagnostics.Debug.WriteLine("InitializeComponent completed");
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            System.Diagnostics.Debug.WriteLine("=== CreateWindow Started ===");
            return new Window(new MainPage()) { Title = "BookLoggerApp" };
        }
    }
}
