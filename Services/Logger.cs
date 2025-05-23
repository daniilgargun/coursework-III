using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text;

namespace ScheduleViewer.Services
{
    public static class Logger
    {
        private static string _logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScheduleViewer",
            "Logs");
            
        private static string _logFilePath;
            
        private static bool _initialized = false;
        
        /// <summary>
        /// Устанавливает пользовательский путь для директории журналов
        /// </summary>
        public static void SetCustomLogDirectory(string customPath)
        {
            if (string.IsNullOrWhiteSpace(customPath))
                return;
                
            try
            {
                if (!Directory.Exists(customPath))
                {
                    Directory.CreateDirectory(customPath);
                }
                
                _logDirectory = customPath;
                _logFilePath = Path.Combine(_logDirectory, $"log_{DateTime.Now:yyyy-MM-dd}.txt");
                
                Log($"Путь журнала изменен на: {_logDirectory}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при установке пути журнала: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Инициализирует систему логирования, создавая необходимые директории
        /// </summary>
        public static void Initialize()
        {
            try
            {
                if (!_initialized)
                {
                    // Устанавливаем кодировку по умолчанию для корректного отображения русских символов
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                    
                    if (!Directory.Exists(_logDirectory))
                    {
                        Directory.CreateDirectory(_logDirectory);
                    }
                    
                    _logFilePath = Path.Combine(_logDirectory, $"log_{DateTime.Now:yyyy-MM-dd}.txt");
                    
                    // Создаем файл, если он не существует, и записываем заголовок
                    if (!File.Exists(_logFilePath))
                    {
                        File.WriteAllText(_logFilePath, $"=== Журнал ScheduleViewer начат {DateTime.Now} ===\r\n", Encoding.UTF8);
                    }
                    
                    _initialized = true;
                    
                    Log("Система логирования инициализирована", LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при инициализации системы логирования: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Записывает сообщение в журнал
        /// </summary>
        public static void Log(string message, LogLevel level = LogLevel.Debug)
        {
            try
            {
                if (!_initialized)
                {
                    Initialize();
                }
                
                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
                
                // Записываем в отладочную консоль
                Debug.WriteLine(logMessage);
                
                // Записываем в файл
                using (StreamWriter writer = new StreamWriter(_logFilePath, true, Encoding.UTF8))
                {
                    writer.WriteLine(logMessage);
                }
                
                // Если это ошибка, дополнительно записываем в файл ошибок
                if (level == LogLevel.Error)
                {
                    string errorFilePath = Path.Combine(_logDirectory, $"error_{DateTime.Now:yyyyMMdd}.log");
                    using (StreamWriter writer = new StreamWriter(errorFilePath, true, Encoding.UTF8))
                    {
                        writer.WriteLine(logMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при записи в журнал: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Записывает информацию об исключении в журнал
        /// </summary>
        public static void LogException(Exception ex, string context = "")
        {
            try
            {
                string message = string.IsNullOrEmpty(context) 
                    ? $"Исключение: {ex.GetType().Name}: {ex.Message}"
                    : $"Исключение в {context}: {ex.GetType().Name}: {ex.Message}";
                    
                Log(message, LogLevel.Error);
                
                if (ex.StackTrace != null)
                {
                    Log($"Stack Trace: {ex.StackTrace}", LogLevel.Error);
                }
                
                if (ex.InnerException != null)
                {
                    Log($"Внутреннее исключение: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}", LogLevel.Error);
                    
                    if (ex.InnerException.StackTrace != null)
                    {
                        Log($"Inner Stack Trace: {ex.InnerException.StackTrace}", LogLevel.Error);
                    }
                }
            }
            catch (Exception logEx)
            {
                Debug.WriteLine($"Ошибка при записи исключения в журнал: {logEx.Message}");
            }
        }
        
        /// <summary>
        /// Возвращает путь к текущему файлу журнала
        /// </summary>
        public static string GetCurrentLogFilePath()
        {
            if (!_initialized)
            {
                Initialize();
            }
            
            return _logFilePath;
        }
        
        /// <summary>
        /// Возвращает путь к директории журналов
        /// </summary>
        public static string GetLogDirectory()
        {
            return _logDirectory;
        }
        
        /// <summary>
        /// Открывает текущий файл журнала в программе по умолчанию
        /// </summary>
        public static void OpenCurrentLogFile()
        {
            try
            {
                if (File.Exists(_logFilePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _logFilePath,
                        UseShellExecute = true
                    });
                    
                    Log("Открыт файл журнала", LogLevel.Info);
                }
                else
                {
                    Log("Не удалось открыть файл журнала: файл не существует", LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                Log($"Ошибка при открытии файла журнала: {ex.Message}", LogLevel.Error);
            }
        }
        
        /// <summary>
        /// Открывает директорию с журналами
        /// </summary>
        public static void OpenLogDirectory()
        {
            try
            {
                if (Directory.Exists(_logDirectory))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _logDirectory,
                        UseShellExecute = true
                    });
                    
                    Log("Открыта директория журналов", LogLevel.Info);
                }
                else
                {
                    Log("Не удалось открыть директорию журналов: директория не существует", LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                Log($"Ошибка при открытии директории журналов: {ex.Message}", LogLevel.Error);
            }
        }
    }
    
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
} 