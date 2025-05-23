using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ScheduleViewer.Models
{
    public class Subject
    {
        public int Id { get; set; }
        
        [Required, MaxLength(200)]
        public string Name { get; set; }
        
        [MaxLength(500)]
        public string Description { get; set; }
        
        // Навигационное свойство для связи с расписанием
        public virtual ICollection<Schedule> Schedules { get; set; }
        
        public Subject()
        {
            Schedules = new List<Schedule>();
            Description = "Описание не указано"; // Значение по умолчанию
        }
    }
} 