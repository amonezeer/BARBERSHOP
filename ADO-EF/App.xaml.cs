using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using MaterialDesignThemes.Wpf;
using ADO_EF.Data;
using ADO_EF.Services;
using Microsoft.Extensions.Logging;

namespace ADO_EF
{
    public partial class App : Application
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;

        public App()
        {
            _configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            ChangeTheme("Light");
        }

        private void ConfigureServices(ServiceCollection services)
        {
            services.AddSingleton<IConfiguration>(_configuration);

            services.AddDbContext<DataContext>(options =>
                options.UseSqlServer(_configuration.GetConnectionString("LocalDb")));

            services.AddLogging(logging =>
            {
                logging.AddConsole();
                logging.AddDebug();
            });

            services.AddScoped<DatabaseService>();
            services.AddScoped<EmailService>();
            services.AddScoped<TwilioService>(); 
            services.AddScoped<MainWindow>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при запуске приложения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        public void ChangeTheme(string baseTheme)
        {
            Resources.MergedDictionaries.Clear();

            var baseThemeMode = baseTheme == "Dark" ? BaseTheme.Dark : BaseTheme.Light;

            var primaryColor = (Color)ColorConverter.ConvertFromString("#673AB7");
            var secondaryColor = (Color)ColorConverter.ConvertFromString("#9C27B0");

            var theme = Theme.Create(
                baseThemeMode,
                primaryColor,
                secondaryColor
            );

            var themeDict = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.{baseTheme}.xaml")
            };

            var materialDict = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml")
            };

            Resources.MergedDictionaries.Add(themeDict);
            Resources.MergedDictionaries.Add(materialDict);
            Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/MaterialDesignColors;component/Themes/Recommended/Primary/MaterialDesignColor.DeepPurple.xaml") });
            Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/MaterialDesignColors;component/Themes/Recommended/Secondary/MaterialDesignColor.Purple.xaml") });

            var paletteHelper = new PaletteHelper();
            paletteHelper.SetTheme(theme);
        }
    }
}