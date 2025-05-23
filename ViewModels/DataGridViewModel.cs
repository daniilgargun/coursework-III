using ScheduleViewer.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;

namespace ScheduleViewer.ViewModels
{
    public class DataGridViewModel<T> : ViewModelBase where T : class
    {
        private readonly ScheduleDbContext _dbContext;
        private readonly DbSet<T> _dbSet;
        private readonly string _title;
        private ObservableCollection<T> _items;
        private T _selectedItem;

        public string Title
        {
            get => _title;
        }

        public ObservableCollection<T> Items
        {
            get => _items;
            set => SetProperty(ref _items, value);
        }

        public T SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        // Команды
        public ICommand RefreshCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand SaveCommand { get; }

        public DataGridViewModel(ScheduleDbContext dbContext, DbSet<T> dbSet, string title)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _dbSet = dbSet ?? throw new ArgumentNullException(nameof(dbSet));
            _title = title ?? throw new ArgumentNullException(nameof(title));
            _items = new ObservableCollection<T>();

            // Инициализация команд
            RefreshCommand = new RelayCommand(async _ => await RefreshDataAsync());
            AddCommand = new RelayCommand(_ => 
            {
                AddNewItem();
                return Task.CompletedTask;
            });
            DeleteCommand = new RelayCommand(_ => 
            {
                DeleteSelectedItem();
                return Task.CompletedTask;
            }, _ => SelectedItem != null);
            SaveCommand = new RelayCommand(async _ => await SaveChangesAsync());

            // Загрузка данных
            RefreshDataAsync().ConfigureAwait(false);
        }

        private async Task RefreshDataAsync()
        {
            try
            {
                var items = await _dbSet.ToListAsync();
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Items.Clear();
                    foreach (var item in items)
                    {
                        Items.Add(item);
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddNewItem()
        {
            try
            {
                var newItem = Activator.CreateInstance<T>();
                _dbSet.Add(newItem);
                Items.Add(newItem);
                SelectedItem = newItem;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении элемента: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteSelectedItem()
        {
            if (SelectedItem == null)
                return;

            var result = MessageBox.Show("Вы уверены, что хотите удалить выбранный элемент?", 
                "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _dbSet.Remove(SelectedItem);
                    Items.Remove(SelectedItem);
                    SelectedItem = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении элемента: {ex.Message}", "Ошибка", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async Task SaveChangesAsync()
        {
            try
            {
                await _dbContext.SaveChangesAsync();
                MessageBox.Show("Изменения успешно сохранены.", "Информация", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении изменений: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
} 