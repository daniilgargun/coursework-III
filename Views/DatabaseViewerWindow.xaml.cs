using ScheduleViewer.Data;
using ScheduleViewer.ViewModels;
using System;
using System.Windows;

namespace ScheduleViewer.Views
{
    /// <summary>
    /// Логика взаимодействия для DatabaseViewerWindow.xaml
    /// </summary>
    public partial class DatabaseViewerWindow : Window
    {
        public DatabaseViewerWindow()
        {
            InitializeComponent();
            
            // Создаем контекст базы данных
            var dbContext = new ScheduleDbContext();
            
            // Устанавливаем DataContext
            DataContext = new DatabaseViewerViewModel(dbContext);
        }

        private void DataGridView_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
} 