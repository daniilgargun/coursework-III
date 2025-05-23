using ScheduleViewer.Models;
using ScheduleViewer.Services;
using ScheduleViewer.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;

namespace ScheduleViewer.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ScheduleService _scheduleService;
        private readonly ScheduleParser _scheduleParser;
        
        private ObservableCollection<Group> _groups;
        private ObservableCollection<Teacher> _teachers;
        private ObservableCollection<Schedule> _scheduleItems;
        private Group? _selectedGroup;
        private Teacher? _selectedTeacher;
        private DateTime _selectedDate;
        private bool _isLoading;
        private string _statusMessage;
        private bool _loadCurrentDateOnly;

        public ObservableCollection<Group> Groups
        {
            get => _groups;
            set => SetProperty(ref _groups, value);
        }

        public ObservableCollection<Teacher> Teachers
        {
            get => _teachers;
            set => SetProperty(ref _teachers, value);
        }

        public ObservableCollection<Schedule> ScheduleItems
        {
            get => _scheduleItems;
            set => SetProperty(ref _scheduleItems, value);
        }

        public Group? SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                if (SetProperty(ref _selectedGroup, value))
                {
                    // Если выбрана группа, сбрасываем выбранного преподавателя
                    if (value != null) 
                    {
                        _selectedTeacher = null;
                        OnPropertyChanged(nameof(SelectedTeacher));
                        LoadScheduleForGroupAsync().ConfigureAwait(false);
                    }
                }
            }
        }

        public Teacher? SelectedTeacher
        {
            get => _selectedTeacher;
            set
            {
                if (SetProperty(ref _selectedTeacher, value))
                {
                    // Если выбран преподаватель, сбрасываем выбранную группу
                    if (value != null)
                    {
                        _selectedGroup = null;
                        OnPropertyChanged(nameof(SelectedGroup));
                        LoadScheduleForTeacherAsync().ConfigureAwait(false);
                    }
                }
            }
        }

        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (SetProperty(ref _selectedDate, value))
                {
                    RefreshScheduleAsync().ConfigureAwait(false);
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool LoadCurrentDateOnly
        {
            get => _loadCurrentDateOnly;
            set => SetProperty(ref _loadCurrentDateOnly, value);
        }

        // Команды
        public ICommand RefreshCommand { get; }
        public ICommand UpdateFromRemoteCommand { get; }
        public ICommand OpenDatabaseViewerCommand { get; }
        public ICommand TestConnectionCommand { get; }
        public ICommand OpenCurrentLogCommand { get; }
        public ICommand OpenLogDirectoryCommand { get; }
        public ICommand OpenHtmlDebugCommand { get; }

        // Новые команды для обновленного интерфейса
        public ICommand SearchCommand { get; }
        public ICommand SelectTodayCommand { get; }
        public ICommand SelectTomorrowCommand { get; }
        public ICommand SelectWeekCommand { get; }

        public MainViewModel(ScheduleService scheduleService, ScheduleParser scheduleParser)
        {
            _scheduleService = scheduleService ?? throw new ArgumentNullException(nameof(scheduleService));
            _scheduleParser = scheduleParser ?? throw new ArgumentNullException(nameof(scheduleParser));
            
            // Инициализация коллекций
            _groups = new ObservableCollection<Group>();
            _teachers = new ObservableCollection<Teacher>();
            _scheduleItems = new ObservableCollection<Schedule>();
            
            // Установка начальных значений
            _selectedDate = DateTime.Today;
            _isLoading = false;
            _statusMessage = "Готов к работе";
            _loadCurrentDateOnly = true; // По умолчанию показываем только выбранный день
            
            // Инициализация команд
            RefreshCommand = new RelayCommand(async _ => await RefreshScheduleAsync());
            UpdateFromRemoteCommand = new RelayCommand(async _ => await UpdateFromRemoteAsync());
            OpenDatabaseViewerCommand = new RelayCommand(_ => 
            {
                OpenDatabaseViewer();
                return Task.CompletedTask;
            });
            TestConnectionCommand = new RelayCommand(async _ => await TestBtkConnection());
            OpenCurrentLogCommand = new RelayCommand(_ => 
            {
                Logger.OpenCurrentLogFile();
                return Task.CompletedTask;
            });
            OpenLogDirectoryCommand = new RelayCommand(_ => 
            {
                Logger.OpenLogDirectory();
                return Task.CompletedTask;
            });
            OpenHtmlDebugCommand = new RelayCommand(_ => 
            {
                OpenHtmlDebugFile();
                return Task.CompletedTask;
            });
            
            // Инициализация новых команд
            SearchCommand = new RelayCommand(async param => await SearchScheduleAsync(param?.ToString()));
            SelectTodayCommand = new RelayCommand(_ => 
            {
                SelectedDate = DateTime.Today;
                return Task.CompletedTask;
            });
            SelectTomorrowCommand = new RelayCommand(_ => 
            {
                SelectedDate = DateTime.Today.AddDays(1);
                return Task.CompletedTask;
            });
            SelectWeekCommand = new RelayCommand(_ => 
            {
                LoadCurrentDateOnly = false;
                RefreshScheduleAsync().ConfigureAwait(false);
                return Task.CompletedTask;
            });
            
            // Асинхронная инициализация данных
            InitializeAsync().ConfigureAwait(false);
            
            Logger.Log("MainViewModel инициализирован");
        }

        private async Task InitializeAsync()
        {
            IsLoading = true;
            StatusMessage = "Загрузка данных...";
            
            try
            {
                Logger.Log("Начало загрузки данных", LogLevel.Info);
                
                // Загрузка групп
                var groups = await _scheduleService.GetAllGroupsAsync();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Groups.Clear();
                    foreach (var group in groups)
                    {
                        Groups.Add(group);
                    }
                });
                Logger.Log($"Загружено {groups.Count} групп", LogLevel.Info);

                // Загрузка преподавателей
                var teachers = await _scheduleService.GetAllTeachersAsync();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Teachers.Clear();
                    foreach (var teacher in teachers)
                    {
                        Teachers.Add(teacher);
                    }
                });
                Logger.Log($"Загружено {teachers.Count} преподавателей", LogLevel.Info);

                // Если базы данных пусты, попробуем загрузить данные с сайта
                if (groups.Count == 0 || teachers.Count == 0)
                {
                    StatusMessage = "Загрузка данных с сайта БТК...";
                    Logger.Log("База данных пуста. Попытка загрузки данных с сайта", LogLevel.Warning);
                    
                    try
                    {
                        await UpdateFromRemoteAsync();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex, "Первоначальная загрузка данных с сайта");
                        StatusMessage = "Не удалось загрузить данные с сайта БТК.";
                        
                        // Показываем сообщение пользователю
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"Не удалось загрузить данные с сайта БТК.\n\n" +
                                          $"Причина: {ex.Message}\n\n" +
                                          $"Пожалуйста, проверьте подключение к интернету и повторите попытку.",
                                          "Ошибка обновления", MessageBoxButton.OK, MessageBoxImage.Warning);
                        });
                    }
                }
                else
                {
                    StatusMessage = "Данные загружены";
                    Logger.Log("Данные успешно загружены из базы данных", LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Загрузка данных");
                StatusMessage = $"Ошибка загрузки данных: {ex.Message}";
                
                // Показываем сообщение пользователю
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Произошла ошибка при загрузке данных: {ex.Message}", 
                                   "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Загрузка расписания для выбранной группы
        /// </summary>
        private async Task LoadScheduleForGroupAsync()
        {
            if (SelectedGroup == null)
                return;

            IsLoading = true;
            StatusMessage = $"Загрузка расписания для группы {SelectedGroup.Name}...";
            
            try
            {
                List<Schedule> scheduleItems;
                
                // Проверяем, нужно ли загрузить расписание только на выбранную дату
                if (LoadCurrentDateOnly)
                {
                    scheduleItems = await _scheduleService.GetScheduleForGroupByDateAsync(SelectedGroup.Id, SelectedDate);
                    StatusMessage = $"Расписание для группы {SelectedGroup.Name} на {SelectedDate:dd.MM.yyyy}";
                }
                else
                {
                    scheduleItems = await _scheduleService.GetScheduleForGroupCurrentWeekAsync(SelectedGroup.Id);
                    StatusMessage = $"Расписание для группы {SelectedGroup.Name} на текущую неделю";
                }
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ScheduleItems.Clear();
                    foreach (var item in scheduleItems)
                    {
                        ScheduleItems.Add(item);
                    }
                });
                
                if (scheduleItems.Count == 0)
                {
                    if (LoadCurrentDateOnly)
                        StatusMessage = $"Расписание для группы {SelectedGroup.Name} на {SelectedDate:dd.MM.yyyy} не найдено";
                    else
                        StatusMessage = $"Расписание для группы {SelectedGroup.Name} на неделю не найдено. Попробуйте обновить данные с сайта.";
                }
                else
                {
                    if (LoadCurrentDateOnly)
                        StatusMessage = $"Расписание для группы {SelectedGroup.Name} на {SelectedDate:dd.MM.yyyy} загружено ({scheduleItems.Count} занятий)";
                    else
                        StatusMessage = $"Расписание для группы {SelectedGroup.Name} на неделю загружено ({scheduleItems.Count} занятий)";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки расписания: {ex.Message}");
                StatusMessage = $"Ошибка загрузки расписания: {ex.Message}";
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Произошла ошибка при загрузке расписания для группы: {ex.Message}", 
                                   "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Загрузка расписания для выбранного преподавателя
        /// </summary>
        private async Task LoadScheduleForTeacherAsync()
        {
            if (SelectedTeacher == null)
                return;

            IsLoading = true;
            StatusMessage = $"Загрузка расписания для преподавателя {SelectedTeacher.FullName}...";
            
            try
            {
                List<Schedule> scheduleItems;
                
                // Проверяем, нужно ли загрузить расписание только на выбранную дату
                if (LoadCurrentDateOnly)
                {
                    scheduleItems = await _scheduleService.GetScheduleForTeacherByDateAsync(SelectedTeacher.Id, SelectedDate);
                    StatusMessage = $"Расписание для преподавателя {SelectedTeacher.FullName} на {SelectedDate:dd.MM.yyyy}";
                }
                else
                {
                    scheduleItems = await _scheduleService.GetScheduleForTeacherCurrentWeekAsync(SelectedTeacher.Id);
                    StatusMessage = $"Расписание для преподавателя {SelectedTeacher.FullName} на текущую неделю";
                }
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ScheduleItems.Clear();
                    foreach (var item in scheduleItems)
                    {
                        ScheduleItems.Add(item);
                    }
                });
                
                if (scheduleItems.Count == 0)
                {
                    if (LoadCurrentDateOnly)
                        StatusMessage = $"Расписание для преподавателя {SelectedTeacher.FullName} на {SelectedDate:dd.MM.yyyy} не найдено";
                    else
                        StatusMessage = $"Расписание для преподавателя {SelectedTeacher.FullName} на неделю не найдено. Попробуйте обновить данные с сайта.";
                }
                else
                {
                    if (LoadCurrentDateOnly)
                        StatusMessage = $"Расписание для преподавателя {SelectedTeacher.FullName} на {SelectedDate:dd.MM.yyyy} загружено ({scheduleItems.Count} занятий)";
                    else
                        StatusMessage = $"Расписание для преподавателя {SelectedTeacher.FullName} на неделю загружено ({scheduleItems.Count} занятий)";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки расписания: {ex.Message}");
                StatusMessage = $"Ошибка загрузки расписания: {ex.Message}";
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Произошла ошибка при загрузке расписания для преподавателя: {ex.Message}", 
                                   "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RefreshScheduleAsync()
        {
            if (SelectedGroup != null)
            {
                await LoadScheduleForGroupAsync();
            }
            else if (SelectedTeacher != null)
            {
                await LoadScheduleForTeacherAsync();
            }
            else
            {
                StatusMessage = "Выберите группу или преподавателя для просмотра расписания";
            }
        }

        /// <summary>
        /// Показывает расширенное сообщение об ошибке с возможностью открытия сайта БТК
        /// </summary>
        private void ShowErrorDialog(string title, string message, MessageBoxImage icon = MessageBoxImage.Error)
        {
            Logger.Log($"Отображение диалога с ошибкой: {title}, {message}", LogLevel.Warning);
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                var result = MessageBox.Show(
                    $"{message}\n\nХотите открыть сайт БТК в браузере для проверки доступности?\n\nПодробная информация записана в файл журнала:\n{Logger.GetCurrentLogFilePath()}",
                    title,
                    MessageBoxButton.YesNoCancel,
                    icon,
                    MessageBoxResult.No,
                    MessageBoxOptions.DefaultDesktopOnly);
                    
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Открываем сайт БТК в браузере по умолчанию
                        Logger.Log("Открытие сайта БТК в браузере", LogLevel.Info);
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "https://bartc.by/index.php/obuchayushchemusya/dnevnoe-otdelenie/tekushchee-raspisanie",
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex, "Открытие сайта БТК в браузере");
                        MessageBox.Show(
                            $"Не удалось открыть сайт в браузере. Ошибка: {ex.Message}",
                            "Ошибка",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    // Открываем файл журнала логов
                    Logger.Log("Открытие журнала логов из диалога ошибки", LogLevel.Info);
                    Logger.OpenCurrentLogFile();
                }
            });
        }

        private async Task UpdateFromRemoteAsync()
        {
            IsLoading = true;
            StatusMessage = "Обновление расписания с сайта БТК...";
            
            try
            {
                // Проверяем подключение к интернету
                try
                {
                    Debug.WriteLine("Проверка подключения к интернету...");
                    var client = new System.Net.Http.HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(5);
                    await client.GetAsync("https://www.google.com");
                    Debug.WriteLine("Подключение к интернету успешно проверено");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка проверки подключения к интернету: {ex.Message}");
                    throw new Exception("Отсутствует подключение к интернету. Пожалуйста, проверьте подключение и повторите попытку.");
                }
                
                // Пробуем загрузить данные с сайта БТК напрямую, используя URL по умолчанию
                Debug.WriteLine("Начинаем обновление расписания с сайта БТК...");
                
                try 
                {
                    var result = await _scheduleParser.UpdateScheduleFromRemoteSourceAsync(
                        new Data.ScheduleDbContext());
                    
                    if (result)
                    {
                        StatusMessage = "Расписание успешно обновлено с сайта БТК";
                        Debug.WriteLine("Расписание успешно обновлено с сайта БТК");
                        
                        // Обновляем списки после импорта
                        var groups = await _scheduleService.GetAllGroupsAsync();
                        var teachers = await _scheduleService.GetAllTeachersAsync();
                        
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Groups.Clear();
                            foreach (var group in groups)
                            {
                                Groups.Add(group);
                            }
                            
                            Teachers.Clear();
                            foreach (var teacher in teachers)
                            {
                                Teachers.Add(teacher);
                            }
                            
                            // Показываем сообщение об успешном обновлении
                            MessageBox.Show($"Расписание успешно обновлено с сайта БТК. Загружено {groups.Count} групп, {teachers.Count} преподавателей.",
                                        "Обновление расписания", MessageBoxButton.OK, MessageBoxImage.Information);
                        });
                        
                        await RefreshScheduleAsync();
                    }
                    else
                    {
                        StatusMessage = "Не удалось обновить расписание с сайта БТК";
                        Debug.WriteLine("Обновление расписания не выполнено - метод вернул false");
                        
                        string errorMessage = "Не удалось получить данные с сайта БТК.\n\n" +
                                     "Возможные причины:\n" +
                                     "- Отсутствует подключение к интернету\n" +
                                     "- Сайт БТК недоступен или изменил структуру\n" +
                                     "- Не удалось найти расписание на странице\n\n" +
                                     "Рекомендации:\n" +
                                     "- Проверьте подключение к интернету\n" +
                                     "- Попробуйте открыть сайт БТК в браузере";
                        
                        // Показываем сообщение об ошибке
                        ShowErrorDialog("Ошибка обновления расписания", errorMessage, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Исключение при обновлении расписания: {ex.Message}");
                    StatusMessage = $"Ошибка обновления расписания: {ex.Message}";
                    
                    string errorDetails = $"Произошла ошибка при обновлении расписания с сайта БТК:\n{ex.Message}";
                    string errorType = ex.GetType().Name;
                    
                    string errorMessage = $"{errorDetails}\n\n" +
                                        "Тип ошибки: {errorType}\n\n" +
                                        "Возможные причины:\n" +
                                        "- Отсутствует подключение к интернету\n" +
                                        "- Сайт БТК недоступен или изменил структуру\n" +
                                        "- Проблемы с доступом к базе данных\n\n" +
                                        "Дополнительная информация записана в файл журнала.";
                    
                    // Показываем сообщение об ошибке с детальной информацией
                    ShowErrorDialog("Ошибка при обновлении расписания", errorMessage, MessageBoxImage.Error);
                    
                    throw; // Пробрасываем исключение дальше
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Внешнее исключение при обновлении расписания: {ex.Message}");
                StatusMessage = $"Ошибка обновления расписания: {ex.Message}";
                
                throw; // Пробрасываем исключение дальше для обработки в вызывающем коде
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void OpenDatabaseViewer()
        {
            try
            {
                var databaseViewerWindow = new DatabaseViewerWindow();
                databaseViewerWindow.Show();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при открытии просмотра базы данных: {ex.Message}");
                StatusMessage = $"Ошибка при открытии просмотра базы данных: {ex.Message}";
                
                MessageBox.Show($"Ошибка при открытии просмотра базы данных: {ex.Message}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Выполняет проверку подключения к сайту БТК и отображает результаты
        /// </summary>
        private async Task TestBtkConnection()
        {
            IsLoading = true;
            StatusMessage = "Проверка подключения к сайту БТК...";
            Logger.Log("Запуск проверки подключения к сайту БТК", LogLevel.Info);
            
            try
            {
                string results = await _scheduleParser.TestConnectionAsync();
                Logger.Log("Результаты проверки подключения:", LogLevel.Info);
                Logger.Log(results, LogLevel.Info);
                
                // Отображаем результаты в диалоговом окне
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var resultWindow = new Window
                    {
                        Title = "Результаты проверки подключения к сайту БТК",
                        Width = 600,
                        Height = 400,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.WhiteSmoke)
                    };
                    
                    var grid = new System.Windows.Controls.Grid();
                    grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
                    
                    var textBox = new System.Windows.Controls.TextBox
                    {
                        Text = results,
                        IsReadOnly = true,
                        TextWrapping = TextWrapping.Wrap,
                        VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                        Margin = new Thickness(10),
                        BorderThickness = new Thickness(1),
                        BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGray),
                        Padding = new Thickness(5)
                    };
                    System.Windows.Controls.Grid.SetRow(textBox, 0);
                    
                    var buttonsPanel = new System.Windows.Controls.StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(10)
                    };
                    System.Windows.Controls.Grid.SetRow(buttonsPanel, 1);
                    
                    var openLogButton = new System.Windows.Controls.Button
                    {
                        Content = "Открыть журнал логов",
                        Padding = new Thickness(10, 5, 10, 5),
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    openLogButton.Click += (s, e) =>
                    {
                        Logger.OpenCurrentLogFile();
                    };
                    
                    var openBrowserButton = new System.Windows.Controls.Button
                    {
                        Content = "Открыть сайт БТК в браузере",
                        Padding = new Thickness(10, 5, 10, 5)
                    };
                    openBrowserButton.Click += (s, e) =>
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "https://bartc.by/index.php/obuchayushchemusya/dnevnoe-otdelenie/tekushchee-raspisanie",
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            Logger.LogException(ex, "Открытие сайта в браузере");
                            MessageBox.Show(
                                $"Не удалось открыть сайт в браузере. Ошибка: {ex.Message}",
                                "Ошибка",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                    };
                    
                    buttonsPanel.Children.Add(openLogButton);
                    buttonsPanel.Children.Add(openBrowserButton);
                    
                    grid.Children.Add(textBox);
                    grid.Children.Add(buttonsPanel);
                    
                    resultWindow.Content = grid;
                    resultWindow.ShowDialog();
                });
                
                StatusMessage = "Проверка подключения завершена";
                Logger.Log("Проверка подключения успешно завершена", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Проверка подключения");
                StatusMessage = $"Ошибка при проверке подключения: {ex.Message}";
                
                // Показываем сообщение об ошибке
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Произошла ошибка при проверке подключения: {ex.Message}",
                                   "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Открывает файл с отладочным HTML для анализа
        /// </summary>
        private void OpenHtmlDebugFile()
        {
            try 
            {
                string debugHtmlPath = Path.Combine(Logger.GetLogDirectory(), "last_html_debug.html");
                
                if (File.Exists(debugHtmlPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = debugHtmlPath,
                        UseShellExecute = true
                    });
                    Logger.Log("Открыт файл HTML-отладки", LogLevel.Info);
                }
                else
                {
                    MessageBox.Show(
                        "Файл с отладочным HTML не найден. Сначала запустите обновление расписания, чтобы создать этот файл.",
                        "Файл не найден",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Открытие HTML-отладки");
                MessageBox.Show(
                    $"Не удалось открыть файл отладочного HTML. Ошибка: {ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Поиск расписания по универсальному запросу
        /// </summary>
        private async Task SearchScheduleAsync(string searchQuery)
        {
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                StatusMessage = "Введите запрос для поиска";
                return;
            }

            IsLoading = true;
            StatusMessage = $"Поиск по запросу: {searchQuery}...";
            
            try
            {
                // Ищем группу
                var group = _groups.FirstOrDefault(g => g.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase));
                if (group != null)
                {
                    SelectedGroup = group;
                    StatusMessage = $"Найдена группа: {group.Name}";
                    return;
                }
                
                // Ищем преподавателя
                var teacher = _teachers.FirstOrDefault(t => t.FullName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase));
                if (teacher != null)
                {
                    SelectedTeacher = teacher;
                    StatusMessage = $"Найден преподаватель: {teacher.FullName}";
                    return;
                }
                
                // Если ничего не нашли, пробуем загрузить расписание по номеру аудитории
                var classrooms = await _scheduleService.GetAllClassroomsAsync();
                var classroom = classrooms.FirstOrDefault(c => c.Number.Contains(searchQuery, StringComparison.OrdinalIgnoreCase));
                
                if (classroom != null)
                {
                    IsLoading = true;
                    StatusMessage = $"Загрузка расписания для аудитории {classroom.Number}...";
                    
                    List<Schedule> scheduleItems;
                    if (LoadCurrentDateOnly)
                    {
                        scheduleItems = await _scheduleService.GetScheduleForClassroomByDateAsync(classroom.Id, SelectedDate);
                        StatusMessage = $"Расписание для аудитории {classroom.Number} на {SelectedDate:dd.MM.yyyy}";
                    }
                    else
                    {
                        // Здесь можно реализовать получение расписания для аудитории на неделю, если такой метод есть
                        scheduleItems = await _scheduleService.GetScheduleForClassroomByDateAsync(classroom.Id, SelectedDate);
                        StatusMessage = $"Расписание для аудитории {classroom.Number} на {SelectedDate:dd.MM.yyyy}";
                    }
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ScheduleItems.Clear();
                        foreach (var item in scheduleItems)
                        {
                            ScheduleItems.Add(item);
                        }
                    });
                    
                    if (scheduleItems.Count == 0)
                    {
                        StatusMessage = $"Расписание для аудитории {classroom.Number} не найдено";
                    }
                    
                    return;
                }
                
                // Ищем по названию предмета
                var subjects = await _scheduleService.GetAllSubjectsAsync();
                var subject = subjects.FirstOrDefault(s => s.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase));
                
                if (subject != null)
                {
                    IsLoading = true;
                    StatusMessage = $"Поиск занятий по предмету {subject.Name}...";
                    
                    // Здесь нужно реализовать отдельный метод в ScheduleService для поиска по предмету
                    // Но пока используем общий список и фильтруем его в памяти
                    var allSchedules = new List<Schedule>();
                    
                    foreach (var g in _groups)
                    {
                        var schedules = await _scheduleService.GetScheduleForGroupCurrentWeekAsync(g.Id);
                        allSchedules.AddRange(schedules);
                    }
                    
                    var filteredSchedules = allSchedules
                        .Where(s => s.SubjectId == subject.Id)
                        .OrderBy(s => s.Date)
                        .ThenBy(s => s.StartTime)
                        .ToList();
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ScheduleItems.Clear();
                        foreach (var item in filteredSchedules)
                        {
                            ScheduleItems.Add(item);
                        }
                    });
                    
                    if (filteredSchedules.Count == 0)
                    {
                        StatusMessage = $"Занятия по предмету '{subject.Name}' не найдены";
                    }
                    else
                    {
                        StatusMessage = $"Найдено {filteredSchedules.Count} занятий по предмету '{subject.Name}'";
                    }
                    
                    return;
                }
                
                StatusMessage = "Ничего не найдено по вашему запросу";
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Поиск расписания");
                StatusMessage = $"Ошибка при поиске: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
} 