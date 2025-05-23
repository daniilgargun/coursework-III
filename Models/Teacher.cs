using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ScheduleViewer.Models
{
    public class Teacher
    {
        public int Id { get; set; }
        
        [Required, MaxLength(100)]
        public string Name { get; set; }
        
        // Для обратной совместимости с интерфейсом
        public string FullName => Name;
        
        // Навигационное свойство для связи с расписанием
        public virtual ICollection<Schedule> Schedules { get; set; }
        
        public Teacher()
        {
            Schedules = new List<Schedule>();
        }
    }
} 