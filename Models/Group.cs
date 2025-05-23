using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ScheduleViewer.Models
{
    public class Group
    {
        public int Id { get; set; }
        
        [Required, MaxLength(100)]
        public string Name { get; set; }
        
        // Навигационное свойство для связи с расписанием
        public virtual ICollection<Schedule> Schedules { get; set; }
        
        public Group()
        {
            Schedules = new List<Schedule>();
        }
    }
} 