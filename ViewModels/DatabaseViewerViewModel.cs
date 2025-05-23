using ScheduleViewer.Data;
using ScheduleViewer.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;

namespace ScheduleViewer.ViewModels
{
    public class DatabaseViewerViewModel : ViewModelBase
    {
        private readonly ScheduleDbContext _dbContext;
        private ObservableCollection<string> _tableNames;
        private string _selectedTableName;
        private string _statusMessage;

        // ViewModel для каждой таблицы
        public DataGridViewModel<Group> GroupsViewModel { get; }
        public DataGridViewModel<Subject> SubjectsViewModel { get; }
        public DataGridViewModel<Teacher> TeachersViewModel { get; }
        public DataGridViewModel<Classroom> ClassroomsViewModel { get; }
        public DataGridViewModel<Schedule> SchedulesViewModel { get; }

        public ObservableCollection<string> TableNames
        {
            get => _tableNames;
            set => SetProperty(ref _tableNames, value);
        }

        public string SelectedTableName
        {
            get => _selectedTableName;
            set => SetProperty(ref _selectedTableName, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // Команды
        public ICommand RefreshAllCommand { get; }

        public DatabaseViewerViewModel(ScheduleDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            
            // Инициализация списка таблиц
            _tableNames = new ObservableCollection<string>
            {
                "Группы",
                "Предметы",
                "Преподаватели",
                "Аудитории",
                "Расписание"
            };
            
            _selectedTableName = _tableNames.FirstOrDefault();
            _statusMessage = "Готово";
            
            // Инициализация ViewModel для каждой таблицы
            GroupsViewModel = new DataGridViewModel<Group>(_dbContext, _dbContext.Groups, "Группы");
            SubjectsViewModel = new DataGridViewModel<Subject>(_dbContext, _dbContext.Subjects, "Предметы");
            TeachersViewModel = new DataGridViewModel<Teacher>(_dbContext, _dbContext.Teachers, "Преподаватели");
            ClassroomsViewModel = new DataGridViewModel<Classroom>(_dbContext, _dbContext.Classrooms, "Аудитории");
            SchedulesViewModel = new DataGridViewModel<Schedule>(_dbContext, _dbContext.Schedules, "Расписание");
            
            // Инициализация команд
            RefreshAllCommand = new RelayCommand(async _ => await RefreshAllAsync());
        }

        private async Task RefreshAllAsync()
        {
            StatusMessage = "Обновление всех таблиц...";
            
            try
            {
                // Вызываем команды обновления для каждой таблицы
                await Task.WhenAll(
                    ((RelayCommand)GroupsViewModel.RefreshCommand).ExecuteAsync(null),
                    ((RelayCommand)SubjectsViewModel.RefreshCommand).ExecuteAsync(null),
                    ((RelayCommand)TeachersViewModel.RefreshCommand).ExecuteAsync(null),
                    ((RelayCommand)ClassroomsViewModel.RefreshCommand).ExecuteAsync(null),
                    ((RelayCommand)SchedulesViewModel.RefreshCommand).ExecuteAsync(null)
                );
                
                StatusMessage = "Все таблицы обновлены";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка при обновлении таблиц: {ex.Message}";
            }
        }
    }
} 