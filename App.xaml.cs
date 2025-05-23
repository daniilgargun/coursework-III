using System;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ScheduleViewer.Data;
using ScheduleViewer.Services;
using System.Text;
using LogLevel = ScheduleViewer.Services.LogLevel;

namespace ScheduleViewer
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            try
            {
                // Установка пользовательского пути для логов
                string customLogPath = Path.Combine(Environment.CurrentDirectory, "log");
                if (!Directory.Exists(customLogPath))
                {
                    Directory.CreateDirectory(customLogPath);
                }
                Logger.SetCustomLogDirectory(customLogPath);
                
                // Инициализация системы логирования
                Logger.Initialize();
                Logger.Log("Приложение запущено", LogLevel.Info);
                
                // Кодировка для корректного отображения русских символов
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                Logger.Log("Зарегистрирован провайдер кодировок", LogLevel.Info);
                
                // Сбрасываем и пересоздаем базу данных при первом запуске приложения
                Task.Run(async () => 
                {
                    await DatabaseReset.ResetDatabaseAsync();
                }).Wait();
                
                // Регистрация обработчиков необработанных исключений
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
                Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при инициализации приложения: {ex.Message}", 
                    "Ошибка инициализации", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Logger.Log("Приложение завершает работу", LogLevel.Info);
            base.OnExit(e);
        }
        
        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Logger.LogException(e.Exception, "Необработанное исключение UI потока");
            HandleUnhandledException(e.Exception, "работе пользовательского интерфейса");
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                Logger.LogException(ex, "Необработанное исключение домена");
                HandleUnhandledException(ex, "работе приложения");
            }
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Logger.LogException(e.Exception, "Необработанное исключение в задаче");
            e.SetObserved();
        }
        
        private void HandleUnhandledException(Exception ex, string source)
        {
            string errorMessage = $"Произошла ошибка при {source}: {ex.Message}";
            
            Logger.Log(errorMessage, LogLevel.Error);
            
            var result = MessageBox.Show(
                $"{errorMessage}\n\nДополнительная информация сохранена в файл журнала:\n{Logger.GetCurrentLogFilePath()}\n\nХотите открыть журнал?",
                "Ошибка приложения",
                MessageBoxButton.YesNo,
                MessageBoxImage.Error);
                
            if (result == MessageBoxResult.Yes)
            {
                Logger.OpenCurrentLogFile();
            }
        }
    }
}
