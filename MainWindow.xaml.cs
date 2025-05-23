using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ScheduleViewer.Data;
using ScheduleViewer.ViewModels;
using ScheduleViewer.Services;
using System.ComponentModel;

namespace ScheduleViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private GridViewColumnHeader _lastHeaderClicked = null;
        private ListSortDirection _lastDirection = ListSortDirection.Ascending;
        
        public MainWindow()
        {
            InitializeComponent();
            
            // Инициализация контекста данных
            var dbContext = new ScheduleDbContext();
            var scheduleService = new ScheduleService(dbContext);
            var scheduleParser = new ScheduleParser();
            
            // Установка DataContext
            DataContext = new MainViewModel(scheduleService, scheduleParser);
        }
        
        /// <summary>
        /// Обработчик клика по заголовку столбца для сортировки
        /// </summary>
        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            var headerClicked = e.OriginalSource as GridViewColumnHeader;
            ListSortDirection direction;
            
            if (headerClicked == null || headerClicked.Role == GridViewColumnHeaderRole.Padding)
            {
                return;
            }
            
            // Определяем направление сортировки
            if (headerClicked != _lastHeaderClicked)
            {
                direction = ListSortDirection.Ascending;
            }
            else
            {
                direction = _lastDirection == ListSortDirection.Ascending ? 
                    ListSortDirection.Descending : ListSortDirection.Ascending;
            }
            
            // Получаем имя свойства для сортировки
            var header = headerClicked.Column.Header as string;
            string sortBy = "";
            
            switch (header)
            {
                case "Дата":
                    sortBy = "Date";
                    break;
                case "Время":
                    sortBy = "StartTime";
                    break;
                case "Предмет":
                    sortBy = "Subject.Name";
                    break;
                case "Преподаватель":
                    sortBy = "Teacher.FullName";
                    break;
                case "Аудитория":
                    sortBy = "Classroom.FullName";
                    break;
                case "Группа":
                    sortBy = "Group.Name";
                    break;
                case "Тип занятия":
                    sortBy = "LessonType";
                    break;
                default:
                    return;
            }
            
            // Сортировка данных
            SortDataGrid(sortBy, direction);
            
            // Сохраняем значения для следующего клика
            _lastHeaderClicked = headerClicked;
            _lastDirection = direction;
        }
        
        /// <summary>
        /// Сортировка данных в ListView
        /// </summary>
        private void SortDataGrid(string sortBy, ListSortDirection direction)
        {
            ICollectionView dataView = CollectionViewSource.GetDefaultView(ScheduleListView.ItemsSource);
            
            if (dataView != null)
            {
                dataView.SortDescriptions.Clear();
                
                // Добавляем новое описание сортировки
                SortDescription sortDescription = new SortDescription(sortBy, direction);
                dataView.SortDescriptions.Add(sortDescription);
                
                // Если сортируем по свойству вложенного объекта, может потребоваться обновление представления
                dataView.Refresh();
            }
        }
    }
}