using Microsoft.EntityFrameworkCore;
using ScheduleViewer.Data;
using ScheduleViewer.Services;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ScheduleViewer
{
    public static class DatabaseReset
    {
        public static async Task ResetDatabaseAsync()
        {
            try
            {
                // Получаем путь к файлу базы данных
                string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "BD", "schedule.db");
                
                // Проверяем существование файла базы данных
                if (File.Exists(dbPath))
                {
                    // Удаляем файл базы данных
                    File.Delete(dbPath);
                    Logger.Log("Существующий файл базы данных удален", LogLevel.Info);
                }
                
                // Создаем новый контекст базы данных
                var dbContext = new ScheduleDbContext();
                
                // Убеждаемся, что база данных создана
                await dbContext.Database.EnsureCreatedAsync();
                Logger.Log("База данных успешно пересоздана с новой структурой", LogLevel.Info);
                
                return;
            }
            catch (Exception ex)
            {
                Logger.Log($"Ошибка при сбросе базы данных: {ex.Message}", LogLevel.Error);
                throw;
            }
        }
    }
} 