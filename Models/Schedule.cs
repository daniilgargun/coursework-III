using System;
using System.Collections.Generic;

namespace ScheduleViewer.Models
{
    public class Schedule
    {
        public int Id { get; set; }
        
        // Внешние ключи
        public int GroupId { get; set; }
        public int SubjectId { get; set; }
        public int TeacherId { get; set; }
        public int ClassroomId { get; set; }
        
        // Время проведения занятия
        public DateTime Date { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        
        // Тип занятия (лекция, семинар, лабораторная и т.д.)
        public string LessonType { get; set; } = string.Empty;
        
        // Навигационные свойства
        public virtual Group Group { get; set; } = null!;
        public virtual Subject Subject { get; set; } = null!;
        public virtual Teacher Teacher { get; set; } = null!;
        public virtual Classroom Classroom { get; set; } = null!;
        
        // Вычисляемые свойства
        public string DayOfWeek => Date.DayOfWeek.ToString();
        public bool IsCurrentWeek => Date.Date >= DateTime.Now.Date && Date.Date < DateTime.Now.Date.AddDays(7);
        
        // Номер пары на основе времени
        public int LessonNumber
        {
            get
            {
                // Определяем тип дня недели
                string dayType = GetDayType((int)Date.DayOfWeek);
                
                // Сопоставление времени с номером пары согласно расписанию звонков
                if (TimeSpanBetween(StartTime, new TimeSpan(8, 0, 0), new TimeSpan(9, 40, 0)))
                    return 1;
                else if (TimeSpanBetween(StartTime, new TimeSpan(9, 50, 0), new TimeSpan(11, 45, 0)))
                    return 2;
                else if (TimeSpanBetween(StartTime, new TimeSpan(12, 20, 0), new TimeSpan(14, 0, 0)))
                    return 3;
                else if (TimeSpanBetween(StartTime, new TimeSpan(14, 10, 0), new TimeSpan(15, 50, 0)) && dayType != "tuesday" ||
                         TimeSpanBetween(StartTime, new TimeSpan(15, 5, 0), new TimeSpan(16, 45, 0)) && dayType == "tuesday")
                    return 4;
                else if (TimeSpanBetween(StartTime, new TimeSpan(16, 0, 0), new TimeSpan(17, 40, 0)) && dayType != "tuesday" && dayType != "thursday" ||
                         TimeSpanBetween(StartTime, new TimeSpan(16, 55, 0), new TimeSpan(18, 35, 0)) && dayType == "tuesday" ||
                         TimeSpanBetween(StartTime, new TimeSpan(16, 35, 0), new TimeSpan(18, 15, 0)) && dayType == "thursday")
                    return 5;
                else if (TimeSpanBetween(StartTime, new TimeSpan(17, 50, 0), new TimeSpan(19, 25, 0)) && dayType == "normal" ||
                         TimeSpanBetween(StartTime, new TimeSpan(18, 45, 0), new TimeSpan(20, 20, 0)) && dayType == "tuesday" ||
                         TimeSpanBetween(StartTime, new TimeSpan(18, 25, 0), new TimeSpan(20, 0, 0)) && dayType == "thursday" ||
                         TimeSpanBetween(StartTime, new TimeSpan(17, 05, 0), new TimeSpan(18, 40, 0)) && dayType == "saturday")
                    return 6;
                
                return 0; // Неизвестный номер пары
            }
        }
        
        // Вспомогательные методы
        private bool TimeSpanBetween(TimeSpan time, TimeSpan start, TimeSpan end)
        {
            return time >= start && time <= end;
        }
        
        private string GetDayType(int weekday)
        {
            switch (weekday)
            {
                case 2: // Tuesday
                    return "tuesday";
                case 4: // Thursday
                    return "thursday";
                case 6: // Saturday
                    return "saturday";
                default:
                    return "normal"; // Monday, Wednesday, Friday
            }
        }
    }
} 