using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ScheduleViewer.Models
{
    public class Classroom
    {
        public int Id { get; set; }
        
        [MaxLength(50)]
        public string Number { get; set; }
        
        // В моделе требуется поле Building, добавляем его обратно
        [MaxLength(100)]
        public string Building { get; set; }
        
        // Требуется и поле Capacity
        public int Capacity { get; set; }
        
        // Навигационное свойство для связи с расписанием
        public virtual ICollection<Schedule> Schedules { get; set; }
        
        public Classroom()
        {
            Schedules = new List<Schedule>();
            Building = "БТК"; // Устанавливаем значение по умолчанию
            Capacity = 30; // Устанавливаем значение по умолчанию для вместимости
        }
    }
} 