using Microsoft.EntityFrameworkCore;
using ScheduleViewer.Data;
using ScheduleViewer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ScheduleViewer.Services
{
    public class ScheduleService
    {
        private readonly ScheduleDbContext _dbContext;

        public ScheduleService(ScheduleDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// Получение всех групп
        /// </summary>
        public async Task<List<Group>> GetAllGroupsAsync()
        {
            return await _dbContext.Groups.OrderBy(g => g.Name).ToListAsync();
        }

        /// <summary>
        /// Получение всех предметов
        /// </summary>
        public async Task<List<Subject>> GetAllSubjectsAsync()
        {
            return await _dbContext.Subjects.ToListAsync();
        }

        /// <summary>
        /// Получение всех преподавателей
        /// </summary>
        public async Task<List<Teacher>> GetAllTeachersAsync()
        {
            return await _dbContext.Teachers.OrderBy(t => t.Name).ToListAsync();
        }

        /// <summary>
        /// Получение всех аудиторий
        /// </summary>
        public async Task<List<Classroom>> GetAllClassroomsAsync()
        {
            return await _dbContext.Classrooms.ToListAsync();
        }

        /// <summary>
        /// Получение расписания для конкретной группы на указанную дату
        /// </summary>
        public async Task<List<Schedule>> GetScheduleForGroupByDateAsync(int groupId, DateTime date)
        {
            try
            {
                // Получаем данные без сортировки по StartTime в SQL-запросе
                var schedules = await _dbContext.Schedules
                    .Include(s => s.Group)
                    .Include(s => s.Subject)
                    .Include(s => s.Teacher)
                    .Include(s => s.Classroom)
                    .Where(s => s.GroupId == groupId && s.Date.Date == date.Date)
                    .ToListAsync();
                
                // Выполняем сортировку в памяти
                return schedules.OrderBy(s => s.StartTime).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при получении расписания для группы на дату: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Получение расписания для конкретной группы на текущую неделю
        /// </summary>
        public async Task<List<Schedule>> GetScheduleForGroupCurrentWeekAsync(int groupId)
        {
            var today = DateTime.Today;
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek + 1); // Понедельник текущей недели
            var endOfWeek = startOfWeek.AddDays(6); // Воскресенье текущей недели
            
            try
            {
                // Сначала получаем данные без сортировки по времени
                var schedules = await _dbContext.Schedules
                    .Include(s => s.Group)
                    .Include(s => s.Subject)
                    .Include(s => s.Teacher)
                    .Include(s => s.Classroom)
                    .Where(s => s.GroupId == groupId && s.Date >= startOfWeek && s.Date <= endOfWeek)
                    .OrderBy(s => s.Date)
                    .ToListAsync();
                
                // Затем сортируем в памяти по времени
                return schedules.OrderBy(s => s.Date).ThenBy(s => s.StartTime).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при получении расписания для группы: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Получение расписания для конкретного преподавателя на текущую неделю
        /// </summary>
        public async Task<List<Schedule>> GetScheduleForTeacherCurrentWeekAsync(int teacherId)
        {
            var today = DateTime.Today;
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek + 1); // Понедельник текущей недели
            var endOfWeek = startOfWeek.AddDays(6); // Воскресенье текущей недели
            
            try
            {
                // Сначала получаем данные без сортировки по времени
                var schedules = await _dbContext.Schedules
                    .Include(s => s.Group)
                    .Include(s => s.Subject)
                    .Include(s => s.Teacher)
                    .Include(s => s.Classroom)
                    .Where(s => s.TeacherId == teacherId && s.Date >= startOfWeek && s.Date <= endOfWeek)
                    .OrderBy(s => s.Date)
                    .ToListAsync();
                
                // Затем сортируем в памяти по времени
                return schedules.OrderBy(s => s.Date).ThenBy(s => s.StartTime).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при получении расписания для преподавателя: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Получение занятий в конкретной аудитории на указанную дату
        /// </summary>
        public async Task<List<Schedule>> GetScheduleForClassroomByDateAsync(int classroomId, DateTime date)
        {
            try
            {
                var schedules = await _dbContext.Schedules
                    .Include(s => s.Group)
                    .Include(s => s.Subject)
                    .Include(s => s.Teacher)
                    .Include(s => s.Classroom)
                    .Where(s => s.ClassroomId == classroomId && s.Date.Date == date.Date)
                    .ToListAsync();
                
                // Сортируем в памяти по времени начала
                return schedules.OrderBy(s => s.StartTime).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при получении расписания для аудитории: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Добавление нового занятия в расписание
        /// </summary>
        public async Task<bool> AddScheduleItemAsync(Schedule schedule)
        {
            try
            {
                await _dbContext.Schedules.AddAsync(schedule);
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при добавлении занятия: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Обновление информации о занятии
        /// </summary>
        public async Task<bool> UpdateScheduleItemAsync(Schedule schedule)
        {
            try
            {
                _dbContext.Schedules.Update(schedule);
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при обновлении занятия: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Удаление занятия из расписания
        /// </summary>
        public async Task<bool> DeleteScheduleItemAsync(int scheduleId)
        {
            try
            {
                var schedule = await _dbContext.Schedules.FindAsync(scheduleId);
                if (schedule != null)
                {
                    _dbContext.Schedules.Remove(schedule);
                    await _dbContext.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при удалении занятия: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Получение расписания для преподавателя на конкретную дату
        /// </summary>
        public async Task<List<Schedule>> GetScheduleForTeacherByDateAsync(int teacherId, DateTime date)
        {
            try
            {
                var schedules = await _dbContext.Schedules
                    .Include(s => s.Group)
                    .Include(s => s.Subject)
                    .Include(s => s.Teacher)
                    .Include(s => s.Classroom)
                    .Where(s => s.TeacherId == teacherId && s.Date.Date == date.Date)
                    .ToListAsync();
                
                // Сортируем в памяти по времени начала
                return schedules.OrderBy(s => s.StartTime).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при получении расписания для преподавателя на дату: {ex.Message}");
                throw;
            }
        }
    }
} 