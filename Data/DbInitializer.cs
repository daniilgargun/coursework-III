using ScheduleViewer.Models;
using ScheduleViewer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScheduleViewer.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(ScheduleDbContext context)
        {
            // Проверяем, что база данных создана
            context.Database.EnsureCreated();
            
            // Проверяем, есть ли уже записи в базе данных
            if (context.Groups.Any() || context.Subjects.Any() || context.Teachers.Any() || context.Classrooms.Any())
            {
                // База данных уже инициализирована
                System.Diagnostics.Debug.WriteLine("База данных уже содержит данные, инициализация пропущена");
                return;
            }
            
            // Пробуем загрузить данные с сайта БТК
            try
            {
                System.Diagnostics.Debug.WriteLine("Загрузка данных с сайта БТК...");
                var parser = new ScheduleParser();
                var success = await parser.UpdateScheduleFromRemoteSourceAsync(context);
                
                // Если данные успешно загружены, выходим
                if (success)
                {
                    System.Diagnostics.Debug.WriteLine("Данные успешно загружены с сайта БТК");
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine("Не удалось загрузить данные с сайта БТК");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки данных с сайта БТК: {ex.Message}");
            }
            
            // Сообщаем, что для работы приложения необходимо подключение к интернету
            System.Diagnostics.Debug.WriteLine("База данных пуста. Для работы приложения необходимо стабильное подключение к интернету и доступ к сайту БТК. Пожалуйста, загрузите данные с сайта БТК через интерфейс приложения.");
        }
        
        // Для обратной совместимости, вызываем асинхронный метод синхронно
        public static void Initialize(ScheduleDbContext context)
        {
            InitializeAsync(context).Wait();
        }
    }
} 